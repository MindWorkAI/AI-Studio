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