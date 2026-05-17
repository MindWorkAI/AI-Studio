namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Static container for pending API keys during plugin loading.
/// </summary>
public static class PendingEnterpriseApiKeys
{
    private static readonly List<PendingEnterpriseApiKey> PENDING_KEYS = [];
    private static readonly Lock LOCK = new();

    /// <summary>
    /// Adds a pending API key to the list.
    /// </summary>
    /// <param name="key">The pending API key to add.</param>
    public static void Add(PendingEnterpriseApiKey key)
    {
        lock (LOCK)
            PENDING_KEYS.Add(key);
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