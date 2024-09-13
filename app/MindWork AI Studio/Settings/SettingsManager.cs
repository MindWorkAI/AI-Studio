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

    public Provider GetPreselectedProvider(Tools.Components component)
    {
        if(this.ConfigurationData.Providers.Count == 1)
            return this.ConfigurationData.Providers[0];
        
        var preselection = component switch
        {
            Tools.Components.CHAT => this.ConfigurationData.Chat.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.Chat.PreselectedProvider) : default,
            Tools.Components.GRAMMAR_SPELLING_ASSISTANT => this.ConfigurationData.GrammarSpelling.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.GrammarSpelling.PreselectedProvider) : default,
            Tools.Components.ICON_FINDER_ASSISTANT => this.ConfigurationData.IconFinder.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.IconFinder.PreselectedProvider) : default,
            Tools.Components.REWRITE_ASSISTANT => this.ConfigurationData.RewriteImprove.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.RewriteImprove.PreselectedProvider) : default,
            Tools.Components.TRANSLATION_ASSISTANT => this.ConfigurationData.Translation.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.Translation.PreselectedProvider) : default,
            Tools.Components.AGENDA_ASSISTANT => this.ConfigurationData.Agenda.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.Agenda.PreselectedProvider) : default,
            Tools.Components.CODING_ASSISTANT => this.ConfigurationData.Coding.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.Coding.PreselectedProvider) : default,
            Tools.Components.TEXT_SUMMARIZER_ASSISTANT => this.ConfigurationData.TextSummarizer.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.TextSummarizer.PreselectedProvider) : default,
            Tools.Components.EMAIL_ASSISTANT => this.ConfigurationData.EMail.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.EMail.PreselectedProvider) : default,
            Tools.Components.LEGAL_CHECK_ASSISTANT => this.ConfigurationData.LegalCheck.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.LegalCheck.PreselectedProvider) : default,
            Tools.Components.SYNONYMS_ASSISTANT => this.ConfigurationData.Synonyms.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.Synonyms.PreselectedProvider) : default,
            Tools.Components.MY_TASKS_ASSISTANT => this.ConfigurationData.MyTasks.PreselectOptions ? this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.MyTasks.PreselectedProvider) : default,
            
            _ => default,
        };

        if (preselection != default)
            return preselection;

        return this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.App.PreselectedProvider);
    }

    public Profile GetPreselectedProfile(Tools.Components component)
    {
        var preselection = component switch
        {
            Tools.Components.CHAT => this.ConfigurationData.Chat.PreselectOptions ? this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == this.ConfigurationData.Chat.PreselectedProfile) : default,
            Tools.Components.AGENDA_ASSISTANT => this.ConfigurationData.Agenda.PreselectOptions ? this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == this.ConfigurationData.Agenda.PreselectedProfile) : default,
            Tools.Components.CODING_ASSISTANT => this.ConfigurationData.Coding.PreselectOptions ? this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == this.ConfigurationData.Coding.PreselectedProfile) : default,
            Tools.Components.EMAIL_ASSISTANT => this.ConfigurationData.EMail.PreselectOptions ? this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == this.ConfigurationData.EMail.PreselectedProfile) : default,
            Tools.Components.LEGAL_CHECK_ASSISTANT => this.ConfigurationData.LegalCheck.PreselectOptions ? this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == this.ConfigurationData.LegalCheck.PreselectedProfile) : default,
            Tools.Components.MY_TASKS_ASSISTANT => this.ConfigurationData.MyTasks.PreselectOptions ? this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == this.ConfigurationData.MyTasks.PreselectedProfile) : default,

            _ => default,
        };
        
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