using System.Collections.Concurrent;
using System.Text.Json;
using Runtime.LocalModes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;

namespace SPTarkov.Server.Core.Services.Profile;

// Candidate-only opt-in: user/local-modes/enabled.json must contain {"enabled":true}.
// Reads never create files or characters. Existing PvE data remains the account.
[Injectable(InjectionType.Singleton)]
public sealed class LocalGameModeService : ILocalModeProfileStore
{
    public const string AccountContextKey = "runtime.localModes.accountId";
    public const string SocketBindingKey = "runtime.localModes.socketBinding";
    private readonly SaveServer saves;
    private readonly LocalModeSessions sessions;
    private readonly SemaphoreSlim creation = new(1, 1);
    private readonly ConcurrentDictionary<string, object> ledgerGates = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> purchaseGates = new();
    private readonly Dictionary<string, RaidState> activeRaids = new();
    private readonly object raidGate = new();
    private Dictionary<string, Dictionary<string, string>>? index;
    private readonly object indexGate = new();
    private const string DirectoryName = "user/local-modes";
    private const string IndexPath = DirectoryName + "/index.json";

    public LocalGameModeService(SaveServer saves)
    {
        this.saves = saves;
        sessions = new(this);
        var settings = DirectoryName + "/enabled.json";
        if (File.Exists(settings))
        {
            using var config = JsonDocument.Parse(File.ReadAllBytes(settings));
            Enabled = config.RootElement.GetProperty("enabled").GetBoolean();
        }
    }

    public bool Enabled { get; }
    public object LedgerGate(MongoId id) => ledgerGates.GetOrAdd(AccountId(id).ToString(), _ => new object());
    public SemaphoreSlim PurchaseGate(MongoId id) => purchaseGates.GetOrAdd(AccountId(id).ToString(), _ => new(1, 1));
    public int AccountBalance(MongoId id) => saves.GetProfile(AccountId(id)).CharacterData?.PmcData?.TarCoinBalance ?? 0;

    public bool BeginRaid(MongoId profile)
    {
        if (!Enabled) return false;
        var binding = Capture(profile);
        if (binding.ProfileId != profile.ToString()) throw new InvalidOperationException("Raid character is not selected");
        lock (raidGate)
        {
            if (activeRaids.TryGetValue(binding.AccountId, out var active))
            {
                if (!active.InTransit || active.Profile != profile) throw new InvalidOperationException("Local raid already active");
                active.InTransit = false;
                return false;
            }
            activeRaids.Add(binding.AccountId, new RaidState(profile, sessions.EnterRaid(binding)));
            return true;
        }
    }

    public void RaidStarted(MongoId profile, string serverId)
    {
        if (!Enabled) return;
        lock (raidGate) activeRaids[AccountId(profile).ToString()].ServerId = serverId;
    }

    public void RaidStartFailed(MongoId profile, bool acquired)
    {
        if (!Enabled) return;
        lock (raidGate)
        {
            var account = AccountId(profile).ToString();
            if (!activeRaids.TryGetValue(account, out var state)) return;
            if (acquired) { activeRaids.Remove(account); state.Lease.Dispose(); }
            else state.InTransit = true;
        }
    }

    public void VerifyRaidEnd(MongoId profile, string serverId)
    {
        if (!Enabled) return;
        lock (raidGate)
            if (!activeRaids.TryGetValue(AccountId(profile).ToString(), out var active)
                || active.Profile != profile || active.ServerId != serverId || active.InTransit)
                throw new InvalidOperationException("Local raid result does not match the active character/serverId");
    }

    public void RaidSaved(MongoId profile, bool transfer)
    {
        if (!Enabled) return;
        lock (raidGate)
        {
            var account = AccountId(profile).ToString();
            var state = activeRaids[account];
            if (transfer) state.InTransit = true;
            else { activeRaids.Remove(account); state.Lease.Dispose(); }
        }
    }

    // Explicit game startup/logout is the existing local server session boundary.
    // A new guarded game may reuse this backend after the prior process ended.
    public void GameSessionBoundary(MongoId identity)
    {
        if (!Enabled) return;
        lock (raidGate)
            if (activeRaids.Remove(AccountId(identity).ToString(), out var state)) state.Lease.Dispose();
    }

    public MongoId AccountId(MongoId id)
    {
        if (!Enabled || id.IsEmpty) return id;
        var info = saves.GetProfile(id).ProfileInfo!;
        if (info.LocalModeOwnerId is not { } owner) return id;
        if (info.LocalMode is not ("regular" or "pvp-season") || !OwnsProfile(owner.ToString(), info.LocalMode, id.ToString()))
            throw new InvalidOperationException("Unowned internal local-mode character");
        return owner;
    }

    public LocalModeBinding Capture(MongoId account) => sessions.Capture(AccountId(account).ToString());
    public bool IsCurrent(LocalModeBinding binding) => sessions.IsCurrent(binding);
    public string CurrentMode(MongoId id) => Enabled ? Capture(id).RequestedMode : "pve";

    public static bool IsAccountRoute(string route)
    {
        var path = route.Split('?')[0].TrimEnd('/');
        return path.StartsWith("/launcher", StringComparison.Ordinal)
            || path is "/client/game/mode" or "/client/game/config" or "/v2/client/game/profiles"
                or "/client/game/token/issue" or "/v2/client/shop/token/generate";
    }

    public MongoId ResolveRequest(MongoId identity, string route)
    {
        if (!Enabled || identity.IsEmpty) return identity;
        var account = AccountId(identity);
        return IsAccountRoute(route) ? account : new MongoId(Capture(account).ProfileId);
    }

    public MongoId? ProfileForMode(MongoId account, string mode)
    {
        var found = FindProfile(AccountId(account).ToString(), mode);
        // MongoId has an implicit string conversion: an untyped null branch
        // otherwise produces an Empty MongoId rather than a missing slot.
        return found is null ? (MongoId?)null : new MongoId(found);
    }

    public async Task<LocalModeBinding> SelectAsync(MongoId identity, string? mode)
    {
        var account = AccountId(identity);
        if (mode is null) return Capture(account);
        if (!LocalModeSessions.IsSupported(mode)) throw new ArgumentException("Unsupported local game mode");
        // Serialize placeholder creation and index publication. No existing
        // character is deleted, copied or replaced when another mode is chosen.
        await creation.WaitAsync();
        await LocalModeAccountDeletion.MutationGate.WaitAsync();
        try
        {
            // Finish a committed deletion before any later index publication.
            new LocalModeAccountDeletion().Recover();
            lock (raidGate)
                if (activeRaids.ContainsKey(account.ToString()) && Capture(account).RequestedMode != mode)
                    throw new InvalidOperationException("Cannot switch local character during a raid or transfer");
            if (mode != "pve" && FindProfile(account.ToString(), mode) is null)
            {
                var owner = saves.GetProfile(account).ProfileInfo!;
                var id = new MongoId();
                saves.CreateProfile(new Info
                {
                    ProfileId = id,
                    ScavengerId = new MongoId(),
                    Aid = owner.Aid,
                    Username = $"local-mode:{account}:{mode}",
                    Edition = owner.Edition,
                    IsWiped = true,
                    LocalModeOwnerId = account,
                    LocalMode = mode,
                });
                // Persist the child first. An interrupted index publication
                // leaves an internal orphan, never an alias of the PvE profile.
                await saves.SaveProfileAsync(id);
                lock (indexGate)
                {
                    var next = ReadIndex().ToDictionary(pair => pair.Key, pair => new Dictionary<string, string>(pair.Value));
                    if (!next.TryGetValue(account.ToString(), out var modes)) next[account.ToString()] = modes = new();
                    modes.Add(mode, id.ToString());
                    WriteIndex(next);
                    index = next;
                }
            }
            return sessions.Select(account.ToString(), mode);
        }
        finally { LocalModeAccountDeletion.MutationGate.Release(); creation.Release(); }
    }

    public bool IsAccount(string id)
    {
        if (!MongoId.IsValidMongoId(id)) return false;
        var mongo = new MongoId(id);
        return saves.ProfileExists(mongo) && saves.GetProfile(mongo).ProfileInfo is { LocalModeOwnerId: null, LocalMode: null };
    }

    public string? FindProfile(string accountId, string mode)
    {
        if (!IsAccount(accountId) || !LocalModeSessions.IsSupported(mode)) return null;
        if (mode == "pve") return accountId;
        lock (indexGate)
            return ReadIndex().GetValueOrDefault(accountId)?.GetValueOrDefault(mode);
    }

    public bool OwnsProfile(string accountId, string mode, string profileId)
    {
        if (mode == "pve") return accountId == profileId && IsAccount(accountId);
        if (!MongoId.IsValidMongoId(profileId) || !saves.ProfileExists(new MongoId(profileId))) return false;
        var info = saves.GetProfile(new MongoId(profileId)).ProfileInfo;
        return FindProfile(accountId, mode) == profileId && info?.LocalModeOwnerId?.ToString() == accountId && info.LocalMode == mode;
    }

    private Dictionary<string, Dictionary<string, string>> ReadIndex()
    {
        // Called under indexGate. Reading an untouched account is side-effect free.
        // Reload the atomically published index; physical account deletion may
        // have changed it through SaveServer while this service stays alive.
        var loaded = File.Exists(IndexPath)
            ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllBytes(IndexPath))
                ?? throw new InvalidOperationException("Invalid local mode index")
            : new();
        var ids = new HashSet<string>();
        foreach (var (account, modes) in loaded)
        {
            if (!IsAccount(account)) throw new InvalidOperationException("Unknown account in local mode index");
            foreach (var (mode, id) in modes)
            {
                if (mode is not ("regular" or "pvp-season") || !MongoId.IsValidMongoId(id) || id == account || !ids.Add(id))
                    throw new InvalidOperationException("Invalid or aliased local mode index");
                var child = new MongoId(id);
                if (!saves.ProfileExists(child) || saves.GetProfile(child).ProfileInfo is not { } info
                    || info.LocalModeOwnerId?.ToString() != account || info.LocalMode != mode)
                    throw new InvalidOperationException("Unowned child in local mode index");
            }
        }
        return index = loaded;
    }

    private static void WriteIndex(Dictionary<string, Dictionary<string, string>> value)
    {
        Directory.CreateDirectory(DirectoryName);
        var temporary = IndexPath + ".pending";
        using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, value);
            file.Flush(true);
        }
        if (File.Exists(IndexPath)) File.Replace(temporary, IndexPath, IndexPath + ".backup");
        else File.Move(temporary, IndexPath);
    }

    private sealed class RaidState(MongoId profile, IDisposable lease)
    {
        public MongoId Profile { get; } = profile;
        public IDisposable Lease { get; } = lease;
        public string? ServerId { get; set; }
        public bool InTransit { get; set; }
    }
}
