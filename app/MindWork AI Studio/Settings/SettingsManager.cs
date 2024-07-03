using System.Text.Json;
using AIStudio.Provider;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace AIStudio.Settings;

/// <summary>
/// The settings manager.
/// </summary>
public sealed class SettingsManager
{
    private const string SETTINGS_FILENAME = "settings.json";
    
    /// <summary>
    /// The directory where the configuration files are stored.
    /// </summary>
    public static string? ConfigDirectory { get; set; }

    /// <summary>
    /// The directory where the data files are stored.
    /// </summary>
    public static string? DataDirectory { get; set; }

    /// <summary>
    /// The configuration data.
    /// </summary>
    public Data ConfigurationData { get; private set; } = new();

    private bool IsSetUp => !string.IsNullOrWhiteSpace(ConfigDirectory) && !string.IsNullOrWhiteSpace(DataDirectory);

    #region API Key Handling
    
    private readonly record struct GetSecretRequest(string Destination, string UserName);

    /// <summary>
    /// Data structure for any requested secret.
    /// </summary>
    /// <param name="Success">True, when the secret was successfully retrieved.</param>
    /// <param name="Secret">The secret, e.g., API key.</param>
    /// <param name="Issue">The issue, when the secret could not be retrieved.</param>
    public readonly record struct RequestedSecret(bool Success, string Secret, string Issue);
    
    /// <summary>
    /// Try to get the API key for the given provider.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="provider">The provider to get the API key for.</param>
    /// <returns>The requested secret.</returns>
    public async Task<RequestedSecret> GetAPIKey(IJSRuntime jsRuntime, IProvider provider) => await jsRuntime.InvokeAsync<RequestedSecret>("window.__TAURI__.invoke", "get_secret", new GetSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName));

    private readonly record struct StoreSecretRequest(string Destination, string UserName, string Secret);
    
    /// <summary>
    /// Data structure for storing a secret response.
    /// </summary>
    /// <param name="Success">True, when the secret was successfully stored.</param>
    /// <param name="Issue">The issue, when the secret could not be stored.</param>
    public readonly record struct StoreSecretResponse(bool Success, string Issue);
    
    /// <summary>
    /// Try to store the API key for the given provider.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="provider">The provider to store the API key for.</param>
    /// <param name="key">The API key to store.</param>
    /// <returns>The store secret response.</returns>
    public async Task<StoreSecretResponse> SetAPIKey(IJSRuntime jsRuntime, IProvider provider, string key) => await jsRuntime.InvokeAsync<StoreSecretResponse>("window.__TAURI__.invoke", "store_secret", new StoreSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName, key));

    private readonly record struct DeleteSecretRequest(string Destination, string UserName);
    
    /// <summary>
    /// Data structure for deleting a secret response.
    /// </summary>
    /// <param name="Success">True, when the secret was successfully deleted.</param>
    /// <param name="Issue">The issue, when the secret could not be deleted.</param>
    public readonly record struct DeleteSecretResponse(bool Success, string Issue);
    
    /// <summary>
    /// Tries to delete the API key for the given provider.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to access the Rust code.</param>
    /// <param name="provider">The provider to delete the API key for.</param>
    /// <returns>The delete secret response.</returns>
    public async Task<DeleteSecretResponse> DeleteAPIKey(IJSRuntime jsRuntime, IProvider provider) => await jsRuntime.InvokeAsync<DeleteSecretResponse>("window.__TAURI__.invoke", "delete_secret", new DeleteSecretRequest($"provider::{provider.Id}::{provider.InstanceName}::api_key", Environment.UserName));

    #endregion
    
    /// <summary>
    /// Loads the settings from the file system.
    /// </summary>
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
        
        this.ConfigurationData = SettingsMigrations.Migrate(loadedConfiguration);
    }

    /// <summary>
    /// Stores the settings to the file system.
    /// </summary>
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
    
    public void InjectSpellchecking(Dictionary<string, object?> attributes) => attributes["spellcheck"] = this.ConfigurationData.EnableSpellchecking ? "true" : "false";
}