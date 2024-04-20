using System.Text.Json;
using AIStudio.Provider;
using Microsoft.JSInterop;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace AIStudio.Settings;

public sealed class SettingsManager
{
    private const string SETTINGS_FILENAME = "settings.json";
    
    public static string? ConfigDirectory { get; set; }

    public static string? DataDirectory { get; set; }

    public Data ConfigurationData { get; private set; } = new();

    private bool IsSetUp => !string.IsNullOrWhiteSpace(ConfigDirectory) && !string.IsNullOrWhiteSpace(DataDirectory);

    #region API Key Handling
    
    private readonly record struct GetSecretRequest(string Destination, string UserName);

    public readonly record struct RequestedSecret(bool Success, string Secret, string Issue);
    
    public async Task<RequestedSecret> GetAPIKey(IJSRuntime jsRuntime, IProvider provider) => await jsRuntime.InvokeAsync<RequestedSecret>("window.__TAURI__.invoke", "get_secret", new GetSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName));

    private readonly record struct StoreSecretRequest(string Destination, string UserName, string Secret);
    
    public readonly record struct StoreSecretResponse(bool Success, string Issue);
    
    public async Task<StoreSecretResponse> SetAPIKey(IJSRuntime jsRuntime, IProvider provider, string key) => await jsRuntime.InvokeAsync<StoreSecretResponse>("window.__TAURI__.invoke", "store_secret", new StoreSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName, key));

    private readonly record struct DeleteSecretRequest(string Destination, string UserName);
    
    public readonly record struct DeleteSecretResponse(bool Success, string Issue);
    
    public async Task<DeleteSecretResponse> DeleteAPIKey(IJSRuntime jsRuntime, IProvider provider) => await jsRuntime.InvokeAsync<DeleteSecretResponse>("window.__TAURI__.invoke", "delete_secret", new DeleteSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName));

    #endregion
    
    public async Task LoadSettings()
    {
        if(!this.IsSetUp)
            return;
        
        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        if(!File.Exists(settingsPath))
            return;
        
        var settingsJson = await File.ReadAllTextAsync(settingsPath);
        var loadedConfiguration = JsonSerializer.Deserialize<Data>(settingsJson);
        if(loadedConfiguration is null)
            return;
        
        this.ConfigurationData = loadedConfiguration;
    }

    public async Task StoreSettings()
    {
        if(!this.IsSetUp)
            return;
        
        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        if(!Directory.Exists(ConfigDirectory))
            Directory.CreateDirectory(ConfigDirectory!);
        
        var settingsJson = JsonSerializer.Serialize(this.ConfigurationData);
        await File.WriteAllTextAsync(settingsPath, settingsJson);
    }
}