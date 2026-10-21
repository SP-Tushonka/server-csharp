using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Runtime.LocalModes;

// The account identity remains the launcher/PHPSESSID identity. A character
// identity is supplied only to gameplay callbacks, never to Set-Cookie.
public sealed record LocalModeBinding(string AccountId, string ProfileId, string RequestedMode, long Generation)
{
    public string RaidMode => "pve";
    public string CookieId => AccountId;
}

public interface ILocalModeProfileStore
{
    // Must reject a child masquerading as an account. Never create on a read.
    bool IsAccount(string accountId);
    // pve resolves to the unmodified existing profile; other modes resolve to
    // separately persisted profiles created through normal edition templates.
    string? FindProfile(string accountId, string mode);
    bool OwnsProfile(string accountId, string mode, string profileId);
}

public sealed class LocalModeSessions(ILocalModeProfileStore store)
{
    public static readonly string[] SupportedModes = ["pve", "regular", "pvp-season"];
    private readonly ConcurrentDictionary<string, LocalModeBinding> current = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object> gates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> raids = new(StringComparer.Ordinal);

    public static bool IsSupported(string? mode) => mode is "pve" or "regular" or "pvp-season";

    public LocalModeBinding Capture(string accountId)
    {
        ValidateAccount(accountId);
        lock (Gate(accountId))
            return current.GetOrAdd(accountId, id => Create(id, "pve", 0));
    }

    // Call only after the target profile's creation/save transaction succeeds.
    // A request for an unknown or uncreated mode never changes the active mode.
    public LocalModeBinding Select(string accountId, string mode)
    {
        ValidateAccount(accountId);
        if (!IsSupported(mode)) throw new ArgumentException("Unsupported local game mode", nameof(mode));
        lock (Gate(accountId))
        {
            var old = Capture(accountId);
            if (old.RequestedMode == mode) return old;
            if (raids.GetValueOrDefault(accountId) != 0)
                throw new InvalidOperationException("Cannot change character mode during a local raid");
            var next = Create(accountId, mode, checked(old.Generation + 1));
            current[accountId] = next;
            return next;
        }
    }

    // A socket captures this binding at connection. Reject an old socket after
    // selection changes; do not silently route its queued mutations to another
    // character. HTTP callbacks similarly keep their immutable captured binding.
    public bool IsCurrent(LocalModeBinding binding)
    {
        lock (Gate(binding.AccountId))
            return current.TryGetValue(binding.AccountId, out var value) && value == binding;
    }

    public IDisposable EnterRaid(LocalModeBinding binding)
    {
        lock (Gate(binding.AccountId))
        {
            if (!IsCurrent(binding)) throw new InvalidOperationException("Stale local game mode session");
            if (raids.GetValueOrDefault(binding.AccountId) != 0)
                throw new InvalidOperationException("An account already has an active local raid");
            raids[binding.AccountId] = 1;
            return new RaidLease(this, binding.AccountId);
        }
    }

    private LocalModeBinding Create(string accountId, string mode, long generation)
    {
        var profile = store.FindProfile(accountId, mode)
            ?? throw new InvalidOperationException("The local mode character has not been created");
        if (!ValidId(profile) || !store.OwnsProfile(accountId, mode, profile))
            throw new InvalidOperationException("Local mode profile ownership mismatch");
        if ((mode == "pve") != (accountId == profile))
            throw new InvalidOperationException("Local mode character aliases the existing PvE profile");
        // Distinct modes must never share a character, even through corrupt data.
        foreach (var other in SupportedModes.Where(x => x != mode))
            if (store.FindProfile(accountId, other) == profile)
                throw new InvalidOperationException("Two local modes alias the same character");
        return new(accountId, profile, mode, generation);
    }

    private static bool ValidId(string id) => Regex.IsMatch(id, "\\A[0-9a-f]{24}\\z", RegexOptions.CultureInvariant);
    private void ValidateAccount(string id)
    {
        if (!ValidId(id) || !store.IsAccount(id)) throw new ArgumentException("Unknown local account", nameof(id));
    }
    private object Gate(string id) => gates.GetOrAdd(id, _ => new object());

    private sealed class RaidLease(LocalModeSessions owner, string account) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lock (owner.Gate(account)) owner.raids.TryRemove(account, out _);
        }
    }
}
