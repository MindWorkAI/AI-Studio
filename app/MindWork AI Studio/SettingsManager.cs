using Microsoft.JSInterop;

namespace AIStudio;

public sealed class SettingsManager
{
    public static string? ConfigDirectory { get; set; }

    public static string? DataDirectory { get; set; }
    
    public bool IsSetUp => !string.IsNullOrWhiteSpace(ConfigDirectory) && !string.IsNullOrWhiteSpace(DataDirectory);

    private readonly record struct GetSecretRequest(string Destination, string UserName);
    
    public async Task<string> GetAPIKey(IJSRuntime jsRuntime) => await jsRuntime.InvokeAsync<string>("window.__TAURI__.invoke", "get_secret", new GetSecretRequest("api_key", Environment.UserName));

    private readonly record struct StoreSecretRequest(string Destination, string UserName, string Secret);
    
    public async Task SetAPIKey(IJSRuntime jsRuntime, string key) => await jsRuntime.InvokeVoidAsync("window.__TAURI__.invoke", "store_secret", new StoreSecretRequest("api_key", Environment.UserName, key));
}