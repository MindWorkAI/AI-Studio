namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Tells whether the OS keyring of this computer holds a secret.
/// </summary>
public interface IStoredSecrets
{
    /// <summary>
    /// Whether the API key of a provider is stored.
    /// </summary>
    /// <param name="secretId">The provider.</param>
    /// <param name="storeType">The kind of provider.</param>
    /// <returns>True when the key is stored.</returns>
    public Task<bool> HasAPIKeyAsync(ISecretId secretId, SecretStoreType storeType);

    /// <summary>
    /// Whether another secret is stored, e.g., the password of a data source.
    /// </summary>
    /// <param name="secretId">What the secret belongs to.</param>
    /// <param name="storeType">The kind of secret.</param>
    /// <returns>True when the secret is stored.</returns>
    public Task<bool> HasSecretAsync(ISecretId secretId, SecretStoreType storeType);
}