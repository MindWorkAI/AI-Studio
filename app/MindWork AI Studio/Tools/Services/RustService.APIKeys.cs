using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    private const string SELF_HOSTED_SECRET_ID = "Self-hosted";

    // Temporary compatibility shim until 2026-12-19:
    // documentation/compatibility-shims/2026-06-self-hosted-secret-id.md
    private const string LEGACY_SELF_HOSTED_SECRET_ID_DE = "Selbst gehostet";

    private static string APIKey(SecretStoreType storeType, ISecretId secretId) => $"{storeType.Prefix()}::{secretId.SecretId}::{secretId.SecretName}::api_key";

    private static IEnumerable<string> LegacySelfHostedAPIKeys(ISecretId secretId, SecretStoreType storeType)
    {
        if (secretId.SecretId == SELF_HOSTED_SECRET_ID)
            yield return $"{storeType.Prefix()}::{LEGACY_SELF_HOSTED_SECRET_ID_DE}::{secretId.SecretName}::api_key";

        if (secretId.SecretId == $"{ISecretId.ENTERPRISE_KEY_PREFIX}::{SELF_HOSTED_SECRET_ID}")
            yield return $"{storeType.Prefix()}::{ISecretId.ENTERPRISE_KEY_PREFIX}::{LEGACY_SELF_HOSTED_SECRET_ID_DE}::{secretId.SecretName}::api_key";
    }

    /// <summary>
    /// Try to get the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to get the API key for.</param>
    /// <param name="isTrying">Indicates if we are trying to get the API key. In that case, we don't log errors.</param>
    /// <param name="storeType">The secret store type. Defaults to LLM_PROVIDER for backward compatibility.</param>
    /// <returns>The requested secret.</returns>
    public async Task<RequestedSecret> GetAPIKey(ISecretId secretId, SecretStoreType storeType, bool isTrying = false)
    {
        var secretKey = APIKey(storeType, secretId);
        var legacySecretKeys = LegacySelfHostedAPIKeys(secretId, storeType).ToList();
        var secret = await this.GetAPIKeyByKey(secretKey, isTrying || legacySecretKeys.Count > 0);
        if (secret.Success)
        {
            foreach (var legacySecretKey in legacySecretKeys)
                await this.DeleteAPIKeyByKey(legacySecretKey, isTrying: true);

            return secret;
        }

        foreach (var legacySecretKey in legacySecretKeys)
        {
            var legacySecret = await this.GetAPIKeyByKey(legacySecretKey, isTrying: true);
            if (!legacySecret.Success)
                continue;

            this.logger!.LogInformation($"Migrating legacy self-hosted API key namespace '{legacySecretKey}' to '{secretKey}'.");
            var migrationResult = await this.StoreEncryptedAPIKeyByKey(secretKey, legacySecret.Secret);
            if (migrationResult.Success)
                await this.DeleteAPIKeyByKey(legacySecretKey, isTrying: true);
            else
                this.logger!.LogWarning($"Failed to migrate legacy self-hosted API key namespace '{legacySecretKey}' to '{secretKey}': '{migrationResult.Issue}'");

            return legacySecret;
        }

        return secret;
    }

    private async Task<RequestedSecret> GetAPIKeyByKey(string secretKey, bool isTrying)
    {
        var secretRequest = new SelectSecretRequest(secretKey, Environment.UserName, isTrying);
        var result = await this.http.PostAsJsonAsync("/secrets/get", secretRequest, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            if(!isTrying)
                this.logger!.LogError($"Failed to get the API key for '{secretKey}' due to an API issue: '{result.StatusCode}'");
            return new RequestedSecret(false, new EncryptedText(string.Empty), TB("Failed to get the API key due to an API issue."));
        }

        var secret = await result.Content.ReadFromJsonAsync<RequestedSecret>(this.jsonRustSerializerOptions);
        if (secret.Success)
            this.logger!.LogDebug($"Successfully retrieved the API key for '{secretKey}'.");
        else if (isTrying)
            this.logger!.LogDebug($"No API key configured for '{secretKey}' (try mode): '{secret.Issue}'");
        else
            this.logger!.LogError($"Failed to get the API key for '{secretKey}': '{secret.Issue}'");

        return TranslateSecretStoreIssue(secret);
    }

    /// <summary>
    /// Try to store the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to store the API key for.</param>
    /// <param name="key">The API key to store.</param>
    /// <param name="storeType">The secret store type. Defaults to LLM_PROVIDER for backward compatibility.</param>
    /// <returns>The store secret response.</returns>
    public async Task<StoreSecretResponse> SetAPIKey(ISecretId secretId, string key, SecretStoreType storeType)
    {
        var encryptedKey = await this.encryptor!.Encrypt(key);
        var secretKey = APIKey(storeType, secretId);
        var state = await this.StoreEncryptedAPIKeyByKey(secretKey, encryptedKey);
        if (state.Success)
        {
            foreach (var legacySecretKey in LegacySelfHostedAPIKeys(secretId, storeType))
                await this.DeleteAPIKeyByKey(legacySecretKey, isTrying: true);
        }

        return TranslateSecretStoreIssue(state);
    }

    private async Task<StoreSecretResponse> StoreEncryptedAPIKeyByKey(string secretKey, EncryptedText encryptedKey)
    {
        var request = new StoreSecretRequest(secretKey, Environment.UserName, encryptedKey);
        var result = await this.http.PostAsJsonAsync("/secrets/store", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to store the API key for '{secretKey}' due to an API issue: '{result.StatusCode}'");
            return new StoreSecretResponse(false, TB("Failed to store the API key due to an API issue."));
        }

        var state = await result.Content.ReadFromJsonAsync<StoreSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to store the API key for '{secretKey}': '{state.Issue}'");
        else
            this.logger!.LogDebug($"Successfully stored the API key for '{secretKey}'.");

        return state;
    }

    /// <summary>
    /// Tries to delete the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to delete the API key for.</param>
    /// <param name="storeType">The secret store type. Defaults to LLM_PROVIDER for backward compatibility.</param>
    /// <returns>The delete secret response.</returns>
    public async Task<DeleteSecretResponse> DeleteAPIKey(ISecretId secretId, SecretStoreType storeType)
    {
        var deleteResult = await this.DeleteAPIKeyByKey(APIKey(storeType, secretId));
        if (!deleteResult.Success)
            return deleteResult;

        foreach (var legacySecretKey in LegacySelfHostedAPIKeys(secretId, storeType))
        {
            var legacyDeleteResult = await this.DeleteAPIKeyByKey(legacySecretKey, isTrying: true);
            if (!legacyDeleteResult.Success)
                return legacyDeleteResult;

            deleteResult = deleteResult with { WasEntryFound = deleteResult.WasEntryFound || legacyDeleteResult.WasEntryFound };
        }

        return deleteResult;
    }

    private async Task<DeleteSecretResponse> DeleteAPIKeyByKey(string secretKey, bool isTrying = false)
    {
        var request = new SelectSecretRequest(secretKey, Environment.UserName, false);
        var result = await this.http.PostAsJsonAsync("/secrets/delete", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to delete the API key for '{secretKey}' due to an API issue: '{result.StatusCode}'");
            return new DeleteSecretResponse{Success = false, WasEntryFound = false, Issue = TB("Failed to delete the API key due to an API issue.")};
        }

        var state = await result.Content.ReadFromJsonAsync<DeleteSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success && !isTrying)
            this.logger!.LogError($"Failed to delete the API key for '{secretKey}': '{state.Issue}'");

        return TranslateSecretStoreIssue(state);
    }
}