using System.Globalization;
using Microsoft.Extensions.Logging;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Utils;

namespace SPTarkov.Server.Core.Services.Profile;

[Injectable(InjectionType.Singleton)]
public sealed class BackupService(
    ISptLogger<BackupService> logger,
    IReadOnlyList<SptMod> loadedMods,
    JsonUtil jsonUtil,
    TimeUtil timeUtil,
    BackupConfig backupConfig,
    FileUtil fileUtil
)
{
    private const string ProfileDir = "./user/profiles";
    private const string ActiveModsFilename = "activeMods.json";

    private readonly BackupConfig _backupConfig = backupConfig;

    // Runs InitializeAsync() every x minutes
    private Timer? _backupIntervalTimer;

    private readonly SemaphoreSlim _backupLock = new(1, 1);
    private long _lastBackupTimestamp;

    private const string BackupDateFormat = "yyyy-MM-dd_HH-mm-ss";

    private static readonly CultureInfo[] _cultures = BuildParseCultures();

    /// <summary>
    ///     Builds the cultures used to read backup folder names, in the order they're tried. <br />
    ///     Only invariant is available under globalization-invariant mode, where folder names written by a
    ///     non-Gregorian machine can't be decoded at all.
    /// </summary>
    /// <returns> The cultures to attempt a folder name with, invariant first. </returns>
    private static CultureInfo[] BuildParseCultures()
    {
        try
        {
            return
            [
                CultureInfo.InvariantCulture,
                new CultureInfo("fa-IR") { DateTimeFormat = { Calendar = new PersianCalendar() } },
                // ar-SA defaults to UmAlQura, which must be tried before Hijri: the two disagree by up to 2 days
                new CultureInfo("ar-SA") { DateTimeFormat = { Calendar = new UmAlQuraCalendar() } },
                new CultureInfo("ar-SA") { DateTimeFormat = { Calendar = new HijriCalendar() } },
                new CultureInfo("he-IL") { DateTimeFormat = { Calendar = new HebrewCalendar() } },
                new CultureInfo("th-TH") { DateTimeFormat = { Calendar = new ThaiBuddhistCalendar() } },
                new CultureInfo("ja-JP") { DateTimeFormat = { Calendar = new JapaneseCalendar() } },
            ];
        }
        catch (Exception)
        {
            return [CultureInfo.InvariantCulture];
        }
    }

    /// <summary>
    ///     Start the backup interval if enabled in config.
    /// </summary>
    public async Task StartBackupSystem(CancellationToken cancellationToken = default)
    {
        if (_backupIntervalTimer is not null)
        {
            return;
        }

        if (!_backupConfig.BackupInterval.Enabled)
        {
            // Not backing up at regular intervals, run once and exit
            await InitializeAsync(cancellationToken);

            return;
        }

        _backupIntervalTimer = new Timer(
            async void (_) =>
            {
                try
                {
                    await InitializeAsync(cancellationToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    logger.Error($"Profile backup failed: {ex.Message}", ex);
                }
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(_backupConfig.BackupInterval.IntervalMinutes)
        );

        cancellationToken.Register(() =>
        {
            _backupIntervalTimer?.Dispose();
            _backupIntervalTimer = null;
        });
    }

    /// <summary>
    ///     Run the backup process. <br />
    ///     This method orchestrates the profile backup service. Handles copying profiles to a backup directory and cleaning
    ///     up old backups if the number exceeds the configured maximum.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return;
        }

        // If the backup lock is already locked, skip backup. This stops multiple backups running at once
        // Passing 0 is a non-blocking Wait, will return false if the lock can't be acquired
        var lockAcquired = await _backupLock.WaitAsync(0, cancellationToken);
        if (!lockAcquired)
        {
            return;
        }

        try
        {
            // Make sure we don't back up too often by using a configurable Cooldown
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (currentTimestamp < _lastBackupTimestamp + _backupConfig.BackupCooldown)
            {
                return;
            }
            _lastBackupTimestamp = currentTimestamp;

            var targetDir = GenerateBackupTargetDir();

            // Fetch all profiles in the profile directory.
            List<string> currentProfilePaths;
            try
            {
                currentProfilePaths = fileUtil.GetFiles(ProfileDir);
            }
            catch (Exception ex)
            {
                logger.Debug($"Skipping profile backup: Unable to read profiles directory, {ex.Message}");
                return;
            }

            if (currentProfilePaths.Count == 0)
            {
                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug("No profiles to backup");
                }

                return;
            }

            try
            {
                fileUtil.CreateDirectory(targetDir);

                foreach (var profilePath in currentProfilePaths)
                {
                    // Get filename + extension, removing the path
                    var profileFileName = fileUtil.GetFileNameAndExtension(profilePath);

                    // Create absolute path to file
                    var relativeSourceFilePath = Path.Combine(ProfileDir, profileFileName);
                    var absoluteDestinationFilePath = Path.Combine(targetDir, profileFileName);
                    if (!fileUtil.CopyFile(relativeSourceFilePath, absoluteDestinationFilePath))
                    {
                        logger.Error($"Source file not found: {relativeSourceFilePath}. Cannot copy to: {absoluteDestinationFilePath}");
                    }
                }

                // Write a copy of active mods.
                await fileUtil.WriteFileAsync(
                    Path.Combine(targetDir, ActiveModsFilename),
                    jsonUtil.Serialize(GetActiveServerMods())
                        ?? throw new InvalidOperationException("Could not serialize active server mods!"),
                    cancellationToken
                );

                if (logger.IsLogEnabled(LogLevel.Debug))
                {
                    logger.Debug($"Profile backup created in: {targetDir}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Unable to write to backup profile directory: {ex.Message}");
                return;
            }

            try
            {
                CleanBackups();
            }
            catch (Exception ex)
            {
                logger.Error($"Unable to clean up old profile backups: {ex.Message}", ex);
            }
        }
        finally
        {
            _backupLock.Release();
        }
    }

    /// <summary>
    ///     Check to see if the backup service is enabled via the config.
    /// </summary>
    /// <returns> True if enabled, false otherwise. </returns>
    private bool IsEnabled()
    {
        if (_backupConfig.Enabled)
        {
            return true;
        }

        if (logger.IsLogEnabled(LogLevel.Debug))
        {
            logger.Debug("Profile backups disabled");
        }

        return false;
    }

    /// <summary>
    ///     Generates the target directory path for the backup. The directory path is constructed using the `directory` from
    ///     the configuration and the current backup date.
    /// </summary>
    /// <returns> The target directory path for the backup. </returns>
    private string GenerateBackupTargetDir()
    {
        var backupDate = GenerateBackupDate();
        return Path.GetFullPath($"{_backupConfig.Directory}/{backupDate}");
    }

    /// <summary>
    ///     Generates a formatted backup date string in the format `YYYY-MM-DD_hh-mm-ss`.
    /// </summary>
    /// <returns> The formatted backup date string. </returns>
    private string GenerateBackupDate()
    {
        return FormatBackupDate(timeUtil.GetDateTimeNow());
    }

    /// <summary>
    ///     Formats a date as a backup folder name. <br />
    ///     Always invariant, otherwise the machine's own calendar puts a Persian/Hijri/Buddhist year in the name.
    /// </summary>
    /// <param name="date"> The date to format. </param>
    /// <returns> The formatted backup date string. </returns>
    public static string FormatBackupDate(DateTime date)
    {
        return date.ToString(BackupDateFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     Cleans up old backups in the backup directory. <br />
    ///     If the number of backups exceeds the configured maximum, the oldest are deleted. Backups whose folder
    ///     name can't be dated are left alone, and don't count towards the maximum.
    /// </summary>
    public void CleanBackups()
    {
        var backupPaths = GetBackupPaths(_backupConfig.Directory);

        var excessCount = backupPaths.Count - _backupConfig.MaxBackups;
        if (excessCount > 0)
        {
            RemoveExcessBackups(backupPaths.GetRange(0, excessCount));
        }
    }

    private string? GetMostRecentProfileBackup(IEnumerable<string> backupPaths, string profileId)
    {
        var profileFilename = $"{profileId}.json";

        foreach (var backupPath in backupPaths.Reverse())
        {
            var profileBackups = fileUtil.GetFiles(backupPath);
            var profileBackup = profileBackups.FirstOrDefault(path => path.EndsWith(profileFilename));
            if (profileBackup != null)
            {
                return profileBackup;
            }
        }

        return null;
    }

    /// <summary>
    ///     Retrieves the backup directories from the specified directory, oldest first. <br />
    ///     Directories without a readable date in their name are skipped rather than sorted to an arbitrary position.
    /// </summary>
    /// <param name="dir"> The directory to search for backup files. </param>
    /// <returns> List of sorted backup file paths. </returns>
    public List<string> GetBackupPaths(string dir)
    {
        return fileUtil
            .GetDirectories(dir)
            .Select(path => (Path: path, Date: ExtractDateFromFolderName(path)))
            .Where(backup => backup.Date.HasValue)
            .OrderBy(backup => backup.Date!.Value)
            .Select(backup => backup.Path)
            .ToList();
    }

    /// <summary>
    ///     Extracts a date from a folder name string formatted as `YYYY-MM-DD_hh-mm-ss`. <br />
    ///     Older folders were named with the machine's own calendar, so a name is read as Gregorian first, then
    ///     as each calendar that could have written it, and is only accepted if the result is a plausible date.
    /// </summary>
    /// <param name="folderPath"> The name of the folder from which to extract the date. </param>
    /// <returns> A DateTime object if the folder name is in the correct format, otherwise null. </returns>
    public DateTime? ExtractDateFromFolderName(string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);

        var now = DateTime.UtcNow;
        var minDate = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var maxDate = now.AddYears(5);

        foreach (var culture in _cultures)
        {
            if (
                DateTime.TryParseExact(
                    folderName,
                    BackupDateFormat,
                    culture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt
                )
            )
            {
                if (dt >= minDate && dt <= maxDate)
                {
                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
            }
        }

        logger.Warning($"Invalid backup folder name format: {folderPath}, [{folderName}]");
        return null;
    }

    /// <summary>
    ///     Removes excess backups from the backup directory.
    /// </summary>
    /// <param name="backupFilenames"> List of backup file names to be removed. </param>
    /// <returns> A promise that resolves when all specified backups have been removed. </returns>
    private void RemoveExcessBackups(IEnumerable<string> backupFilenames)
    {
        var filePathsToDelete = backupFilenames.Select(x => x);
        foreach (var pathToDelete in filePathsToDelete)
        {
            fileUtil.DeleteDirectory(Path.Combine(pathToDelete), true);

            if (logger.IsLogEnabled(LogLevel.Debug))
            {
                logger.Debug($"Deleted old backup: {pathToDelete}");
            }
        }
    }

    /// <summary>
    ///     Get a List of active server mod details.
    /// </summary>
    /// <returns> A List of mod names. </returns>
    private List<string> GetActiveServerMods()
    {
        List<string> result = [];

        foreach (var mod in loadedMods)
        {
            result.Add($"{mod.ModMetadata.Author} - {mod.ModMetadata.Version}");
        }

        return result;
    }

    /// <summary>
    ///     Restores the most recent profile backup for the given profile Id
    /// </summary>
    /// <param name="profileId">The profile ID of the backup to restore</param>
    /// <returns>True on success. False on failure</returns>
    public bool RestoreProfile(string profileId)
    {
        var backupDir = _backupConfig.Directory;
        var backupPaths = GetBackupPaths(backupDir);
        var mostRecentBackupForProfile = GetMostRecentProfileBackup(backupPaths, profileId);

        // Verify we have a backup for this profile
        if (mostRecentBackupForProfile == null)
        {
            return false;
        }

        // Restore the most recent profile backup
        var profileFileName = fileUtil.GetFileNameAndExtension(mostRecentBackupForProfile);
        var targetProfilePath = Path.Combine(ProfileDir, profileFileName);

        File.Copy(mostRecentBackupForProfile, targetProfilePath, true);
        return true;
    }
}
