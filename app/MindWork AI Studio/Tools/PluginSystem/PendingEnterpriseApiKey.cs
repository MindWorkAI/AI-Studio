namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a pending API key that needs to be stored in the OS keyring.
/// This is used during plugin loading to collect API keys from configuration plugins
/// before storing them asynchronously.
/// </summary>
/// <param name="SecretId">The secret ID (provider ID).</param>
/// <param name="SecretName">The secret name (provider instance name).</param>
/// <param name="ApiKey">The decrypted API key.</param>
/// <param name="StoreType">The type of secret store to use.</param>
public sealed record PendingEnterpriseApiKey(
    string SecretId,
    string SecretName,
    string ApiKey,
    SecretStoreType StoreType);

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
