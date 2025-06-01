namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Tries to read the enterprise environment for the current user's configuration ID.
    /// </summary>
    /// <returns>
    /// Returns the empty Guid when the environment is not set or the request fails.
    /// Otherwise, the configuration ID.
    /// </returns>
    public async Task<Guid> EnterpriseEnvConfigId()
    {
        var result = await this.http.GetAsync("/system/enterprise/config/id");
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to query the enterprise configuration ID: '{result.StatusCode}'");
            return Guid.Empty;
        }

        Guid.TryParse(await result.Content.ReadAsStringAsync(), out var configurationId);
        return configurationId;
    }
    
    /// <summary>
    /// Tries to read the enterprise environment for a configuration ID, which must be removed.
    /// </summary>
    /// <remarks>
    /// Removing a configuration ID is necessary when the user moved to another department or
    /// left the company, or when the configuration ID is no longer valid.
    /// </remarks>
    /// <returns>
    /// Returns the empty Guid when the environment is not set or the request fails.
    /// Otherwise, the configuration ID.
    /// </returns>
    public async Task<Guid> EnterpriseEnvRemoveConfigId()
    {
        var result = await this.http.DeleteAsync("/system/enterprise/config/id");
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to query the enterprise configuration ID for removal: '{result.StatusCode}'");
            return Guid.Empty;
        }

        Guid.TryParse(await result.Content.ReadAsStringAsync(), out var configurationId);
        return configurationId;
    }

    /// <summary>
    /// Tries to read the enterprise environment for the current user's configuration server URL.
    /// </summary>
    /// <returns>
    /// Returns null when the environment is not set or the request fails.
    /// Otherwise, the configuration server URL.
    /// </returns>
    public async Task<string> EnterpriseEnvConfigServerUrl()
    {
        var result = await this.http.GetAsync("/system/enterprise/config/server");
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to query the enterprise configuration server URL: '{result.StatusCode}'");
            return string.Empty;
        }
        
        var serverUrl = await result.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(serverUrl) ? string.Empty : serverUrl;
    }
}