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

    private readonly ILogger<SettingsManager> logger = logger;
    
    /// <summary>
    /// The directory where the configuration files are stored.
    /// </summary>
    public static string? ConfigDirectory { get; set; }

    /// <summary>
    /// The directory where the data files are stored.
    /// </summary>
    public static string? DataDirectory { get; set; }

    /// <summary>
    /// Whether the app is in dark mode.
    /// </summary>
    public bool IsDarkMode { get; set; }

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
            
            //
            // We filter the enabled preview features based on the preview visibility.
            // This is necessary when the app starts up: some preview features may have
            // been disabled or released from the last time the app was started.
            //
            this.ConfigurationData.App.EnabledPreviewFeatures = this.ConfigurationData.App.PreviewVisibility.FilterPreviewFeatures(this.ConfigurationData.App.EnabledPreviewFeatures);

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
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, settingsJson);
        
        File.Move(tempFile, settingsPath, true);
        this.logger.LogInformation("Stored the settings to the file system.");
    }
    
    public void InjectSpellchecking(Dictionary<string, object?> attributes) => attributes["spellcheck"] = this.ConfigurationData.App.EnableSpellchecking ? "true" : "false";

    public ConfidenceLevel GetMinimumConfidenceLevel(Tools.Components component)
    {
        var minimumLevel = ConfidenceLevel.NONE;
        var enforceGlobalMinimumConfidence = this.ConfigurationData.LLMProviders is { EnforceGlobalMinimumConfidence: true, GlobalMinimumConfidence: not ConfidenceLevel.NONE and not ConfidenceLevel.UNKNOWN };
        if (enforceGlobalMinimumConfidence)
            minimumLevel = this.ConfigurationData.LLMProviders.GlobalMinimumConfidence;
        
        var componentMinimumLevel = component.MinimumConfidence(this);
        if (componentMinimumLevel > minimumLevel)
            minimumLevel = componentMinimumLevel;
        
        return minimumLevel;
    }
    
    public Provider GetPreselectedProvider(Tools.Components component, string? chatProviderId = null)
    {
        var minimumLevel = this.GetMinimumConfidenceLevel(component);
        
        // When there is only one provider, and it has a confidence level that is high enough, we return it:
        if (this.ConfigurationData.Providers.Count == 1 && this.ConfigurationData.Providers[0].UsedLLMProvider.GetConfidence(this).Level >= minimumLevel)
            return this.ConfigurationData.Providers[0];

        // When there is a chat provider, and it has a confidence level that is high enough, we return it:
        if (chatProviderId is not null && !string.IsNullOrWhiteSpace(chatProviderId))
        {
            var chatProvider = this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == chatProviderId);
            if (chatProvider.UsedLLMProvider.GetConfidence(this).Level >= minimumLevel)
                return chatProvider;
        }
        
        // When there is a component-preselected provider, and it has a confidence level that is high enough, we return it:
        var preselectedProvider = component.PreselectedProvider(this);
        if(preselectedProvider != default && preselectedProvider.UsedLLMProvider.GetConfidence(this).Level >= minimumLevel)
            return preselectedProvider;

        // When there is an app-wide preselected provider, and it has a confidence level that is high enough, we return it:
        return this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.App.PreselectedProvider && x.UsedLLMProvider.GetConfidence(this).Level >= minimumLevel);
    }

    public Profile GetPreselectedProfile(Tools.Components component)
    {
        var preselection = component.PreselectedProfile(this);
        if (preselection != default)
            return preselection;
        
        preselection = this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == this.ConfigurationData.App.PreselectedProfile);
        return preselection != default ? preselection : Profile.NO_PROFILE;
    }

    public ConfidenceLevel GetConfiguredConfidenceLevel(LLMProviders llmProvider)
    {
        if(llmProvider is LLMProviders.NONE)
            return ConfidenceLevel.NONE;
        
        switch (this.ConfigurationData.LLMProviders.ConfidenceScheme)
        {
            case ConfidenceSchemes.TRUST_USA_EUROPE:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.FIREWORKS => ConfidenceLevel.UNTRUSTED,
                    
                    _ => ConfidenceLevel.MEDIUM,   
                };
            
            case ConfidenceSchemes.TRUST_USA:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.FIREWORKS => ConfidenceLevel.UNTRUSTED,
                    LLMProviders.MISTRAL => ConfidenceLevel.LOW,
                    
                    _ => ConfidenceLevel.MEDIUM,
                };
            
            case ConfidenceSchemes.TRUST_EUROPE:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.FIREWORKS => ConfidenceLevel.UNTRUSTED,
                    LLMProviders.MISTRAL => ConfidenceLevel.MEDIUM,
                    
                    _ => ConfidenceLevel.LOW,
                };
            
            case ConfidenceSchemes.LOCAL_TRUST_ONLY:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.FIREWORKS => ConfidenceLevel.UNTRUSTED,
                    
                    _ => ConfidenceLevel.VERY_LOW,   
                };

            case ConfidenceSchemes.CUSTOM:
                return this.ConfigurationData.LLMProviders.CustomConfidenceScheme.GetValueOrDefault(llmProvider, ConfidenceLevel.UNKNOWN);

            default:
                return ConfidenceLevel.UNKNOWN;
        }
    }
}