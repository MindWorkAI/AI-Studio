using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.ToolCallingSystem;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace AIStudio.Settings;

/// <summary>
/// The settings manager.
/// </summary>
public sealed class SettingsManager
{
    public readonly record struct ToolMinimumProviderConfidenceResolution(ConfidenceLevel ConfidenceLevel, string Source);

    private const string SETTINGS_FILENAME = "settings.json";
    private const Version CURRENT_SETTINGS_VERSION = Version.V6;
    
    private readonly record struct SettingsVersionReadResult(Version Version, SettingsWriteBlockReason FailureReason);
    
    private readonly record struct CurrentSettingsReadResult(Data? SettingsData, SettingsWriteBlockReason FailureReason);
    
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        Converters = { new TolerantEnumConverter() },
    };

    private readonly ILogger<SettingsManager> logger;
    private readonly RustService rustService;

    /// <summary>
    /// The settings manager.
    /// </summary>
    public SettingsManager(ILogger<SettingsManager> logger, RustService rustService)
    {
        this.logger = logger;
        this.rustService = rustService;
        this.logger.LogInformation("Settings manager created.");
    }

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
    /// Ensures that the startup start-page redirect is evaluated at most once per app session.
    /// </summary>
    public bool StartupStartPageRedirectHandled { get; set; }

    /// <summary>
    /// Indicates that the initial settings load attempt has completed.
    /// </summary>
    public bool HasCompletedInitialSettingsLoad { get; private set; }

    /// <summary>
    /// Indicates why settings writes are blocked for the current session.
    /// </summary>
    public SettingsWriteBlockReason SettingsWriteBlockReason { get; private set; } = SettingsWriteBlockReason.NONE;

    /// <summary>
    /// Indicates that settings writes are blocked for the current session.
    /// </summary>
    public bool SettingsWriteBlocked => this.SettingsWriteBlockReason is not SettingsWriteBlockReason.NONE;

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
        var settingsSnapshot = await this.TryReadSettingsSnapshot();
        if (settingsSnapshot is not null)
            this.ConfigurationData = settingsSnapshot;

        this.HasCompletedInitialSettingsLoad = true;
    }

    /// <summary>
    /// Reads the settings from disk without mutating the current in-memory state.
    /// </summary>
    /// <returns>A (migrated) settings snapshot, or null if it could not be read.</returns>
    public async Task<Data?> TryReadSettingsSnapshot()
    {
        this.SettingsWriteBlockReason = SettingsWriteBlockReason.NONE;
        if(!this.IsSetUp)
        {
            this.logger.LogWarning("Cannot load settings, because the configuration is not set up yet.");
            return null;
        }

        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        if(!File.Exists(settingsPath))
        {
            this.logger.LogWarning("Cannot load settings, because the settings file does not exist.");
            return null;
        }

        var settingsVersion = await this.TryReadSettingsVersion(settingsPath);
        if(settingsVersion.FailureReason is not SettingsWriteBlockReason.NONE)
        {
            this.BlockSettingsWrites(settingsVersion.FailureReason, "The settings file version could not be identified. Settings writes are blocked to avoid overwriting newer or unreadable settings.");
            return await this.TryReadCurrentVersionBackupSnapshotForBlockedSettings();
        }

        if(settingsVersion.Version > CURRENT_SETTINGS_VERSION)
        {
            this.BlockSettingsWrites(SettingsWriteBlockReason.VERSION_NEWER_THAN_APP, $"The settings file uses the newer version '{settingsVersion.Version}'. Settings writes are blocked to avoid overwriting newer settings.");
            return await this.TryReadCurrentVersionBackupSnapshotForBlockedSettings();
        }

        Data? settingsData;
        if(settingsVersion.Version < CURRENT_SETTINGS_VERSION)
        {
            settingsData = await this.TryReadCurrentVersionBackupSnapshot();
            if(settingsData is not null)
            {
                this.PrepareLoadedSettings(settingsData);
                await this.StoreSettingsSnapshot(settingsData, settingsPath);
                await this.StoreCurrentVersionBackup(settingsData);
                this.logger.LogInformation($"Restored settings from the '{GetBackupSettingsFilename(CURRENT_SETTINGS_VERSION)}' backup file.");
                return settingsData;
            }

            this.logger.LogInformation("No valid current-version settings backup was found. Migrating the settings file.");
            settingsData = SettingsMigrations.Migrate(this.logger, settingsVersion.Version, await File.ReadAllTextAsync(settingsPath), JSON_OPTIONS);
            this.PrepareLoadedSettings(settingsData);
            await this.StoreSettingsSnapshot(settingsData, settingsPath);
            await this.StoreCurrentVersionBackup(settingsData);
            return settingsData;
        }

        var currentSettings = await this.TryDeserializeCurrentSettings(settingsPath, "settings file");
        if(currentSettings.FailureReason is not SettingsWriteBlockReason.NONE)
        {
            this.BlockSettingsWrites(currentSettings.FailureReason, "The current settings file could not be safely loaded. Settings writes are blocked to avoid overwriting recoverable settings.");
            return await this.TryReadCurrentVersionBackupSnapshotForBlockedSettings();
        }

        settingsData = currentSettings.SettingsData!;
        this.PrepareLoadedSettings(settingsData);
        await this.StoreCurrentVersionBackup(settingsData);
        return settingsData;
    }

    private async Task<SettingsVersionReadResult> TryReadSettingsVersion(string settingsPath)
    {
        try
        {
            await using var settingsStream = File.OpenRead(settingsPath);
            using var settingsDocument = await JsonDocument.ParseAsync(settingsStream);
            if(!settingsDocument.RootElement.TryGetProperty("Version", out var versionElement))
            {
                this.logger.LogError($"Failed to read the version of the settings file '{settingsPath}'.");
                return new(Version.UNKNOWN, SettingsWriteBlockReason.VERSION_MISSING);
            }

            if(versionElement.ValueKind is JsonValueKind.String && versionElement.GetString() is { } versionText)
            {
                if(Enum.TryParse(versionText, out Version stringVersion) && Enum.IsDefined(stringVersion) && stringVersion is not Version.UNKNOWN)
                    return new(stringVersion, SettingsWriteBlockReason.NONE);

                if(versionText.StartsWith('V') && int.TryParse(versionText[1..], out var futureVersion) && futureVersion > (int)CURRENT_SETTINGS_VERSION)
                    return new((Version)futureVersion, SettingsWriteBlockReason.NONE);

                if(int.TryParse(versionText, out var numericStringVersion) && numericStringVersion > (int)CURRENT_SETTINGS_VERSION)
                    return new((Version)numericStringVersion, SettingsWriteBlockReason.NONE);
            }

            if(versionElement.ValueKind is JsonValueKind.Number && versionElement.TryGetInt32(out var numericVersion) && numericVersion > (int)Version.UNKNOWN && (Enum.IsDefined(typeof(Version), numericVersion) || numericVersion > (int)CURRENT_SETTINGS_VERSION))
                return new((Version)numericVersion, SettingsWriteBlockReason.NONE);
        }
        catch(Exception e)
        {
            this.logger.LogError(e, $"Failed to read the version of the settings file '{settingsPath}'.");
            return new(Version.UNKNOWN, SettingsWriteBlockReason.FILE_UNREADABLE);
        }

        return new(Version.UNKNOWN, SettingsWriteBlockReason.VERSION_UNKNOWN);
    }

    private async Task<Data?> TryReadCurrentVersionBackupSnapshot()
    {
        var backupSettingsPath = GetBackupSettingsPath(CURRENT_SETTINGS_VERSION);
        if(!File.Exists(backupSettingsPath))
        {
            this.logger.LogInformation($"The settings backup file '{backupSettingsPath}' does not exist.");
            return null;
        }

        var backupVersion = await this.TryReadSettingsVersion(backupSettingsPath);
        if(backupVersion.FailureReason is not SettingsWriteBlockReason.NONE)
        {
            this.logger.LogWarning($"The settings backup file '{backupSettingsPath}' could not be used because its version could not be identified. Reason: '{backupVersion.FailureReason}'.");
            return null;
        }

        if(backupVersion.Version != CURRENT_SETTINGS_VERSION)
        {
            this.logger.LogWarning($"The settings backup file '{backupSettingsPath}' uses version '{backupVersion.Version}' instead of '{CURRENT_SETTINGS_VERSION}'.");
            return null;
        }

        var backupSettings = await this.TryDeserializeCurrentSettings(backupSettingsPath, "settings backup file");
        if(backupSettings.FailureReason is not SettingsWriteBlockReason.NONE)
        {
            this.logger.LogWarning($"The settings backup file '{backupSettingsPath}' could not be used. Reason: '{backupSettings.FailureReason}'.");
            return null;
        }

        return backupSettings.SettingsData;
    }

    private async Task<Data?> TryReadCurrentVersionBackupSnapshotForBlockedSettings()
    {
        var settingsData = await this.TryReadCurrentVersionBackupSnapshot();
        if(settingsData is null)
        {
            this.logger.LogWarning($"No valid current-version settings backup was found while settings writes are blocked. Reason: '{this.SettingsWriteBlockReason}'.");
            return null;
        }

        this.PrepareLoadedSettings(settingsData);
        this.logger.LogWarning($"Loaded settings from the '{GetBackupSettingsFilename(CURRENT_SETTINGS_VERSION)}' backup file while settings writes remain blocked. Reason: '{this.SettingsWriteBlockReason}'.");
        return settingsData;
    }

    private async Task<CurrentSettingsReadResult> TryDeserializeCurrentSettings(string settingsPath, string sourceDescription)
    {
        try
        {
            var settingsData = JsonSerializer.Deserialize<Data>(await File.ReadAllTextAsync(settingsPath), JSON_OPTIONS);
            if(settingsData is null)
            {
                this.logger.LogError($"Failed to parse the {sourceDescription} '{settingsPath}'.");
                return new(null, SettingsWriteBlockReason.CURRENT_VERSION_INVALID);
            }

            if(settingsData.Version != CURRENT_SETTINGS_VERSION)
            {
                this.logger.LogError($"The {sourceDescription} '{settingsPath}' uses version '{settingsData.Version}' instead of '{CURRENT_SETTINGS_VERSION}'.");
                return new(null, SettingsWriteBlockReason.CURRENT_VERSION_INVALID);
            }

            return new(settingsData, SettingsWriteBlockReason.NONE);
        }
        catch(Exception e)
        {
            this.logger.LogError(e, $"Failed to parse the {sourceDescription} '{settingsPath}'.");
            return new(null, SettingsWriteBlockReason.FILE_UNREADABLE);
        }
    }

    private void BlockSettingsWrites(SettingsWriteBlockReason reason, string message)
    {
        this.SettingsWriteBlockReason = reason;
        this.logger.LogError($"{message} Reason: '{reason}'.");
    }

    private void PrepareLoadedSettings(Data settingsData)
    {
        //
        // We filter the enabled preview features based on the preview visibility.
        // This is necessary when the app starts up: some preview features may have
        // been disabled or released from the last time the app was started.
        //
        settingsData.App.EnabledPreviewFeatures = settingsData.App.PreviewVisibility.FilterPreviewFeatures(settingsData.App.EnabledPreviewFeatures);
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

        if(this.SettingsWriteBlocked)
        {
            this.logger.LogWarning($"Cannot store settings, because settings writes are blocked. Reason: '{this.SettingsWriteBlockReason}'.");
            return;
        }

        var settingsPath = Path.Combine(ConfigDirectory!, SETTINGS_FILENAME);
        await this.StoreSettingsSnapshot(this.ConfigurationData, settingsPath);
        await this.StoreCurrentVersionBackup(this.ConfigurationData);
    }

    private static string GetBackupSettingsFilename(Version version) => $"settings.{version.ToString().ToLowerInvariant()}.json";

    private static string GetBackupSettingsPath(Version version) => Path.Combine(ConfigDirectory!, GetBackupSettingsFilename(version));

    private async Task StoreCurrentVersionBackup(Data settingsData)
    {
        if(settingsData.Version != CURRENT_SETTINGS_VERSION)
        {
            this.logger.LogWarning($"Skipping settings backup because the settings version '{settingsData.Version}' is not the current version '{CURRENT_SETTINGS_VERSION}'.");
            return;
        }

        var backupSettingsPath = GetBackupSettingsPath(CURRENT_SETTINGS_VERSION);
        await this.StoreSettingsSnapshot(settingsData, backupSettingsPath);
        this.logger.LogInformation($"Stored the settings backup file '{backupSettingsPath}'.");
    }

    private async Task StoreSettingsSnapshot(Data settingsData, string settingsPath)
    {
        if(!Directory.Exists(ConfigDirectory))
        {
            this.logger.LogInformation("Creating the configuration directory.");
            Directory.CreateDirectory(ConfigDirectory!);
        }

        var settingsJson = JsonSerializer.Serialize(settingsData, JSON_OPTIONS);
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, settingsJson);
        
        File.Move(tempFile, settingsPath, true);
        this.logger.LogInformation($"Stored the settings to '{settingsPath}'.");
    }
    
    public void InjectSpellchecking(Dictionary<string, object?> attributes) => attributes["spellcheck"] = this.ConfigurationData.App.EnableSpellchecking ? "true" : "false";

    public ConfidenceLevel GetMinimumConfidenceLevel(Tools.Components component)
    {
        var minimumLevel = ConfidenceLevel.NONE;
        var enforceGlobalMinimumConfidence = this.ConfigurationData.Confidence is { EnforceGlobalMinimumConfidence: true, GlobalMinimumConfidence: not ConfidenceLevel.NONE and not ConfidenceLevel.UNKNOWN };
        if (enforceGlobalMinimumConfidence)
            minimumLevel = this.ConfigurationData.Confidence.GlobalMinimumConfidence;
        
        var componentMinimumLevel = component.MinimumConfidence(this);
        if (componentMinimumLevel > minimumLevel)
            minimumLevel = componentMinimumLevel;
        
        return minimumLevel;
    }
    
    /// <summary>
    /// Checks if the given plugin is enabled.
    /// </summary>
    /// <param name="plugin">The plugin to check.</param>
    /// <returns>True, when the plugin is enabled, false otherwise.</returns>
    public bool IsPluginEnabled(IPluginMetadata plugin) => plugin.Type is PluginType.CONFIGURATION || this.ConfigurationData.EnabledPlugins.Contains(plugin.Id);
    
    /// <summary>
    /// Returns the active language plugin.
    /// </summary>
    /// <returns>The active language plugin.</returns>
    public async Task<ILanguagePlugin> GetActiveLanguagePlugin()
    {
        switch (this.ConfigurationData.App.LanguageBehavior)
        {
            case LangBehavior.AUTO:
                var languageCode = await this.rustService.ReadUserLanguage();
                var languagePlugins = PluginFactory.RunningPlugins.OfType<ILanguagePlugin>().ToList();

                if (!string.IsNullOrWhiteSpace(languageCode))
                {
                    var exactMatch = languagePlugins.FirstOrDefault(x => string.Equals(x.IETFTag, languageCode, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch is not null)
                        return exactMatch;

                    var primaryLanguage = GetPrimaryLanguage(languageCode);
                    if (!string.IsNullOrWhiteSpace(primaryLanguage))
                    {
                        var primaryLanguageMatch = languagePlugins
                            .Where(x => string.Equals(GetPrimaryLanguage(x.IETFTag), primaryLanguage, StringComparison.OrdinalIgnoreCase))
                            .OrderBy(x => x.IETFTag, StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault();
                        
                        if (primaryLanguageMatch is not null)
                        {
                            this.logger.LogWarning($"No exact language plugin found for '{languageCode}'. Use language fallback '{primaryLanguageMatch.IETFTag}'.");
                            return primaryLanguageMatch;
                        }
                    }
                }

                this.logger.LogWarning($"The language plugin for the language '{languageCode}' (normalized='{languageCode}') is not available.");
                return PluginFactory.BaseLanguage;
            
            case LangBehavior.MANUAL:
                var pluginId = this.ConfigurationData.App.LanguagePluginId;
                var plugin = PluginFactory.RunningPlugins.FirstOrDefault(x => x.Id == pluginId);
                if (plugin is null)
                {
                    this.logger.LogWarning($"The chosen language plugin (id='{pluginId}') is not available.");
                    return PluginFactory.BaseLanguage;
                }

                if (plugin is ILanguagePlugin chosenLangPlugin)
                    return chosenLangPlugin;

                this.logger.LogError("The chosen language plugin is not a language plugin.");
                return PluginFactory.BaseLanguage;
        }
        
        this.logger.LogError("The language behavior is unknown.");
        return PluginFactory.BaseLanguage;
    }

    private static string GetPrimaryLanguage(string localeTag)
    {
        if (string.IsNullOrWhiteSpace(localeTag))
            return string.Empty;

        var separatorIndex = localeTag.IndexOf('-');
        if (separatorIndex < 0)
            return localeTag;

        return localeTag[..separatorIndex];
    }
    
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    public Provider GetPreselectedProvider(Tools.Components component, string? currentProviderId = null, bool usePreselectionBeforeCurrentProvider = false)
    {
        var minimumLevel = this.GetMinimumConfidenceLevel(component);
        
        // When there is only one provider, and it has a confidence level that is high enough, we return it:
        if (this.ConfigurationData.Providers.Count == 1 && this.ConfigurationData.Providers[0].UsedLLMProvider.GetConfidence(this).Level >= minimumLevel)
            return this.ConfigurationData.Providers[0];

        // Is there a current provider with a sufficiently high confidence level?
        var currentProvider = Provider.NONE;
        if (currentProviderId is not null && !string.IsNullOrWhiteSpace(currentProviderId))
        {
            var currentProviderProbe = this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == currentProviderId);
            if (currentProviderProbe is not null && currentProviderProbe.UsedLLMProvider.GetConfidence(this).Level >= minimumLevel)
                currentProvider = currentProviderProbe;
        }
        
        // Is there a component-preselected provider with a sufficiently high confidence level?
        var preselectedProvider = Provider.NONE;
        var preselectedProviderProbe = component.PreselectedProvider(this);
        if(preselectedProviderProbe != Provider.NONE && preselectedProviderProbe.UsedLLMProvider.GetConfidence(this).Level >= minimumLevel)
            preselectedProvider = preselectedProviderProbe;

        //
        // Case: The preselected provider should be used before the current provider,
        //       and the preselected provider is available and has a confidence level
        //       that is high enough.
        //
        if(usePreselectionBeforeCurrentProvider && preselectedProvider != Provider.NONE)
            return preselectedProvider;
        
        //
        // Case: The current provider is available and has a confidence level that is
        //       high enough.
        //
        if(currentProvider != Provider.NONE)
            return currentProvider;
        
        //
        // Case: The current provider should be used before the preselected provider,
        //       but the current provider is not available or does not have a confidence
        //       level that is high enough. The preselected provider is available and
        //       has a confidence level that is high enough.
        //
        if(preselectedProvider != Provider.NONE)
            return preselectedProvider;

        // When there is an app-wide preselected provider, and it has a confidence level that is high enough, we return it:
        return this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == this.ConfigurationData.App.PreselectedProvider && x.UsedLLMProvider.GetConfidence(this).Level >= minimumLevel) ?? Provider.NONE;
    }

    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    public Provider GetChatProviderForLoadedChat(string? chatProviderId = null)
    {
        var minimumLevel = this.GetMinimumConfidenceLevel(Tools.Components.CHAT);

        bool IsSelectableProvider(Provider provider) =>
            provider != Provider.NONE
            && provider.UsedLLMProvider != LLMProviders.NONE
            && provider.UsedLLMProvider.GetConfidence(this).Level >= minimumLevel;

        Provider? FindProviderById(string? providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return null;

            var provider = this.ConfigurationData.Providers.FirstOrDefault(x => x.Id == providerId);
            return provider is not null && IsSelectableProvider(provider) ? provider : null;
        }

        var chatProvider = FindProviderById(chatProviderId);
        if (chatProvider is not null)
            return chatProvider;

        var defaultChatProvider = this.ConfigurationData.Chat.PreselectOptions
            ? FindProviderById(this.ConfigurationData.Chat.PreselectedProvider)
            : null;
        if (defaultChatProvider is not null)
            return defaultChatProvider;

        var defaultAppProvider = FindProviderById(this.ConfigurationData.App.PreselectedProvider);
        if (defaultAppProvider is not null)
            return defaultAppProvider;

        var selectableProviders = this.ConfigurationData.Providers.Where(IsSelectableProvider).ToList();
        return selectableProviders.Count == 1 ? selectableProviders[0] : Provider.NONE;
    }

    public Profile GetPreselectedProfile(Tools.Components component)
    {
        var preselection = component.GetProfilePreselection(this);
        if (preselection.DoNotPreselectProfile)
            return Profile.NO_PROFILE;

        if (preselection.UseSpecificProfile)
            return this.GetProfileById(preselection.SpecificProfileId);

        var appPreselection = ProfilePreselection.FromStoredValue(this.ConfigurationData.App.PreselectedProfile);
        if (appPreselection.DoNotPreselectProfile || !appPreselection.UseSpecificProfile)
            return Profile.NO_PROFILE;

        return this.GetProfileById(appPreselection.SpecificProfileId);
    }

    public Profile GetAppPreselectedProfile()
    {
        var appPreselection = ProfilePreselection.FromStoredValue(this.ConfigurationData.App.PreselectedProfile);
        if (appPreselection.DoNotPreselectProfile || !appPreselection.UseSpecificProfile)
            return Profile.NO_PROFILE;

        return this.GetProfileById(appPreselection.SpecificProfileId);
    }
    
    public ChatTemplate GetPreselectedChatTemplate(Tools.Components component)
    {
        var preselection = component.PreselectedChatTemplate(this);
        if (preselection != ChatTemplate.NO_CHAT_TEMPLATE)
            return preselection;
        
        return this.GetChatTemplateById(this.ConfigurationData.App.PreselectedChatTemplate);
    }

    public Profile GetProfileById(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return Profile.NO_PROFILE;

        if (string.Equals(profileId, Profile.NO_PROFILE.Id, StringComparison.OrdinalIgnoreCase))
            return Profile.NO_PROFILE;

        return this.ConfigurationData.Profiles.FirstOrDefault(x => x.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase)) ?? Profile.NO_PROFILE;
    }

    public ChatTemplate GetChatTemplateById(string? chatTemplateId)
    {
        if (string.IsNullOrWhiteSpace(chatTemplateId))
            return ChatTemplate.NO_CHAT_TEMPLATE;

        if (string.Equals(chatTemplateId, ChatTemplate.NO_CHAT_TEMPLATE.Id, StringComparison.OrdinalIgnoreCase))
            return ChatTemplate.NO_CHAT_TEMPLATE;

        return this.ConfigurationData.ChatTemplates.FirstOrDefault(x => x.Id.Equals(chatTemplateId, StringComparison.OrdinalIgnoreCase)) ?? ChatTemplate.NO_CHAT_TEMPLATE;
    }

    public HashSet<string> GetDefaultToolIds(AIStudio.Tools.Components component)
    {
        var key = component.ToString();
        if (this.ConfigurationData.Tools.DefaultToolIdsByComponent.TryGetValue(key, out var toolIds))
            return ToolSelectionRules.NormalizeSelection(toolIds);

        return [];
    }

    public HashSet<string> FilterToolIdsForProvider(AIStudio.Settings.Provider provider, IEnumerable<string> selectedToolIds)
    {
        if (!this.AreToolsEnabled())
            return [];

        var toolCallingAvailability = provider.GetToolCallingAvailability();
        if (!toolCallingAvailability.IsAvailable)
            return [];

        var modelCapabilities = provider.GetModelCapabilities();
        var supportsRequiredApis =
            modelCapabilities.Contains(Capability.CHAT_COMPLETION_API) ||
            modelCapabilities.Contains(Capability.RESPONSES_API);
        if (!supportsRequiredApis || !modelCapabilities.Contains(Capability.FUNCTION_CALLING))
            return [];

        var providerConfidence = provider.UsedLLMProvider.GetConfidence(this).Level;
        var filtered = ToolSelectionRules.NormalizeSelection(selectedToolIds);

        foreach (var toolId in filtered.ToList())
        {
            if (!this.IsToolActive(toolId))
            {
                filtered.Remove(toolId);
                continue;
            }

            var minimumToolConfidence = this.GetMinimumProviderConfidenceForTool(toolId);
            if (!ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, minimumToolConfidence))
                filtered.Remove(toolId);
        }

        return filtered;
    }

    public bool AreToolsEnabled() => this.ConfigurationData.Tools.EnableTools;

    public bool IsToolActive(string toolId) =>
        this.AreToolsEnabled() &&
        !this.ConfigurationData.Tools.DisabledToolIds.Contains(toolId);

    public bool IsToolSelectionVisible(AIStudio.Tools.Components component) => component switch
    {
        AIStudio.Tools.Components.CHAT => true,
        _ => this.ConfigurationData.Tools.VisibleToolSelectionComponents.Contains(component.ToString()),
    };

    public void SetToolSelectionVisibility(AIStudio.Tools.Components component, bool isVisible)
    {
        if (component is AIStudio.Tools.Components.CHAT)
            return;

        var key = component.ToString();
        if (isVisible)
            this.ConfigurationData.Tools.VisibleToolSelectionComponents.Add(key);
        else
            this.ConfigurationData.Tools.VisibleToolSelectionComponents.Remove(key);
    }

    public ToolMinimumProviderConfidenceResolution GetMinimumProviderConfidenceResolutionForTool(string toolId)
    {
        if (ManagedConfiguration.TryGet(x => x.Tools, x => x.MinimumProviderConfidenceByToolId, out var configMeta) && configMeta.IsLocked)
        {
            var managedValues = configMeta.GetValue();
            if (managedValues.TryGetValue(toolId, out var configuredManagedLevel) &&
                Enum.TryParse<ConfidenceLevel>(configuredManagedLevel, true, out var managedConfidenceLevel) &&
                Enum.IsDefined(managedConfidenceLevel) &&
                managedConfidenceLevel is not ConfidenceLevel.UNKNOWN)
            {
                return new(managedConfidenceLevel, "managed config");
            }

            if (managedValues.ContainsKey(toolId))
            {
                this.logger.LogError(
                    "Managed minimum provider confidence '{ConfiguredLevel}' for tool '{ToolId}' is invalid. Requiring HIGH as a safe fallback.",
                    configuredManagedLevel,
                    toolId);
                return new(ConfidenceLevel.HIGH, "invalid managed config; safe fallback");
            }
        }

        if (this.ConfigurationData.Tools.MinimumProviderConfidenceByToolId.TryGetValue(toolId, out var configuredLevel) &&
            Enum.TryParse<ConfidenceLevel>(configuredLevel, true, out var confidenceLevel) &&
            Enum.IsDefined(confidenceLevel) &&
            confidenceLevel is not ConfidenceLevel.UNKNOWN)
        {
            return new(confidenceLevel, "stored override");
        }

        return new(ToolSelectionRules.GetDefaultMinimumProviderConfidence(toolId), "default fallback");
    }

    public ConfidenceLevel GetMinimumProviderConfidenceForTool(string toolId) => this.GetMinimumProviderConfidenceResolutionForTool(toolId).ConfidenceLevel;

    public void SetMinimumProviderConfidenceForTool(string toolId, ConfidenceLevel confidenceLevel)
    {
        var defaultLevel = ToolSelectionRules.GetDefaultMinimumProviderConfidence(toolId);
        if (confidenceLevel == defaultLevel)
        {
            this.ConfigurationData.Tools.MinimumProviderConfidenceByToolId.Remove(toolId);
            return;
        }

        this.ConfigurationData.Tools.MinimumProviderConfidenceByToolId[toolId] = confidenceLevel.ToString();
    }

    public ConfidenceLevel GetConfiguredConfidenceLevel(LLMProviders llmProvider)
    {
        if(llmProvider is LLMProviders.NONE)
            return ConfidenceLevel.NONE;
        
        switch (this.ConfigurationData.Confidence.ConfidenceScheme)
        {
            case ConfidenceSchemes.TRUST_ALL:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    
                    _ => ConfidenceLevel.MEDIUM,   
                };
            
            case ConfidenceSchemes.TRUST_USA_EUROPE:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.DEEP_SEEK => ConfidenceLevel.LOW,
                    
                    _ => ConfidenceLevel.MEDIUM,   
                };
            
            case ConfidenceSchemes.TRUST_USA:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.MISTRAL => ConfidenceLevel.LOW,
                    LLMProviders.HELMHOLTZ => ConfidenceLevel.LOW,
                    LLMProviders.GWDG => ConfidenceLevel.LOW,
                    LLMProviders.DEEP_SEEK => ConfidenceLevel.LOW,
                    
                    _ => ConfidenceLevel.MEDIUM,
                };
            
            case ConfidenceSchemes.TRUST_EUROPE:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.MISTRAL => ConfidenceLevel.MEDIUM,
                    LLMProviders.HELMHOLTZ => ConfidenceLevel.MEDIUM,
                    LLMProviders.GWDG => ConfidenceLevel.MEDIUM,
                    
                    _ => ConfidenceLevel.LOW,
                };
            
            case ConfidenceSchemes.TRUST_ASIA:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    LLMProviders.DEEP_SEEK => ConfidenceLevel.MEDIUM,
                    
                    _ => ConfidenceLevel.LOW,
                };
            
            case ConfidenceSchemes.LOCAL_TRUST_ONLY:
                return llmProvider switch
                {
                    LLMProviders.SELF_HOSTED => ConfidenceLevel.HIGH,
                    
                    _ => ConfidenceLevel.VERY_LOW,   
                };

            case ConfidenceSchemes.CUSTOM:
                return this.ConfigurationData.Confidence.CustomConfidenceScheme.GetValueOrDefault(llmProvider, ConfidenceLevel.UNKNOWN);

            default:
                return ConfidenceLevel.UNKNOWN;
        }
    }

    public static string ToSettingName<TIn, TOut>(Expression<Func<TIn, TOut>> propertyExpression)
    {
        MemberExpression? memberExpr;

        // Handle the case where the expression is a unary expression (e.g., when using Convert):
        if (propertyExpression.Body is UnaryExpression { NodeType: ExpressionType.Convert } unaryExpr)
            memberExpr = unaryExpr.Operand as MemberExpression;
        else
            memberExpr = propertyExpression.Body as MemberExpression;

        if (memberExpr is null)
            throw new ArgumentException("Expression must be a property access", nameof(propertyExpression));

        // Return the full name of the property, including the class name:
        return $"{typeof(TIn).Name}.{memberExpr.Member.Name}";
    }
}
