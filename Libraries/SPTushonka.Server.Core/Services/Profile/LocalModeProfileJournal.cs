using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace SPTarkov.Server.Core.Services.Profile;

// Durable intent is published before any account/character purchase mutation.
// Startup rolls an unfinished operation back, or a committed operation forward.
// Each individual profile replacement is atomic; the journal spans both files.
public sealed class LocalModeProfileJournal
{
    private readonly string profiles;
    private readonly string transactions;
    public LocalModeProfileJournal(string userRoot = "user")
    {
        profiles = Path.GetFullPath(Path.Combine(userRoot, "profiles"));
        transactions = Path.GetFullPath(Path.Combine(userRoot, "local-modes", "transactions"));
    }

    public sealed record Entry(string Id, string Json, string Sha256);
    public sealed record Intent(int Schema, string State, List<Entry> Before, List<Entry>? After);
    public sealed record Handle(string Path, Intent Intent);

    public Handle Begin(IReadOnlyDictionary<string, string> before)
    {
        var entries = Entries(before);
        Directory.CreateDirectory(transactions);
        var path = Path.Combine(transactions, Guid.NewGuid().ToString("N") + ".json");
        var intent = new Intent(1, "started", entries, null);
        AtomicWrite(path, JsonSerializer.Serialize(intent));
        return new(path, intent);
    }

    public void Commit(Handle handle, IReadOnlyDictionary<string, string> after)
    {
        ValidateHandle(handle);
        var entries = Entries(after);
        if (!entries.Select(x => x.Id).SequenceEqual(handle.Intent.Before.Select(x => x.Id)))
            throw new InvalidOperationException("Local transaction changed its profile set");
        var committed = handle.Intent with { State = "committed", After = entries };
        AtomicWrite(handle.Path, JsonSerializer.Serialize(committed));
        Apply(entries);
        File.Delete(handle.Path);
    }

    public IReadOnlyDictionary<string, string> Rollback(Handle handle)
    {
        ValidateHandle(handle);
        // If a commit was published, preserve it and complete it. Never undo a
        // successful debit while leaving its delivery on another profile.
        var persisted = Read(handle.Path);
        var desired = persisted.State == "committed" ? persisted.After! : persisted.Before;
        Apply(desired);
        File.Delete(handle.Path);
        return desired.ToDictionary(x => x.Id, x => x.Json);
    }

    public int Recover()
    {
        if (!Directory.Exists(transactions)) return 0;
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(transactions, "*.json").Order(StringComparer.Ordinal))
        {
            var intent = Read(file);
            Apply(intent.State == "committed" ? intent.After! : intent.Before);
            File.Delete(file);
            count++;
        }
        return count;
    }

    private Intent Read(string path)
    {
        var intent = JsonSerializer.Deserialize<Intent>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Invalid local transaction journal");
        if (intent.Schema != 1 || intent.State is not ("started" or "committed"))
            throw new InvalidOperationException("Unsupported local transaction journal");
        ValidateEntries(intent.Before);
        if (intent.State == "committed")
        {
            ValidateEntries(intent.After ?? throw new InvalidOperationException("Missing committed profiles"));
            if (!intent.After!.Select(x => x.Id).SequenceEqual(intent.Before.Select(x => x.Id)))
                throw new InvalidOperationException("Journal profile set mismatch");
        }
        return intent;
    }

    private static List<Entry> Entries(IReadOnlyDictionary<string, string> values)
    {
        var entries = values.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new Entry(x.Key, x.Value, Hash(x.Value))).ToList();
        ValidateEntries(entries);
        return entries;
    }

    private static void ValidateEntries(List<Entry> entries)
    {
        if (entries.Count is < 1 or > 2 || entries.Select(x => x.Id).Distinct().Count() != entries.Count)
            throw new InvalidOperationException("Invalid local transaction profile set");
        foreach (var entry in entries)
        {
            if (!Regex.IsMatch(entry.Id, "\\A[0-9a-f]{24}\\z") || Hash(entry.Json) != entry.Sha256)
                throw new InvalidOperationException("Invalid local transaction profile identity/hash");
            using var document = JsonDocument.Parse(entry.Json);
            if (document.RootElement.GetProperty("info").GetProperty("id").GetString() != entry.Id)
                throw new InvalidOperationException("Local transaction profile body identity differs");
        }
    }

    private void Apply(List<Entry> entries)
    {
        ValidateEntries(entries);
        foreach (var entry in entries) AtomicWrite(Path.Combine(profiles, entry.Id + ".json"), entry.Json);
    }

    private void ValidateHandle(Handle handle)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(handle.Path)) != transactions || !File.Exists(handle.Path))
            throw new InvalidOperationException("Unknown local transaction handle");
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            output.Write(bytes);
            output.Flush(true);
        }
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }
}
