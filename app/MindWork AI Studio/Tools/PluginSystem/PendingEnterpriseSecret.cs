namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a pending enterprise secret that needs to be stored in the OS keyring.
/// </summary>
/// <param name="SecretId">The secret ID.</param>
/// <param name="SecretName">The secret name.</param>
/// <param name="SecretData">The decrypted secret data.</param>
/// <param name="StoreType">The type of secret store to use.</param>
public sealed record PendingEnterpriseSecret(
    string SecretId,
    string SecretName,
    string SecretData,
    SecretStoreType StoreType);