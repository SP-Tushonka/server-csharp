using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Servers;

[Injectable(InjectionType.Singleton)]
public sealed class SaveServer(
    FileUtil fileUtil,
    IEnumerable<SaveLoadRouter> saveLoadRouters,
    JsonUtil jsonUtil,
    HashUtil hashUtil,
    ProfileMigrationService profileValidatorService,
    BackupService backupService,
    ISptLogger<SaveServer> logger,
    CoreConfig coreConfig
)
{
    private const string profileFilepath = "user/profiles/";

    private readonly ConcurrentDictionary<MongoId, SptProfile> profiles = new();
    private readonly ConcurrentDictionary<MongoId, string> saveMd5 = new();
    private readonly ConcurrentDictionary<MongoId, SemaphoreSlim> saveLocks = new();

    /// <summary>
    ///     Load all profiles in /user/profiles folder into memory (this.profiles)
    /// </summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        new LocalModeProfileJournal().Recover();
        new LocalModeAccountDeletion().Recover();

        // get files to load
        if (!fileUtil.DirectoryExists(profileFilepath))
        {
            fileUtil.CreateDirectory(profileFilepath);
        }

        var files = fileUtil.GetFiles(profileFilepath).Where(item => fileUtil.GetFileExtension(item) == "json");

        // load profiles
        var stopwatch = Stopwatch.StartNew();
        foreach (var file in files)
        {
            // Only allow files that fit the criteria of being a mongo id be parsed
            var filename = Path.GetFileNameWithoutExtension(file);
            if (MongoId.IsValidMongoId(filename))
            {
                await LoadProfileAsync(fileUtil.StripExtension(file), cancellationToken);
            }
        }

        stopwatch.Stop();
        if (logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"{files.Count()} Profiles took: {stopwatch.ElapsedMilliseconds}ms to load.");
        }
    }

    /// <summary>
    ///     Save changes for each profile from memory into user/profiles json
    /// </summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        // Save every profile
        var totalTime = 0L;
        foreach (var sessionID in profiles)
        {
            totalTime += await SaveProfileAsync(sessionID.Key, cancellationToken);
        }

        if (!profiles.IsEmpty && logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug($"Saved {profiles.Count} profiles, took: {totalTime}ms");
        }
    }

    /// <summary>
    ///     Get a player profile from memory
    /// </summary>
    /// <param name="sessionId"> Session ID </param>
    /// <returns> SptProfile of the player </returns>
    /// <exception cref="Exception"> Thrown when sessionId is null / empty or no profiles with that ID are found </exception>
    public SptProfile GetProfile(MongoId sessionId)
    {
        if (sessionId.IsEmpty)
        {
            throw new Exception("session id provided was empty, did you restart the server while the game was running?");
        }

        if (profiles == null || profiles.IsEmpty)
        {
            throw new Exception($"no profiles found in saveServer with id: {sessionId}");
        }

        if (!profiles.TryGetValue(sessionId, out var sptProfile))
        {
            throw new Exception($"no profile found for sessionId: {sessionId}");
        }

        return sptProfile;
    }

    private string SerializeForLocalTransaction(MongoId id) =>
        jsonUtil.Serialize(profiles[id], !coreConfig.Features.CompressProfile)
            ?? throw new InvalidOperationException("Could not serialize local transaction profile");

    private void RestoreLocalTransactionProfile(MongoId id, string json) =>
        profiles[id] = jsonUtil.Deserialize<SptProfile>(json)
            ?? throw new InvalidOperationException("Could not restore local transaction profile");

    public async Task<LocalProfileTransaction> BeginLocalProfileTransactionAsync(IEnumerable<MongoId> ids, CancellationToken cancellationToken = default)
    {
        var ordered = ids.Distinct().OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
        var held = new List<SemaphoreSlim>();
        try
        {
            foreach (var id in ordered)
            {
                var gate = saveLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken);
                held.Add(gate);
            }
            return new LocalProfileTransaction(this, ordered, held);
        }
        catch { foreach (var gate in held) gate.Release(); throw; }
    }

    public sealed class LocalProfileTransaction : IAsyncDisposable
    {
        private readonly SaveServer owner;
        private readonly MongoId[] ids;
        private readonly List<SemaphoreSlim> held;
        private readonly LocalModeProfileJournal journal = new();
        private readonly LocalModeProfileJournal.Handle handle;
        private bool finished;
        internal LocalProfileTransaction(SaveServer owner, MongoId[] ids, List<SemaphoreSlim> held)
        {
            this.owner = owner; this.ids = ids; this.held = held;
            handle = journal.Begin(Snapshot());
        }
        private Dictionary<string, string> Snapshot() => ids.ToDictionary(id => id.ToString(), owner.SerializeForLocalTransaction);
        public Task CommitAsync()
        {
            if (finished) throw new InvalidOperationException("Local transaction already finished");
            journal.Commit(handle, Snapshot());
            finished = true;
            Release();
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            if (!finished)
            {
                // A published commit is rolled forward; an uncommitted mutation
                // restores its exact before-images, both on disk and in memory.
                foreach (var (id, json) in journal.Rollback(handle))
                    owner.RestoreLocalTransactionProfile(new MongoId(id), json);
                finished = true;
                Release();
            }
            return ValueTask.CompletedTask;
        }
        private void Release()
        {
            foreach (var id in ids) owner.saveMd5.TryRemove(id, out _);
            foreach (var gate in held) gate.Release();
        }
    }

    public bool ProfileExists(MongoId id)
    {
        return profiles.ContainsKey(id);
    }

    /// <summary>
    ///     Gets all profiles from memory
    /// </summary>
    /// <returns> Dictionary of Profiles with their ID as Keys. </returns>
    public Dictionary<MongoId, SptProfile> GetProfiles()
    {
        return profiles.ToDictionary();
    }

    /// <summary>
    ///     Delete a profile by id (Does not remove the profile file!)
    /// </summary>
    /// <param name="sessionID"> ID of profile to remove </param>
    /// <returns> True when deleted, false when profile not found </returns>
    public bool DeleteProfileById(MongoId sessionID)
    {
        if (profiles.ContainsKey(sessionID))
        {
            if (profiles.TryRemove(sessionID, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Create a new profile in memory with empty pmc/scav objects
    /// </summary>
    /// <param name="profileInfo"> Basic profile data </param>
    /// <exception cref="Exception"> Thrown when profile already exists </exception>
    public void CreateProfile(Info profileInfo)
    {
        if (!profileInfo.ProfileId.HasValue)
        {
            // TODO: Localize me
            throw new Exception("Creating profile failed: profile has no sessionId");
        }

        if (profiles.ContainsKey(profileInfo.ProfileId.Value))
        {
            // TODO: Localize me
            throw new Exception($"Creating profile failed: profile already exists for sessionId: {profileInfo.ProfileId}");
        }

        profiles.TryAdd(
            profileInfo.ProfileId.Value,
            new SptProfile
            {
                ProfileInfo = profileInfo,
                CharacterData = new Characters { PmcData = new PmcData(), ScavData = new PmcData() },
            }
        );
    }

    /// <summary>
    ///     Add full profile in memory by key (info.id)
    /// </summary>
    /// <param name="profileDetails"> Profile to save </param>
    public void AddProfile(SptProfile profileDetails)
    {
        profiles.TryAdd(profileDetails.ProfileInfo!.ProfileId!.Value, profileDetails);
    }

    /// <summary>
    ///     Look up profile json in user/profiles by id and store in memory. <br />
    ///     Execute saveLoadRouters callbacks after being loaded into memory.
    /// </summary>
    /// <param name="sessionID"> ID of profile to store in memory </param>
    public async Task LoadProfileAsync(MongoId sessionID, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(profileFilepath, $"{sessionID}.json");
        if (fileUtil.FileExists(filePath))
        // File found, store in profiles[]
        {
            JsonObject? profile;

            try
            {
                profile = await jsonUtil.DeserializeFromFileAsync<JsonObject>(filePath, cancellationToken);
            }
            catch (JsonException e)
            {
                // If the profile fails to deserialize, it may have corrupted, try to restore from a backup
                logger.Warning($"Failed loading profile for {sessionID.ToString()}. Attempting to load backup");

                // We make a copy of the profile before overwriting it, just incase
                var corruptBackupPath = Path.Combine(profileFilepath, $"{sessionID}-corrupt.json");
                File.Copy(filePath, corruptBackupPath, true);

                if (backupService.RestoreProfile(sessionID))
                {
                    profile = await jsonUtil.DeserializeFromFileAsync<JsonObject>(filePath, cancellationToken);
                    logger.Success("Profile restored from backup!");
                }
                else
                {
                    throw new Exception("Failed to restore profile backup", e);
                }
            }

            if (profile is not null)
            {
                try
                {
                    profiles[sessionID] = profileValidatorService.MigrateAndValidateProfile(profile);
                }
                catch (InvalidOperationException ex)
                {
                    logger.Critical($"Failed to load profile with ID '{sessionID}'");
                    logger.Critical(ex.ToString());
                }
            }
        }

        // We don't proceed further here as only one object in the profile has data in it.
        if (IsProfileInvalidOrUnloadable(sessionID))
        {
            return;
        }

        // Run callbacks
        foreach (var callback in saveLoadRouters) // HealthSaveLoadRouter, InraidSaveLoadRouter, InsuranceSaveLoadRouter, ProfileSaveLoadRouter. THESE SHOULD EXIST IN HERE
        {
            profiles[sessionID] = callback.HandleLoad(GetProfile(sessionID));
        }
    }

    /// <summary>
    ///     Save changes from in-memory profile to user/profiles json
    ///     Execute onBeforeSaveCallbacks callbacks prior to being saved to json
    /// </summary>
    /// <param name="sessionID"> Profile id (user/profiles/id.json) </param>
    /// <returns> Time taken to save the profile in seconds </returns>
    public async Task<long> SaveProfileAsync(MongoId sessionID, CancellationToken cancellationToken = default)
    {
        // No need to save profiles that have been marked as invalid
        if (IsProfileInvalidOrUnloadable(sessionID))
        {
            return 0;
        }

        // Lock based on sessionID so we don't attempt to write to the same save file
        // multiple times at the same time, leading to file access contention
        var saveLock = saveLocks.GetOrAdd(sessionID, _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken);

        Stopwatch start;
        try
        {
            var filePath = Path.Combine(profileFilepath, $"{sessionID}.json");

            start = Stopwatch.StartNew();
            var jsonProfile =
                jsonUtil.Serialize(profiles[sessionID], !coreConfig.Features.CompressProfile)
                ?? throw new InvalidOperationException("Could not serialize profile for saving!");
            var fmd5 = await hashUtil.GenerateHashForDataAsync(HashingAlgorithm.MD5, jsonProfile, cancellationToken);
            if (!saveMd5.TryGetValue(sessionID, out var currentMd5) || currentMd5 != fmd5)
            {
                saveMd5[sessionID] = fmd5;
                // save profile to disk
                await fileUtil.WriteFileAsync(filePath, jsonProfile, cancellationToken);
            }

            start.Stop();
        }
        finally
        {
            saveLock.Release();
        }

        return start.ElapsedMilliseconds;
    }

    /// <summary>
    ///     Remove a physical profile json from user/profiles
    /// </summary>
    /// <param name="sessionID"> Profile ID to remove </param>
    /// <returns> True if successful </returns>
    public bool RemoveProfile(MongoId sessionID)
    {
        // First provisioning has no index yet. Serialize that case too, before
        // checking for children or choosing the ordinary account-only path.
        LocalModeAccountDeletion.MutationGate.Wait();
        var held = new List<SemaphoreSlim>();
        LocalModeAccountDeletion.Plan? plan = null;
        try
        {
            var deletion = new LocalModeAccountDeletion();
            deletion.Recover();
            var file = Path.Combine(profileFilepath, $"{sessionID}.json");
            if (!profiles.TryGetValue(sessionID, out var target)) return !fileUtil.FileExists(file);
            if (target.ProfileInfo is not { LocalModeOwnerId: null, LocalMode: null }) return false;
            var ids = deletion.IndexedIds(sessionID.ToString()).Select(id => new MongoId(id))
                .Concat(profiles.Where(x => x.Value.ProfileInfo?.LocalModeOwnerId == sessionID).Select(x => x.Key))
                .Distinct().OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
            foreach (var id in ids)
            {
                var gate = saveLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
                gate.Wait(); held.Add(gate);
            }
            // Pending saves completed before final bytes/ownership are captured.
            var loaded = profiles.ToDictionary(x => x.Key.ToString(), x => x.Value);
            if (!File.Exists("user/local-modes/index.json") && !File.Exists(file))
            {
                if (ids.Length != 1) throw new InvalidOperationException("Unindexed local mode child requires recovery before account deletion");
                profiles.TryRemove(sessionID, out _); saveMd5.TryRemove(sessionID, out _);
                return true; // Preserve ordinary deletion of an already absent file.
            }
            plan = deletion.Prepare(sessionID.ToString(), loaded);
            deletion.Commit(plan);
            return true;
        }
        finally
        {
            // A durable explicit-delete intent must not be undone by a later
            // background save while a filesystem failure awaits recovery.
            if (plan?.Committed == true)
                foreach (var id in plan.Ids)
                {
                    var mongo = new MongoId(id);
                    profiles.TryRemove(mongo, out _); saveMd5.TryRemove(mongo, out _);
                }
            foreach (var gate in held) gate.Release();
            LocalModeAccountDeletion.MutationGate.Release();
        }
    }

    /// <summary>
    /// Determines whether the specified profile is marked as invalid or cannot be loaded.
    /// </summary>
    /// <param name="sessionID">The ID of the profile to check.</param>
    /// <returns>
    /// <c>true</c> if the profile is invalid or unloadable; otherwise, <c>false</c>.
    /// </returns>
    public bool IsProfileInvalidOrUnloadable(MongoId sessionID)
    {
        if (
            profiles.TryGetValue(sessionID, out var profile)
            && profile.ProfileInfo!.InvalidOrUnloadableProfile is not null
            && profile.ProfileInfo!.InvalidOrUnloadableProfile!.Value
        )
        {
            return true;
        }

        return false;
    }
}
