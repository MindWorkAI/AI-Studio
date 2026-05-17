using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    private static string SecretKey(ISecretId secretId, SecretStoreType storeType) => $"{storeType.Prefix()}::{secretId.SecretId}::{secretId.SecretName}";

    private static string LegacySecretKey(ISecretId secretId) => $"secret::{secretId.SecretId}::{secretId.SecretName}";

    /// <summary>
    /// Try to get the secret data for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to get the data for.</param>
    /// <param name="storeType">The secret store type.</param>
    /// <param name="isTrying">Indicates if we are trying to get the data. In that case, we don't log errors.</param>
    /// <returns>The requested secret.</returns>
    public async Task<RequestedSecret> GetSecret(ISecretId secretId, SecretStoreType storeType, bool isTrying = false)
    {
        var secretKey = SecretKey(secretId, storeType);
        var secretRequest = new SelectSecretRequest(secretKey, Environment.UserName, isTrying);
        var result = await this.http.PostAsJsonAsync("/secrets/get", secretRequest, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            if(!isTrying)
                this.logger!.LogError($"Failed to get the secret data for '{secretKey}' due to an API issue: '{result.StatusCode}'");
            return new RequestedSecret(false, new EncryptedText(string.Empty), TB("Failed to get the secret data due to an API issue."));
        }
        
        var secret = await result.Content.ReadFromJsonAsync<RequestedSecret>(this.jsonRustSerializerOptions);
        if (!secret.Success && storeType is SecretStoreType.DATA_SOURCE)
        {
            var legacySecret = await this.GetLegacySecret(secretId, isTrying: true);
            if (legacySecret.Success)
            {
                this.logger!.LogDebug($"Successfully retrieved the legacy data source secret for '{LegacySecretKey(secretId)}'.");
                return legacySecret;
            }
        }

        if (!secret.Success && !isTrying)
            this.logger!.LogError($"Failed to get the secret data for '{secretKey}': '{secret.Issue}'");
        
        return secret;
    }
    
    /// <summary>
    /// Try to store the secret data for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to store the data for.</param>
    /// <param name="secretData">The data to store.</param>
    /// <param name="storeType">The secret store type.</param>
    /// <returns>The store secret response.</returns>
    public async Task<StoreSecretResponse> SetSecret(ISecretId secretId, string secretData, SecretStoreType storeType)
    {
        var secretKey = SecretKey(secretId, storeType);
        var encryptedSecret = await this.encryptor!.Encrypt(secretData);
        var request = new StoreSecretRequest(secretKey, Environment.UserName, encryptedSecret);
        var result = await this.http.PostAsJsonAsync("/secrets/store", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to store the secret data for '{secretKey}' due to an API issue: '{result.StatusCode}'");
            return new StoreSecretResponse(false, TB("Failed to store the secret data due to an API issue."));
        }
        
        var state = await result.Content.ReadFromJsonAsync<StoreSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to store the secret data for '{secretKey}': '{state.Issue}'");

        if (state.Success && storeType is SecretStoreType.DATA_SOURCE)
            await this.DeleteSecretByKey(LegacySecretKey(secretId));
        
        return state;
    }
    
    /// <summary>
    /// Tries to delete the secret data for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to delete the data for.</param>
    /// <param name="storeType">The secret store type.</param>
    /// <returns>The delete secret response.</returns>
    public async Task<DeleteSecretResponse> DeleteSecret(ISecretId secretId, SecretStoreType storeType)
    {
        var deleteResult = await this.DeleteSecretByKey(SecretKey(secretId, storeType));
        if (storeType is not SecretStoreType.DATA_SOURCE || !deleteResult.Success)
            return deleteResult;

        var legacyDeleteResult = await this.DeleteSecretByKey(LegacySecretKey(secretId));
        if (!legacyDeleteResult.Success)
            return legacyDeleteResult;

        return deleteResult with { WasEntryFound = deleteResult.WasEntryFound || legacyDeleteResult.WasEntryFound };
    }

    private async Task<RequestedSecret> GetLegacySecret(ISecretId secretId, bool isTrying)
    {
        var secretKey = LegacySecretKey(secretId);
        var secretRequest = new SelectSecretRequest(secretKey, Environment.UserName, isTrying);
        var result = await this.http.PostAsJsonAsync("/secrets/get", secretRequest, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            if(!isTrying)
                this.logger!.LogError($"Failed to get the secret data for '{secretKey}' due to an API issue: '{result.StatusCode}'");
            return new RequestedSecret(false, new EncryptedText(string.Empty), TB("Failed to get the secret data due to an API issue."));
        }

        return await result.Content.ReadFromJsonAsync<RequestedSecret>(this.jsonRustSerializerOptions);
    }

    private async Task<DeleteSecretResponse> DeleteSecretByKey(string secretKey)
    {
        var request = new SelectSecretRequest(secretKey, Environment.UserName, false);
        var result = await this.http.PostAsJsonAsync("/secrets/delete", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to delete the secret data for '{secretKey}' due to an API issue: '{result.StatusCode}'");
            return new DeleteSecretResponse{Success = false, WasEntryFound = false, Issue = TB("Failed to delete the secret data due to an API issue.")};
        }
        
        var state = await result.Content.ReadFromJsonAsync<DeleteSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to delete the secret data for '{secretKey}': '{state.Issue}'");
        
        return state;
    }
}