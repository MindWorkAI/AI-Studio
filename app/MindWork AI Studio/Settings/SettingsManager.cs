using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace AIStudio.Settings;

/// <summary>
/// The settings manager.
/// </summary>
public sealed class SettingsManager(ILogger<SettingsManager> logger)
{
    private const string SETTINGS_FILENAME = "settings.json";
    
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private ILogger<SettingsManager> logger = logger;
    
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
    /// <param name="Success">True, when the secret was successfully deleted or not found.</param>
    /// <param name="Issue">The issue, when the secret could not be deleted.</param>
    /// <param name="WasEntryFound">True, when the entry was found and deleted.</param>
    public readonly record struct DeleteSecretResponse(bool Success, string Issue, bool WasEntryFound);
    
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
        {
            this.logger.LogWarning("Cannot load settings, because the configuration is not set up yet.");
            return;
        }

        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        if(!File.Exists(settingsPath))
        {
            this.logger.LogWarning("Cannot load settings, because the settings file does not exist.");
            return;
        }

        // We read the `"Version": "V3"` line to determine the version of the settings file:
        await foreach (var line in File.ReadLinesAsync(settingsPath))
        {
            if (!line.Contains("""
                               "Version":
                               """))
                continue;

            // Extract the version from the line:
            var settingsVersionText = line.Split('"')[3];
                
            // Parse the version:
            Enum.TryParse(settingsVersionText, out Version settingsVersion);
            if(settingsVersion is Version.UNKNOWN)
            {
                this.logger.LogError("Unknown version of the settings file found.");
                this.ConfigurationData = new();
                return;
            }
                
            this.ConfigurationData = SettingsMigrations.Migrate(this.logger, settingsVersion, await File.ReadAllTextAsync(settingsPath), JSON_OPTIONS);
            return;
        }
        
        this.logger.LogError("Failed to read the version of the settings file.");
        this.ConfigurationData = new();
    }

    /// <summary>
    /// Stores the settings to the file system.
    /// </summary>
    public async Task StoreSettings()
    {
        if(!this.IsSetUp)
        {
            this.logger.LogWarning("Cannot store settings, because the configuration is not set up yet.");
            return;
        }

        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        if(!Directory.Exists(ConfigDirectory))
        {
            this.logger.LogInformation("Creating the configuration directory.");
            Directory.CreateDirectory(ConfigDirectory!);
        }

        var settingsJson = JsonSerializer.Serialize(this.ConfigurationData, JSON_OPTIONS);
        await File.WriteAllTextAsync(settingsPath, settingsJson);
        
        this.logger.LogInformation("Stored the settings to the file system.");
    }
    
    public void InjectSpellchecking(Dictionary<string, object?> attributes) => attributes["spellcheck"] = this.ConfigurationData.App.EnableSpellchecking ? "true" : "false";
}