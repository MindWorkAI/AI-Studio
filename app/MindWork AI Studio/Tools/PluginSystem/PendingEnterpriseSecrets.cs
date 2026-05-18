namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Static container for pending enterprise secrets during plugin loading.
/// </summary>
public static class PendingEnterpriseSecrets
{
    private static readonly List<PendingEnterpriseSecret> PENDING_SECRETS = [];
    private static readonly Lock LOCK = new();

    /// <summary>
    /// Adds a pending enterprise secret to the list.
    /// </summary>
    /// <param name="secret">The pending enterprise secret to add.</param>
    public static void Add(PendingEnterpriseSecret secret)
    {
        lock (LOCK)
            PENDING_SECRETS.Add(secret);
    }

    /// <summary>
    /// Gets and clears all pending enterprise secrets.
    /// </summary>
    /// <returns>A list of all pending enterprise secrets.</returns>
    public static IReadOnlyList<PendingEnterpriseSecret> GetAndClear()
    {
        lock (LOCK)
        {
            var secrets = PENDING_SECRETS.ToList();
            PENDING_SECRETS.Clear();
            return secrets;
        }
    }
}