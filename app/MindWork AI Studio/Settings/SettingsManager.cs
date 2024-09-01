using System.Text.Json;
using System.Text.Json.Serialization;

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