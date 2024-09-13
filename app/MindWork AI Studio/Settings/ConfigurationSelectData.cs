using AIStudio.Assistants.Agenda;
using AIStudio.Assistants.Coding;
using AIStudio.Assistants.IconFinder;
using AIStudio.Assistants.RewriteImprove;
using AIStudio.Assistants.TextSummarizer;
using AIStudio.Assistants.EMail;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;

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
    public static IEnumerable<ConfigurationSelectData<SendBehavior>> GetSendBehaviorData()
    {
        yield return new("No key is sending the input", SendBehavior.NO_KEY_IS_SENDING);
        yield return new("Modifier key + enter is sending the input", SendBehavior.MODIFER_ENTER_IS_SENDING);
        yield return new("Enter is sending the input", SendBehavior.ENTER_IS_SENDING);
    }
    
    public static IEnumerable<ConfigurationSelectData<UpdateBehavior>> GetUpdateBehaviorData()
    {
        yield return new("No automatic update checks", UpdateBehavior.NO_CHECK);
        yield return new("Once at startup", UpdateBehavior.ONCE_STARTUP);
        yield return new("Check every hour", UpdateBehavior.HOURLY);
        yield return new("Check every day", UpdateBehavior.DAILY);
        yield return new ("Check every week", UpdateBehavior.WEEKLY);
    }
    
    public static IEnumerable<ConfigurationSelectData<WorkspaceStorageBehavior>> GetWorkspaceStorageBehaviorData()
    {
        yield return new("Disable workspaces", WorkspaceStorageBehavior.DISABLE_WORKSPACES);
        yield return new("Store chats automatically", WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY);
        yield return new("Store chats manually", WorkspaceStorageBehavior.STORE_CHATS_MANUALLY);
    }
    
    public static IEnumerable<ConfigurationSelectData<WorkspaceStorageTemporaryMaintenancePolicy>> GetWorkspaceStorageTemporaryMaintenancePolicyData()
    {
        yield return new("No automatic maintenance for temporary chats", WorkspaceStorageTemporaryMaintenancePolicy.NO_AUTOMATIC_MAINTENANCE);
        yield return new("Delete temporary chats older than 7 days", WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_7_DAYS);
        yield return new("Delete temporary chats older than 30 days", WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_30_DAYS);
        yield return new("Delete temporary chats older than 90 days", WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_90_DAYS);
        yield return new("Delete temporary chats older than 180 days", WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_180_DAYS);
        yield return new("Delete temporary chats older than 1 year", WorkspaceStorageTemporaryMaintenancePolicy.DELETE_OLDER_THAN_365_DAYS);
    }
    
    public static IEnumerable<ConfigurationSelectData<NavBehavior>> GetNavBehaviorData()
    {
        yield return new("Navigation expands on mouse hover", NavBehavior.EXPAND_ON_HOVER);
        yield return new("Navigation never expands, but there are tooltips", NavBehavior.NEVER_EXPAND_USE_TOOLTIPS);
        yield return new("Navigation never expands, no tooltips", NavBehavior.NEVER_EXPAND_NO_TOOLTIPS);
        yield return new("Always expand navigation", NavBehavior.ALWAYS_EXPAND);
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
                yield return new("Not yet specified", language);
            else
                yield return new(language.Name(), language);
    }
    
    public static IEnumerable<ConfigurationSelectData<CommonLanguages>> GetCommonLanguagesOptionalData()
    {
        foreach (var language in Enum.GetValues<CommonLanguages>())
            if(language is CommonLanguages.AS_IS)
                yield return new("Do not specify the language", language);
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
    
    public static IEnumerable<ConfigurationSelectData<ConfidenceSchemes>> GetConfidenceSchemesData()
    {
        foreach (var scheme in Enum.GetValues<ConfidenceSchemes>())
            yield return new(scheme.GetListDescription(), scheme);
    }
    
    public static IEnumerable<ConfigurationSelectData<ConfidenceLevel>> GetConfidenceLevelsData()
    {
        foreach (var level in Enum.GetValues<ConfidenceLevel>())
        {
            switch (level)
            {
                case ConfidenceLevel.UNKNOWN:
                    continue;
                
                case ConfidenceLevel.NONE:
                    yield return new("No minimum confidence level chosen", level);
                    break;
                
                default:
                    yield return new(level.GetName(), level);
                    break;
            }
        }
    }
}