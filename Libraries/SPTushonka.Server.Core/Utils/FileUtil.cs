using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using SPTarkov.DI.Annotations;

namespace SPTarkov.Server.Core.Utils;

[Injectable]
public sealed class FileUtil
{
    private const string ModBasePath = "user/mods/";

    // "UnityFS\0"
    private static readonly byte[] _bundleSignature = [0x55, 0x6E, 0x69, 0x74, 0x79, 0x46, 0x53, 0x00];

    public List<string> GetFiles(string path, bool recursive = false, string searchPattern = "*")
    {
        var files = new List<string>(Directory.GetFiles(path, searchPattern));

        if (recursive)
        {
            files.AddRange(Directory.GetDirectories(path).SelectMany(d => GetFiles(d, recursive, searchPattern)));
        }

        return files;
    }

    public string[] GetDirectories(string path)
    {
        return Directory.GetDirectories(path);
    }

    public string GetFileExtension(string path)
    {
        return Path.GetExtension(path).Replace(".", "");
    }

    public string GetFileNameAndExtension(string path)
    {
        return Path.GetFileName(path);
    }

    public string StripExtension(string path, bool keepPath = false)
    {
        if (keepPath)
        {
            return path.StartsWith(".") ? path.Split('.')[1] : path.Split('.').First();
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public DirectoryInfo CreateDirectory(string path)
    {
        return Directory.CreateDirectory(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string ReadFile(string path)
    {
        return File.ReadAllText(path);
    }

    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task<byte[]> ReadFileAsBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllBytesAsync(path, cancellationToken);
    }

    public void WriteFile(string filePath, string fileContent)
    {
        if (!DirectoryExists(Path.GetDirectoryName(filePath)))
        {
            CreateDirectory(Path.GetDirectoryName(filePath));
        }

        if (!FileExists(filePath))
        {
            CreateFile(filePath);
        }

        File.WriteAllText(filePath, fileContent);
    }

    public void WriteFile(string filePath, byte[] fileContent)
    {
        if (!FileExists(filePath))
        {
            CreateFile(filePath);
        }

        File.WriteAllBytes(filePath, fileContent);
    }

    public async Task WriteFileAsync(string filePath, string fileContent, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(fileContent);
        await WriteFileAsync(filePath, bytes, cancellationToken);
    }

    /// <summary>
    /// Writes a file atomically by first writing to a temporary file, then replacing the original.
    /// This prevents corruption if the write operation fails or is interrupted.
    /// </summary>
    public async Task WriteFileAsync(string filePath, byte[] fileContent, CancellationToken cancellationToken = default)
    {
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var tempFilePath = filePath + ".bak";

        try
        {
            await using (
                var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)
            )
            {
                await fs.WriteAsync(fileContent, cancellationToken);

                // We flush here so we can be sure it's immediately committed to disk
                await fs.FlushAsync(cancellationToken);
                fs.Flush(true);
            }

            // Overwrite over the old file
            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch { }
            }
            throw;
        }
    }

    private void CreateFile(string filePath)
    {
        var stream = File.Create(filePath);
        stream.Close();
    }

    public bool DeleteFile(string filePath)
    {
        if (!FileExists(filePath))
        {
            return false;
        }

        File.Delete(filePath);
        return true;
    }

    /// <summary>
    ///     Copy a file from one path to another
    /// </summary>
    /// <param name="copyFromPath">Source file to copy from</param>
    /// <param name="destinationFilePath"></param>
    /// <param name="overwrite">Should destination file be overwritten</param>
    public bool CopyFile(string copyFromPath, string destinationFilePath, bool overwrite = false)
    {
        // Check it exists first
        if (!FileExists(copyFromPath))
        {
            return false;
        }

        // Ensure dir exists
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath));

        // Copy the file
        File.Copy(copyFromPath, destinationFilePath, overwrite);
        return true;
    }

    /// <summary>
    ///     Delete a directory, must be empty unless 'deleteContent' is set to 'true'
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="deleteContent"></param>
    public void DeleteDirectory(string directory, bool deleteContent = false)
    {
        Directory.Delete(directory, deleteContent);
    }

    public string GetModPath(string modName)
    {
        return Path.Combine(ModBasePath, modName);
    }

    /// <summary>
    ///     Check the header of a filestream to ensure it matches a Unity AssetBundle
    /// </summary>
    /// <param name="fileStream">The file stream to check the header for</param>
    /// <param name="cancellationToken">
    ///     The <see cref="CancellationToken"/> that can be used to cancel the bundle header verification operation.
    /// </param>
    /// <returns>True if the header matches the AssetBundle header, false if not.</returns>
    public async Task<bool> VerifyBundleHeaderAsync(FileStream fileStream, CancellationToken cancellationToken = default)
    {
        const int headerLength = 12;
        const uint MinBundleFormatVersion = 6;

        var buffer = ArrayPool<byte>.Shared.Rent(headerLength);

        try
        {
            var read = await fileStream.ReadAtLeastAsync(
                buffer.AsMemory(0, headerLength),
                headerLength,
                throwOnEndOfStream: false,
                cancellationToken
            );

            if (read != headerLength || !buffer.AsSpan(0, _bundleSignature.Length).SequenceEqual(_bundleSignature))
            {
                return false;
            }

            var formatVersion = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(_bundleSignature.Length, sizeof(uint)));

            return formatVersion >= MinBundleFormatVersion;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
