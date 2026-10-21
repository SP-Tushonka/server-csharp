using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SPTarkov.Server.Core.Models.Eft.Profile;

namespace SPTarkov.Server.Core.Services.Profile;

// Physical account deletion spans the account, its indexed children and index.
// A flushed intent is the explicit-delete commit point; recovery finishes it.
// No profile body or wallet is copied into the deletion journal.
public sealed class LocalModeAccountDeletion(string userRoot = "user")
{
    public static SemaphoreSlim MutationGate { get; } = new(1, 1);
    private string Profiles => Path.GetFullPath(Path.Combine(userRoot, "profiles"));
    private string Index => Path.GetFullPath(Path.Combine(userRoot, "local-modes", "index.json"));
    private string Journals => Path.GetFullPath(Path.Combine(userRoot, "local-modes", "account-deletions"));
    public sealed record Entry(string Id, string? Mode, string Sha256);
    public sealed record Intent(int Schema, string Account, string? BeforeIndex, string? AfterIndex, List<Entry> Files);
    public sealed class Plan(Intent intent, string path)
    {
        public Intent Intent { get; } = intent;
        public string Journal { get; } = path;
        public bool Committed { get; internal set; }
        public IEnumerable<string> Ids => Intent.Files.Select(x => x.Id);
    }

    // Index IDs remain authoritative while normal character recreation briefly
    // removes/replaces an in-memory child. Call under MutationGate.
    public string[] IndexedIds(string account)
    {
        ValidId(account);
        var index = ParseIndex(File.Exists(Index) ? File.ReadAllText(Index) : null);
        return (index.GetValueOrDefault(account) ?? []).Values.Prepend(account).ToArray();
    }

    public Plan Prepare(string account, IReadOnlyDictionary<string, SptProfile> loaded)
    {
        ValidId(account);
        if (!loaded.TryGetValue(account, out var root) || !Account(root))
            throw new InvalidOperationException("Only an ordinary account can be deleted");
        var before = File.Exists(Index) ? File.ReadAllText(Index) : null;
        var index = ParseIndex(before);
        var seen = new HashSet<string>();
        foreach (var (owner, modes) in index)
        {
            if (!loaded.TryGetValue(owner, out var ownerProfile) || !Account(ownerProfile)
                || ownerProfile.ProfileInfo?.ProfileId?.ToString() != owner)
                throw new InvalidOperationException("Unknown account in local mode index");
            foreach (var (mode, id) in modes)
            {
                if (!seen.Add(id) || !loaded.TryGetValue(id, out var child)
                    || child.ProfileInfo?.ProfileId?.ToString() != id
                    || child.ProfileInfo.LocalModeOwnerId?.ToString() != owner || child.ProfileInfo.LocalMode != mode)
                    throw new InvalidOperationException("Unowned local mode deletion index");
            }
        }
        var indexedChildren = (index.GetValueOrDefault(account) ?? []).Values.ToHashSet(StringComparer.Ordinal);
        if (loaded.Any(x => x.Value.ProfileInfo?.LocalModeOwnerId?.ToString() == account && !indexedChildren.Contains(x.Key)))
            throw new InvalidOperationException("Unindexed local mode child requires recovery before account deletion");
        var files = new List<Entry> { Snapshot(account, null, account) };
        foreach (var (mode, id) in index.GetValueOrDefault(account) ?? [])
            files.Add(Snapshot(id, mode, account));
        index.Remove(account);
        var after = before is null ? null : JsonSerializer.Serialize(index);
        var intent = new Intent(1, account, before, after, files);
        Validate(intent);
        return new(intent, Path.Combine(Journals, Guid.NewGuid().ToString("N") + ".json"));
    }

    public void Commit(Plan plan)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(plan.Journal)) != Journals || File.Exists(plan.Journal))
            throw new InvalidOperationException("Unknown account deletion plan");
        Validate(plan.Intent);
        ValidateCurrent(plan.Intent);
        Directory.CreateDirectory(Journals);
        try { AtomicWrite(plan.Journal, JsonSerializer.Serialize(plan.Intent)); }
        finally { plan.Committed = File.Exists(plan.Journal); }
        Apply(plan.Intent);
        File.Delete(plan.Journal);
    }

    public int Recover()
    {
        if (!Directory.Exists(Journals)) return 0;
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(Journals, "*.json").Order(StringComparer.Ordinal))
        {
            var intent = JsonSerializer.Deserialize<Intent>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("Invalid account deletion journal");
            Validate(intent);
            Apply(intent);
            File.Delete(path);
            count++;
        }
        return count;
    }

    private Entry Snapshot(string id, string? mode, string account)
    {
        var path = ProfilePath(id);
        var bytes = File.ReadAllBytes(path);
        ValidateBody(bytes, id, mode, account);
        return new(id, mode, Hash(bytes));
    }
    private void Apply(Intent intent)
    {
        // Validate every remaining target and the whole index before any write.
        ValidateCurrent(intent);
        var current = File.Exists(Index) ? File.ReadAllText(Index) : null;
        if (current != intent.AfterIndex && intent.AfterIndex is not null)
            AtomicWrite(Index, intent.AfterIndex);
        foreach (var file in intent.Files)
        {
            var path = ProfilePath(file.Id);
            if (File.Exists(path)) File.Delete(path);
        }
    }
    private void ValidateCurrent(Intent intent)
    {
        var current = File.Exists(Index) ? File.ReadAllText(Index) : null;
        if (current != intent.BeforeIndex && current != intent.AfterIndex)
            throw new InvalidOperationException("Account deletion index changed; recovery required");
        foreach (var entry in intent.Files)
        {
            var path = ProfilePath(entry.Id);
            if (!File.Exists(path)) continue; // A committed earlier attempt deleted it.
            var bytes = File.ReadAllBytes(path);
            if (Hash(bytes) != entry.Sha256)
                throw new InvalidOperationException("Account deletion profile changed; refusing overwrite");
            ValidateBody(bytes, entry.Id, entry.Mode, intent.Account);
        }
    }
    private static void Validate(Intent intent)
    {
        if (intent.Schema != 1) throw new InvalidOperationException("Unsupported account deletion journal");
        ValidId(intent.Account);
        if (intent.Files is null || intent.Files.Count is < 1 or > 3
            || intent.Files.Select(x => x.Id).Distinct().Count() != intent.Files.Count
            || intent.Files[0].Id != intent.Account || intent.Files[0].Mode is not null)
            throw new InvalidOperationException("Invalid account deletion profile set");
        var before = ParseIndex(intent.BeforeIndex);
        var expectedChildren = before.GetValueOrDefault(intent.Account) ?? [];
        if (intent.Files.Count != expectedChildren.Count + 1)
            throw new InvalidOperationException("Account deletion omits an indexed child");
        foreach (var entry in intent.Files)
        {
            ValidId(entry.Id);
            if (!Regex.IsMatch(entry.Sha256, "\\A[0-9a-f]{64}\\z"))
                throw new InvalidOperationException("Invalid account deletion hash");
            if (entry.Id != intent.Account && (entry.Mode is null
                || expectedChildren.GetValueOrDefault(entry.Mode) != entry.Id))
                throw new InvalidOperationException("Account deletion includes an unowned child");
        }
        before.Remove(intent.Account);
        var after = ParseIndex(intent.AfterIndex);
        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after)
            || (intent.BeforeIndex is null) != (intent.AfterIndex is null))
            throw new InvalidOperationException("Account deletion changed unrelated index data");
    }
    private static Dictionary<string, Dictionary<string, string>> ParseIndex(string? text)
    {
        if (text is null) return new();
        // Detect duplicate JSON keys before deserialization could collapse them.
        using var document = JsonDocument.Parse(text);
        var accounts = new HashSet<string>(); var children = new HashSet<string>();
        foreach (var account in document.RootElement.EnumerateObject())
        {
            ValidId(account.Name);
            if (!accounts.Add(account.Name)) throw new InvalidOperationException("Duplicate local account index");
            var modes = new HashSet<string>();
            foreach (var mode in account.Value.EnumerateObject())
            {
                var id = mode.Value.GetString()!; ValidId(id);
                if (mode.Name is not ("regular" or "pvp-season") || !modes.Add(mode.Name)
                    || id == account.Name || !children.Add(id))
                    throw new InvalidOperationException("Invalid or aliased local mode index");
            }
        }
        if (children.Overlaps(accounts)) throw new InvalidOperationException("A child aliases a local account");
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(text)
            ?? throw new InvalidOperationException("Invalid local mode index");
    }
    private static bool Account(SptProfile profile) => profile.ProfileInfo is { LocalModeOwnerId: null, LocalMode: null };
    private static void ValidateBody(byte[] bytes, string id, string? mode, string owner)
    {
        // Canonical profile loading accepts UTF-8 BOM. Preserve raw hashes;
        // skip only that encoding marker for the JSON ownership check.
        var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        using var document = JsonDocument.Parse(bytes.AsMemory(offset));
        var info = document.RootElement.GetProperty("info");
        var bodyOwner = info.TryGetProperty("localModeOwnerId", out var o) ? o.GetString() : null;
        var bodyMode = info.TryGetProperty("localMode", out var m) ? m.GetString() : null;
        if (info.GetProperty("id").GetString() != id || bodyMode != mode
            || bodyOwner != (mode is null ? null : owner))
            throw new InvalidOperationException("Account deletion disk ownership differs");
    }
    private string ProfilePath(string id) { ValidId(id); return Path.Combine(Profiles, id + ".json"); }
    private static void ValidId(string id)
    {
        if (id is null || !Regex.IsMatch(id, "\\A[0-9a-f]{24}\\z"))
            throw new InvalidOperationException("Invalid account deletion identity");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { var bytes = System.Text.Encoding.UTF8.GetBytes(content); output.Write(bytes); output.Flush(true); }
        if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
    }
}
