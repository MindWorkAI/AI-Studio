using AIStudio.Assistants.Agenda;
using AIStudio.Assistants.Coding;
using AIStudio.Assistants.IconFinder;
using AIStudio.Assistants.RewriteImprove;
using AIStudio.Assistants.TextSummarizer;
using AIStudio.Assistants.EMail;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

using WritingStylesRewrite = AIStudio.Assistants.RewriteImprove.WritingStyles;
using WritingStylesEMail = AIStudio.Assistants.EMail.WritingStyles;

namespace AIStudio.Settings;

/// <summary>
/// A data structure to map a name to a value.
/// </summary>
/// <param name="Name">The name of the value, to be displayed in the UI.</param>
/// <param name="Value">The value to be stored.</param>
/// <typeparam name="T">The type of the value to store.</typeparam>
public readonly record struct ConfigurationSelectData<T>(string Name, T Value);

/// <summary>
/// A static factory class to get the lists of selectable values.
/// </summary>
public static class ConfigurationSelectDataFactory
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfigurationSelectDataFactory).Namespace, nameof(ConfigurationSelectDataFactory));
    
    public static IEnumerable<ConfigurationSelectData<LangBehavior>> GetLangBehaviorData()
    {
        foreach (var behavior in Enum.GetValues<LangBehavior>())
            yield return new(behavior.Name(), behavior);
    }
    
    public static IEnumerable<ConfigurationSelectData<Guid>> GetLanguagesData()
    {
        foreach (var runningPlugin in PluginFactory.RunningPlugins)
        {
            if(runningPlugin is ILanguagePlugin languagePlugin)
                yield return new(languagePlugin.LangName, runningPlugin.Id);
        }
    }
    
    public static IEnumerable<ConfigurationSelectData<LoadingChatProviderBehavior>> GetLoadingChatProviderBehavior()
    {
        yield return new(TB("When possible, use the LLM provider which was used for each chat in the first place"), LoadingChatProviderBehavior.USE_CHAT_PROVIDER_IF_AVAILABLE);
        yield return new(TB("Use the latest LLM provider, which was used before; use the default chat provider initially"), LoadingChatProviderBehavior.ALWAYS_USE_LATEST_CHAT_PROVIDER);
        yield return new(TB("Always use the default chat provider when loading chats"), LoadingChatProviderBehavior.ALWAYS_USE_DEFAULT_CHAT_PROVIDER);
    }
    
    public static IEnumerable<ConfigurationSelectData<AddChatProviderBehavior>> GetAddChatProviderBehavior()
    {
        yield return new(TB("Use the latest LLM provider, which was used before; use the default chat provider initially"), AddChatProviderBehavior.ADDED_CHATS_USE_LATEST_PROVIDER);
        yield return new(TB("Always use the default chat provider for new chats"), AddChatProviderBehavior.ADDED_CHATS_USE_DEFAULT_PROVIDER);
    }
    
    public static IEnumerable<ConfigurationSelectData<SendBehavior>> GetSendBehaviorData()
    {
        yield return new(TB("No key is sending the input"), SendBehavior.NO_KEY_IS_SENDING);
        yield return new(TB("Modifier key + enter is sending the input"), SendBehavior.MODIFER_ENTER_IS_SENDING);
        yield return new(TB("Enter is sending the input"), SendBehavior.ENTER_IS_SENDING);
    }
    
    public static IEnumerable<ConfigurationSelectData<UpdateBehavior>> GetUpdateBehaviorData()
    {
        yield return new(TB("No automatic update checks"), UpdateBehavior.NO_CHECK);
        yield return new(TB("Once at startup"), UpdateBehavior.ONCE_STARTUP);
        yield return new(TB("Check every hour"), UpdateBehavior.HOURLY);
        yield return new(TB("Check every day"), UpdateBehavior.DAILY);
        yield return new (TB("Check every week"), UpdateBehavior.WEEKLY);
    }
    
    public static IEnumerable<ConfigurationSelectData<WorkspaceStorageBehavior>> GetWorkspaceStorageBehaviorData()
    {
        yield return new(TB("Disable workspaces"), WorkspaceStorageBehavior.DISABLE_WORKSPACES);
        yield return new(TB("Store chats automatically"), WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY);
        yield return new(TB("Store chats manually"), WorkspaceStorageBehavior.STORE_CHATS_MANUALLY);
    }
    
    public static IEnumerable<ConfigurationSelectData<WorkspaceStorageTemporaryMaintenancePolicy>> GetWorkspaceStorageTemporaryMaintenancePolicyData()
    {
        yield return new(TB("No automatic maintenance for disappearing chats; old chats will never be deleted"), WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE);
        yield return new(TB("Delete disappearing chats older than 7 days"), WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS);
        yield return new(TB("Delete disappearing chats older than 30 days"), WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS);
        yield return new(TB("Delete disappearing chats older than 90 days"), WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS);
        yield return new(TB("Delete disappearing chats older than 180 days"), WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_180_DAYS);
        yield return new(TB("Delete disappearing chats older than 1 year"), WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_365_DAYS);
    }
    
    public static IEnumerable<ConfigurationSelectData<WorkspaceDisplayBehavior>> GetWorkspaceDisplayBehaviorData()
    {
        yield return new(TB("Toggle the overlay: the chat uses all the space, workspaces are temporarily shown"), WorkspaceDisplayBehavior.TOGGLE_OVERLAY);
        yield return new(TB("Toggle the sidebar: show the workspaces next to the chat when desired"), WorkspaceDisplayBehavior.TOGGLE_SIDEBAR);
        yield return new(TB("Sidebar is always visible: show the workspaces next to the chat all the time"), WorkspaceDisplayBehavior.SIDEBAR_ALWAYS_VISIBLE);
    }

    public static IEnumerable<ConfigurationSelectData<PreviewVisibility>> GetPreviewVisibility()
    {
        yield return new(TB("All preview features are hidden"), PreviewVisibility.NONE);
        yield return new(TB("Also show features ready for release; these should be stable"), PreviewVisibility.RELEASE_CANDIDATE);
        yield return new(TB("Also show features in beta: these are almost ready for release; expect some bugs"), PreviewVisibility.BETA);
        yield return new(TB("Also show features in alpha: these are in development; expect bugs and missing features"), PreviewVisibility.ALPHA);
        yield return new(TB("Show also prototype features: these are works in progress; expect bugs and missing features"), PreviewVisibility.PROTOTYPE);
        yield return new(TB("Show also experimental features: these are experimental; expect bugs, missing features, many changes"), PreviewVisibility.EXPERIMENTAL);
    }
    
    public static IEnumerable<ConfigurationSelectData<PreviewFeatures>> GetPreviewFeaturesData(SettingsManager settingsManager)
    {
        foreach (var source in settingsManager.ConfigurationData.App.PreviewVisibility.GetPreviewFeatures().Where(x => !x.IsReleased()))
            yield return new(source.GetPreviewDescription(), source);
    }
    
    public static IEnumerable<ConfigurationSelectData<SendToChatDataSourceBehavior>> GetSendToChatDataSourceBehaviorData()
    {
        foreach (var behavior in Enum.GetValues<SendToChatDataSourceBehavior>())
            yield return new(behavior.Description(), behavior);
    }
    
    public static IEnumerable<ConfigurationSelectData<NavBehavior>> GetNavBehaviorData()
    {
        yield return new(TB("Navigation expands on mouse hover"), NavBehavior.EXPAND_ON_HOVER);
        yield return new(TB("Navigation never expands, but there are tooltips"), NavBehavior.NEVER_EXPAND_USE_TOOLTIPS);
        yield return new(TB("Navigation never expands, no tooltips"), NavBehavior.NEVER_EXPAND_NO_TOOLTIPS);
        yield return new(TB("Always expand navigation"), NavBehavior.ALWAYS_EXPAND);
    }

    public static IEnumerable<ConfigurationSelectData<IconSources>> GetIconSourcesData()
    {
        foreach (var source in Enum.GetValues<IconSources>())
            yield return new(source.Name(), source);
    }
    
    public static IEnumerable<ConfigurationSelectData<CommonLanguages>> GetCommonLanguagesData()
    {
        foreach (var language in Enum.GetValues<CommonLanguages>())
            yield return new(language.Name(), language);
    }
    
    public static IEnumerable<ConfigurationSelectData<CommonLanguages>> GetCommonLanguagesTranslationData()
    {
        foreach (var language in Enum.GetValues<CommonLanguages>())
            if(language is CommonLanguages.AS_IS)
                yield return new(TB("Not yet specified"), language);
            else
                yield return new(language.Name(), language);
    }
    
    public static IEnumerable<ConfigurationSelectData<CommonLanguages>> GetCommonLanguagesOptionalData()
    {
        foreach (var language in Enum.GetValues<CommonLanguages>())
            if(language is CommonLanguages.AS_IS)
                yield return new(TB("Do not specify the language"), language);
            else
                yield return new(language.Name(), language);
    }
    
    public static IEnumerable<ConfigurationSelectData<CommonCodingLanguages>> GetCommonCodingLanguagesData()
    {
        foreach (var language in Enum.GetValues<CommonCodingLanguages>())
            yield return new(language.Name(), language);
    }
    
    public static IEnumerable<ConfigurationSelectData<Complexity>> GetComplexityData()
    {
        foreach (var complexity in Enum.GetValues<Complexity>())
            yield return new(complexity.Name(), complexity);
    }
    
    public static IEnumerable<ConfigurationSelectData<NumberParticipants>> GetNumberParticipantsData()
    {
        foreach (var number in Enum.GetValues<NumberParticipants>())
            yield return new(number.Name(), number);
    }
    
    public static IEnumerable<ConfigurationSelectData<WritingStylesRewrite>> GetWritingStyles4RewriteData()
    {
        foreach (var style in Enum.GetValues<WritingStylesRewrite>())
            yield return new(style.Name(), style);
    }
    
    public static IEnumerable<ConfigurationSelectData<WritingStylesEMail>> GetWritingStyles4EMailData()
    {
        foreach (var style in Enum.GetValues<WritingStylesEMail>())
            yield return new(style.Name(), style);
    }
    
    public static IEnumerable<ConfigurationSelectData<SentenceStructure>> GetSentenceStructureData()
    {
        foreach (var voice in Enum.GetValues<SentenceStructure>())
            yield return new(voice.Name(), voice);
    }
    
    public static IEnumerable<ConfigurationSelectData<string>> GetProfilesData(IEnumerable<Profile> profiles)
    {
        foreach (var profile in profiles.GetAllProfiles())
            yield return new(profile.Name, profile.Id);
    }
    
    public static IEnumerable<ConfigurationSelectData<string>> GetChatTemplatesData(IEnumerable<ChatTemplate> chatTemplates)
    {
        foreach (var chatTemplate in chatTemplates.GetAllChatTemplates())
            yield return new(chatTemplate.Name, chatTemplate.Id);
    }
    
    public static IEnumerable<ConfigurationSelectData<ConfidenceSchemes>> GetConfidenceSchemesData()
    {
        foreach (var scheme in Enum.GetValues<ConfidenceSchemes>())
            yield return new(scheme.GetListDescription(), scheme);
    }
    
    public static IEnumerable<ConfigurationSelectData<ConfidenceLevel>> GetConfidenceLevelsData(SettingsManager settingsManager, bool restrictToGlobalMinimum = false)
    {
        var minimumLevel = ConfidenceLevel.NONE;
        if(restrictToGlobalMinimum && settingsManager.ConfigurationData.LLMProviders is { EnforceGlobalMinimumConfidence: true, GlobalMinimumConfidence: not ConfidenceLevel.NONE and not ConfidenceLevel.UNKNOWN })
            minimumLevel = settingsManager.ConfigurationData.LLMProviders.GlobalMinimumConfidence;
        
        foreach (var level in Enum.GetValues<ConfidenceLevel>())
        {
            if(level is ConfidenceLevel.NONE)
                yield return new(TB("No minimum confidence level chosen"), level);
            
            if(level < minimumLevel)
                continue;
            
            switch (level)
            {
                case ConfidenceLevel.NONE:
                case ConfidenceLevel.UNKNOWN:
                    continue;
                
                default:
                    yield return new(level.GetName(), level);
                    break;
            }
        }
    }
    
    public static IEnumerable<ConfigurationSelectData<Themes>> GetThemesData()
    {
        foreach (var theme in Enum.GetValues<Themes>())
            yield return new(theme.GetName(), theme);
    }
}