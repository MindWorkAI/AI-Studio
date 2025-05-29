using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    private static string TB_Secrets(string fallbackEN) => I18N.I.T(fallbackEN, typeof(RustService).Namespace, $"{nameof(RustService)}.Secrets");
    
    /// <summary>
    /// Try to get the secret data for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to get the data for.</param>
    /// <param name="isTrying">Indicates if we are trying to get the data. In that case, we don't log errors.</param>
    /// <returns>The requested secret.</returns>
    public async Task<RequestedSecret> GetSecret(ISecretId secretId, bool isTrying = false)
    {
        static string TB(string fallbackEN) => TB_Secrets(fallbackEN);
        
        var secretRequest = new SelectSecretRequest($"secret::{secretId.SecretId}::{secretId.SecretName}", Environment.UserName, isTrying);
        var result = await this.http.PostAsJsonAsync("/secrets/get", secretRequest, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            if(!isTrying)
                this.logger!.LogError($"Failed to get the secret data for secret ID '{secretId.SecretId}' due to an API issue: '{result.StatusCode}'");
            return new RequestedSecret(false, new EncryptedText(string.Empty), TB("Failed to get the secret data due to an API issue."));
        }
        
        var secret = await result.Content.ReadFromJsonAsync<RequestedSecret>(this.jsonRustSerializerOptions);
        if (!secret.Success && !isTrying)
            this.logger!.LogError($"Failed to get the secret data for secret ID '{secretId.SecretId}': '{secret.Issue}'");
        
        return secret;
    }
    
    /// <summary>
    /// Try to store the secret data for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to store the data for.</param>
    /// <param name="secretData">The data to store.</param>
    /// <returns>The store secret response.</returns>
    public async Task<StoreSecretResponse> SetSecret(ISecretId secretId, string secretData)
    {
        static string TB(string fallbackEN) => TB_Secrets(fallbackEN);
        
        var encryptedSecret = await this.encryptor!.Encrypt(secretData);
        var request = new StoreSecretRequest($"secret::{secretId.SecretId}::{secretId.SecretName}", Environment.UserName, encryptedSecret);
        var result = await this.http.PostAsJsonAsync("/secrets/store", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to store the secret data for secret ID '{secretId.SecretId}' due to an API issue: '{result.StatusCode}'");
            return new StoreSecretResponse(false, TB("Failed to get the secret data due to an API issue."));
        }
        
        var state = await result.Content.ReadFromJsonAsync<StoreSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to store the secret data for secret ID '{secretId.SecretId}': '{state.Issue}'");
        
        return state;
    }
    
    /// <summary>
    /// Tries to delete the secret data for the given secret ID.
    /// </summary>
    /// <param name="secretId">The secret ID to delete the data for.</param>
    /// <returns>The delete secret response.</returns>
    public async Task<DeleteSecretResponse> DeleteSecret(ISecretId secretId)
    {
        static string TB(string fallbackEN) => TB_Secrets(fallbackEN);
        
        var request = new SelectSecretRequest($"secret::{secretId.SecretId}::{secretId.SecretName}", Environment.UserName, false);
        var result = await this.http.PostAsJsonAsync("/secrets/delete", request, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to delete the secret data for secret ID '{secretId.SecretId}' due to an API issue: '{result.StatusCode}'");
            return new DeleteSecretResponse{Success = false, WasEntryFound = false, Issue = TB("Failed to delete the secret data due to an API issue.")};
        }
        
        var state = await result.Content.ReadFromJsonAsync<DeleteSecretResponse>(this.jsonRustSerializerOptions);
        if (!state.Success)
            this.logger!.LogError($"Failed to delete the secret data for secret ID '{secretId.SecretId}': '{state.Issue}'");
        
        return state;
    }
}