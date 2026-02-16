using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Tries to read the enterprise environment for the configuration encryption secret.
    /// </summary>
    /// <returns>
    /// Returns an empty string when the environment is not set or the request fails.
    /// Otherwise, the base64-encoded encryption secret.
    /// </returns>
    public async Task<string> EnterpriseEnvConfigEncryptionSecret()
    {
        var result = await this.http.GetAsync("/system/enterprise/config/encryption_secret");
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to query the enterprise configuration encryption secret: '{result.StatusCode}'");
            return string.Empty;
        }

        var encryptionSecret = await result.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(encryptionSecret) ? string.Empty : encryptionSecret;
    }

    /// <summary>
    /// Reads all enterprise configurations (multi-config support).
    /// </summary>
    /// <returns>
    /// Returns a list of enterprise environments parsed from the Rust runtime.
    /// The ETag is not yet determined; callers must resolve it separately.
    /// </returns>
    public async Task<List<EnterpriseEnvironment>> EnterpriseEnvConfigs()
    {
        var result = await this.http.GetAsync("/system/enterprise/configs");
        if (!result.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to query the enterprise configurations: '{result.StatusCode}'");
        }

        var configs = await result.Content.ReadFromJsonAsync<List<EnterpriseConfig>>(this.jsonRustSerializerOptions);
        if (configs is null)
            throw new InvalidOperationException("Failed to parse the enterprise configurations from Rust.");

        var environments = new List<EnterpriseEnvironment>();
        foreach (var config in configs)
        {
            if (Guid.TryParse(config.Id, out var id))
                environments.Add(new EnterpriseEnvironment(config.ServerUrl, id, null));
            else
                this.logger!.LogWarning($"Skipping enterprise config with invalid ID: '{config.Id}'.");
        }

        return environments;
    }
}
