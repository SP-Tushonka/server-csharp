using System.Globalization;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Services.Profile;

namespace UnitTests.Tests.Services;

[TestFixture]
public class BackupServiceTests
{
    // Non-Gregorian default calendars, RTL, and native-digit cultures
    private static readonly string[] _cultureNames =
    [
        "en-US",
        "de-DE",
        "tr-TR",
        "ru-RU",
        "zh-CN",
        "th-TH",
        "fa-IR",
        "ar-SA",
        "ar-EG",
        "he-IL",
        "ja-JP",
    ];

    private BackupService _backupService;
    private string _tempDir;

    [OneTimeSetUp]
    public void Initialize()
    {
        _backupService = DI.GetInstance().GetService<BackupService>();
    }

    [SetUp]
    public void CreateTempDir()
    {
        _tempDir = Directory.CreateTempSubdirectory("spt-backup-tests").FullName;
    }

    [TearDown]
    public void RemoveTempDir()
    {
        Directory.Delete(_tempDir, true);
    }

    /// <summary>
    ///     Under globalization-invariant mode no named culture exists, so there is nothing for these cases to assert.
    /// </summary>
    private static CultureInfo GetCultureOrIgnore(string cultureName)
    {
        try
        {
            return new CultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            Assert.Ignore($"Culture '{cultureName}' is unavailable on this runtime");
            throw;
        }
    }

    private static void WithCulture(string cultureName, Action action)
    {
        var culture = GetCultureOrIgnore(cultureName);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestCaseSource(nameof(_cultureNames))]
    public void FormatBackupDate_UnderAnyCulture_ExpectGregorianAsciiName(string cultureName)
    {
        WithCulture(
            cultureName,
            () =>
            {
                var formatted = BackupService.FormatBackupDate(new DateTime(2026, 8, 15, 12, 34, 56, DateTimeKind.Utc));

                Assert.That(formatted, Is.EqualTo("2026-08-15_12-34-56"));
            }
        );
    }

    [TestCaseSource(nameof(_cultureNames))]
    public void ExtractDateFromFolderName_UnderAnyCulture_ExpectRoundTrip(string cultureName)
    {
        WithCulture(
            cultureName,
            () =>
            {
                var date = new DateTime(2026, 8, 15, 12, 34, 56, DateTimeKind.Utc);
                var folderName = BackupService.FormatBackupDate(date);

                var extracted = _backupService.ExtractDateFromFolderName(Path.Combine(_tempDir, folderName));

                Assert.That(extracted, Is.EqualTo(date));
            }
        );
    }

    // Folder names left behind by servers that formatted with the machine's own calendar
    [TestCase("2569-08-15_12-34-56", "2026-08-15T12:34:56")] // th-TH, Buddhist year
    [TestCase("1405-05-24_12-34-56", "2026-08-15T12:34:56")] // fa-IR, Persian year
    [TestCase("1448-03-02_12-34-56", "2026-08-15T12:34:56")] // ar-SA, UmAlQura year
    [TestCase("1445-05-01_00-00-00", "2023-11-15T00:00:00")] // ar-SA, a date where UmAlQura and Hijri disagree
    [TestCase("08-08-15_12-34-56", "2026-08-15T12:34:56")] // ja-JP with the Japanese calendar selected, Reiwa 8
    [TestCase("תשפ\"ו-י\"ב-ב'_12-34-56", "2026-08-15T12:34:56")] // he-IL with the Hebrew calendar selected
    public void ExtractDateFromFolderName_WithLegacyCalendarName_ExpectGregorianDate(string folderName, string expected)
    {
        GetCultureOrIgnore("th-TH");

        var extracted = _backupService.ExtractDateFromFolderName(Path.Combine(_tempDir, folderName));

        Assert.That(extracted, Is.EqualTo(DateTime.Parse(expected, CultureInfo.InvariantCulture)));
    }

    [TestCase("not-a-backup")]
    [TestCase("2026-13-45_99-99-99")]
    [TestCase("1899-12-31_00-00-00")]
    [TestCase("9999-01-01_00-00-00")]
    [TestCase("٢٠٢٦-٠٨-١٥_١٢-٣٤-٥٦")]
    [TestCase("")]
    public void ExtractDateFromFolderName_WithUnparseableName_ExpectNull(string folderName)
    {
        Assert.That(_backupService.ExtractDateFromFolderName(Path.Combine(_tempDir, folderName)), Is.Null);
    }

    [TestCaseSource(nameof(_cultureNames))]
    public void GetBackupPaths_WithMixedNames_ExpectOldestFirstAndInvalidDropped(string cultureName)
    {
        string[] folderNames =
        [
            "2026-08-15_12-34-56",
            "2024-01-02_03-04-05",
            "not-a-backup",
            "2569-08-15_12-00-00", // Buddhist year, same day as the first entry but 34 minutes earlier
            "activeMods.json.bak",
            "2025-06-01_00-00-00",
        ];

        foreach (var folderName in folderNames)
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, folderName));
        }

        WithCulture(
            cultureName,
            () =>
            {
                var paths = _backupService.GetBackupPaths(_tempDir).Select(Path.GetFileName).ToList();

                Assert.That(
                    paths,
                    Is.EqualTo(new[] { "2024-01-02_03-04-05", "2025-06-01_00-00-00", "2569-08-15_12-00-00", "2026-08-15_12-34-56" })
                );
            }
        );
    }

    // An install that ran on a Persian/Buddhist/Hijri machine before the format was pinned, then upgraded
    [Test]
    public void GetBackupPaths_WithLegacyAndGregorianNames_ExpectChronologicalOrder()
    {
        GetCultureOrIgnore("fa-IR");

        string[] folderNames =
        [
            "2026-08-15_18-00-00", // written after the upgrade
            "1405-05-24_12-34-56", // Persian, same day, 12:34
            "1447-08-19_09-00-00", // UmAlQura, 2026-02-06
            "2568-11-30_23-00-00", // Buddhist, 2025-11-30
            "9999-01-01_00-00-00", // unreadable in every calendar
        ];

        foreach (var folderName in folderNames)
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, folderName));
        }

        var paths = _backupService.GetBackupPaths(_tempDir).Select(Path.GetFileName).ToList();

        Assert.That(
            paths,
            Is.EqualTo(new[] { "2568-11-30_23-00-00", "1447-08-19_09-00-00", "1405-05-24_12-34-56", "2026-08-15_18-00-00" })
        );
    }

    [Test]
    public void CleanBackups_WithLegacyNames_ExpectOldestDeletedRegardlessOfCalendar()
    {
        GetCultureOrIgnore("fa-IR");

        string[] folderNames =
        [
            "1404-09-09_23-00-00", // Persian, 2025-11-30 - oldest, should go
            "1447-08-19_09-00-00", // UmAlQura, 2026-02-06 - should go
            "2569-08-15_12-00-00", // Buddhist, 2026-08-15 12:00
            "2026-08-15_18-00-00",
            "115-08-15_12-34-56", // ROC year, undecodable
        ];

        foreach (var folderName in folderNames)
        {
            Directory.CreateDirectory(Path.Combine(_tempDir, folderName));
        }

        var config = DI.GetInstance().GetService<BackupConfig>();
        var (originalDir, originalMax) = (config.Directory, config.MaxBackups);
        try
        {
            config.Directory = _tempDir;
            config.MaxBackups = 2;

            _backupService.CleanBackups();
        }
        finally
        {
            (config.Directory, config.MaxBackups) = (originalDir, originalMax);
        }

        var remaining = Directory.GetDirectories(_tempDir).Select(Path.GetFileName).Order();

        Assert.That(remaining, Is.EqualTo(new[] { "115-08-15_12-34-56", "2026-08-15_18-00-00", "2569-08-15_12-00-00" }));
    }

    [Test]
    public void GetBackupPaths_WithOnlyUnparseableNames_ExpectEmpty()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "not-a-backup"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "also-not-a-backup"));

        Assert.That(_backupService.GetBackupPaths(_tempDir), Is.Empty);
    }
}
