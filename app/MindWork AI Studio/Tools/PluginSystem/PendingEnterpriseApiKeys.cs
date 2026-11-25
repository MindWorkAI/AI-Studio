using AIStudio.Tools.Validation;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Static container for pending API keys during plugin loading.
/// </summary>
public static class PendingEnterpriseApiKeys
{
    private static readonly List<PendingEnterpriseApiKey> PENDING_KEYS = [];
    private static readonly Lock LOCK = new();

    /// <summary>
    /// Adds a pending API key to the list, unless it cannot be sent.
    /// </summary>
    /// <remarks>
    /// The whitespace around the key goes, e.g., a line break which came along when somebody copied
    /// the key before encrypting it. A key with any other character which an HTTP header cannot carry
    /// stays out: stored, it would fail with every request, and nobody on the device could change it.
    /// The caller logs that, since it knows which provider of which plugin the key belongs to.
    /// </remarks>
    /// <param name="key">The pending API key to add.</param>
    /// <returns>NONE when the key was added, otherwise why it was left out.</returns>
    public static APIKeyIssue Add(PendingEnterpriseApiKey key)
    {
        var issue = APIKeyValidation.Check(key.ApiKey);
        if (issue.IsProblem)
            return issue;

        lock (LOCK)
            PENDING_KEYS.Add(key with { ApiKey = APIKeyValidation.Normalize(key.ApiKey) });

        return APIKeyIssue.NONE;
    }

    /// <summary>
    /// Gets and clears all pending API keys.
    /// </summary>
    /// <returns>A list of all pending API keys.</returns>
    public static IReadOnlyList<PendingEnterpriseApiKey> GetAndClear()
    {
        lock (LOCK)
        {
            var keys = PENDING_KEYS.ToList();
            PENDING_KEYS.Clear();
            return keys;
        }
    }
}