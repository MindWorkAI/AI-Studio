using System.Text.Json;
using AIStudio.Provider;
using Microsoft.JSInterop;

namespace AIStudio.Settings;

public sealed class SettingsManager
{
    private const string SETTINGS_FILENAME = "settings.json";
    
    public static string? ConfigDirectory { get; set; }

    public static string? DataDirectory { get; set; }
    
    public bool IsSetUp => !string.IsNullOrWhiteSpace(ConfigDirectory) && !string.IsNullOrWhiteSpace(DataDirectory);

    private readonly record struct GetSecretRequest(string Destination, string UserName);
    
    public async Task<string> GetAPIKey(IJSRuntime jsRuntime, IProvider provider) => await jsRuntime.InvokeAsync<string>("window.__TAURI__.invoke", "get_secret", new GetSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName));

    private readonly record struct StoreSecretRequest(string Destination, string UserName, string Secret);
    
    public async Task SetAPIKey(IJSRuntime jsRuntime, IProvider provider, string key) => await jsRuntime.InvokeVoidAsync("window.__TAURI__.invoke", "store_secret", new StoreSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName, key));

    public async Task<Data> LoadSettings()
    {
        if(!this.IsSetUp)
            return new Data();
        
        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        if(!File.Exists(settingsPath))
            return new Data();
        
        var settingsJson = await File.ReadAllTextAsync(settingsPath);
        return JsonSerializer.Deserialize<Data>(settingsJson) ?? new Data();
    }
    
    public async Task StoreSettings(Data data)
    {
        if(!this.IsSetUp)
            return;
        
        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        var settingsJson = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(settingsPath, settingsJson);
    }
}