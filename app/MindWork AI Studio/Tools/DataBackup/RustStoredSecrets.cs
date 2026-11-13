using AIStudio.Tools.Services;

namespace AIStudio.Tools.DataBackup;

/// <summary>
/// Asks the runtime whether the OS keyring holds a secret. It only tries, so a missing secret logs no error.
/// </summary>
/// <param name="rustService">The runtime.</param>
public sealed class RustStoredSecrets(RustService rustService) : IStoredSecrets
{
    /// <inheritdoc />
    public async Task<bool> HasAPIKeyAsync(ISecretId secretId, SecretStoreType storeType) => (await rustService.GetAPIKey(secretId, storeType, isTrying: true)).Success;

    /// <inheritdoc />
    public async Task<bool> HasSecretAsync(ISecretId secretId, SecretStoreType storeType) => (await rustService.GetSecret(secretId, storeType, isTrying: true)).Success;
}