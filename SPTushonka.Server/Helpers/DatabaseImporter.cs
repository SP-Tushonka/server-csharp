using System.Diagnostics;
using System.Text;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Exceptions.Database;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

namespace SPTarkov.Server.Helpers;

public sealed class DatabaseImporter(
    ISptLogger<DatabaseImporter> logger,
    ServerLocalisationService serverLocalisationService,
    ImporterUtil importerUtil,
    JsonUtil jsonUtil
)
{
    private const string SptDataPath = "./SPT_Data/";
    private const int MaxReportedMissingFiles = 10;
    private readonly Dictionary<string, string> _databaseHashes = [];

    public async Task LoadHashesAsync(CancellationToken cancellationToken = default)
    {
        var checksFilePath = Path.Combine(SptDataPath, "checks.dat");

        if (!File.Exists(checksFilePath))
        {
            throw new ValidationErrorException(serverLocalisationService.GetText("validation_error_exception", checksFilePath));
        }

        try
        {
            await using var fs = File.OpenRead(checksFilePath);

            using var reader = new StreamReader(fs, Encoding.ASCII);
            var base64Content = await reader.ReadToEndAsync(cancellationToken);

            var jsonBytes = Convert.FromBase64String(base64Content);

            await using var ms = new MemoryStream(jsonBytes);

            var FileHashes = await jsonUtil.DeserializeFromMemoryStreamAsync<List<FileHash>>(ms, cancellationToken) ?? [];

            foreach (var hash in FileHashes)
            {
                _databaseHashes.Add(hash.Path, hash.Hash);
            }
        }
        catch (Exception ex)
        {
            throw new ValidationErrorException(serverLocalisationService.GetText("validation_error_exception", checksFilePath), ex);
        }

        VerifyFilesExist();
    }

    /// <summary>
    /// Check every file the manifest lists is still on disk, a deleted file is never read so hash verification alone misses it
    /// </summary>
    private void VerifyFilesExist()
    {
        var missing = _databaseHashes.Keys.Where(path => !File.Exists(Path.Combine(SptDataPath, path))).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var file in missing.Take(MaxReportedMissingFiles))
        {
            logger.Error(serverLocalisationService.GetText("validation_error_missing_file", file));
        }

        if (missing.Count > MaxReportedMissingFiles)
        {
            logger.Error(serverLocalisationService.GetText("validation_error_missing_file_overflow", missing.Count - MaxReportedMissingFiles));
        }

        throw new ValidationErrorException(serverLocalisationService.GetText("validation_error_missing_files", missing.Count));
    }

    /// <summary>
    /// Read all json files in database folder and map into a json object
    /// </summary>
    /// <param name="filePath">path to database folder</param>
    /// <param name="shouldVerifyDatabase">if the database should be verified after deserialization</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that can be used to cancel the database hydration operation.
    /// </param>
    /// <returns></returns>
    public async Task<DatabaseTables?> LoadDatabaseAsync(bool shouldVerifyDatabase, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.Info(serverLocalisationService.GetText("importing_database"));
            Stopwatch timer = new();
            timer.Start();

            var dataToImport = await importerUtil.LoadRecursiveAsync<DatabaseTables>(
                $"{SptDataPath}database/",
                shouldVerifyDatabase ? VerifyDatabaseAsync : null,
                cancellationToken: cancellationToken
            );

            timer.Stop();

            logger.Info(serverLocalisationService.GetText("importing_database_finish"));
            logger.Info($"Database import took {timer.ElapsedMilliseconds}ms");

            return dataToImport;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.Warning("Database import was cancelled.");

            throw;
        }
    }

    /// <summary>
    /// Compare the hash computed while the file was being deserialised against the shipped manifest.
    /// </summary>
    public Task VerifyDatabaseAsync(string fileName, string computedHash, CancellationToken cancellationToken)
    {
        var relativePath = fileName.StartsWith(SptDataPath, StringComparison.OrdinalIgnoreCase) ? fileName[SptDataPath.Length..] : fileName;

        if (!_databaseHashes.TryGetValue(relativePath, out var expectedHash) || expectedHash != computedHash)
        {
            throw new ValidationErrorException(serverLocalisationService.GetText("validation_error_file", fileName));
        }

        return Task.CompletedTask;
    }

    private sealed class FileHash
    {
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
    }
}
