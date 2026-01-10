using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Try to get the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to get the API key for.</param>
    /// <param name="isTrying">Indicates if we are trying to get the API key. In that case, we don't log errors.</param>
    /// <returns>The requested secret.</returns>
    public async Task<RequestedSecret> GetAPIKey(ISecretId secretId, bool isTrying = false)
    {
        var secretRequest = new SelectSecretRequest($"provider::{secretId.SecretId}::{secretId.SecretName}::api_key", Environment.UserName, isTrying);
        var result = await this.http.PostAsJsonAsync("/secrets/get", secretRequest, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            if(!isTrying)
                this.logger!.LogError($"Failed to get the API key for 'provider::{secretId.SecretId}::{secretId.SecretName}::api_key' due to an API issue: '{result.StatusCode}'");
            return new RequestedSecret(false, new EncryptedText(string.Empty), TB("Failed to get the API key due to an API issue."));
        }
        
        var secret = await result.Content.ReadFromJsonAsync<RequestedSecret>(this.jsonRustSerializerOptions);
        if (!secret.Success && !isTrying)
            this.logger!.LogError($"Failed to get the API key for 'provider::{secretId.SecretId}::{secretId.SecretName}::api_key': '{secret.Issue}'");
        
        this.logger!.LogDebug($"Successfully retrieved the API key for 'provider::{secretId.SecretId}::{secretId.SecretName}::api_key'.");
        return secret;
    }
    
    /// <summary>
    /// Try to store the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to store the API key for.</param>
    /// <param name="key">The API key to store.</param>
    /// <returns>The store secret response.</returns>
    public async Task<StoreSecretResponse> SetAPIKey(ISecretId secretId, string key)
    {
        var encryptedKey = await this.encryptor!.Encrypt(key);
        var request = new StoreSecretRequest($"provider::{secretId.SecretId}::{secretId.SecretName}::api_key", Environment.UserName, encryptedKey);
        var result = await this.http.PostAsJsonAsync("/secrets/store", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to store the API key for 'provider::{secretId.SecretId}::{secretId.SecretName}::api_key' due to an API issue: '{result.StatusCode}'");
            return new StoreSecretResponse(false, TB("Failed to get the API key due to an API issue."));
        }
        
        var state = await result.Content.ReadFromJsonAsync<StoreSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to store the API key for 'provider::{secretId.SecretId}::{secretId.SecretName}::api_key': '{state.Issue}'");
        
        this.logger!.LogDebug($"Successfully stored the API key for 'provider::{secretId.SecretId}::{secretId.SecretName}::api_key'.");
        return state;
    }
    
    /// <summary>
    /// Tries to delete the API key for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to delete the API key for.</param>
    /// <returns>The delete secret response.</returns>
    public async Task<DeleteSecretResponse> DeleteAPIKey(ISecretId secretId)
    {
        var request = new SelectSecretRequest($"provider::{secretId.SecretId}::{secretId.SecretName}::api_key", Environment.UserName, false);
        var result = await this.http.PostAsJsonAsync("/secrets/delete", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to delete the API key for secret ID '{secretId.SecretId}' due to an API issue: '{result.StatusCode}'");
            return new DeleteSecretResponse{Success = false, WasEntryFound = false, Issue = TB("Failed to delete the API key due to an API issue.")};
        }
        
        var state = await result.Content.ReadFromJsonAsync<DeleteSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to delete the API key for secret ID '{secretId.SecretId}': '{state.Issue}'");
        
        return state;
    }
}