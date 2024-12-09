using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools;

public static class ComponentsExtensions
{
    public static bool AllowSendTo(this Components component) => component switch
    {
        Components.NONE => false,
        Components.EDI_ASSISTANT => false,
        Components.BIAS_DAY_ASSISTANT => false,
        
        _ => true,
    };
    
    public static string Name(this Components component) => component switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => "Grammar & Spelling Assistant",
        Components.TEXT_SUMMARIZER_ASSISTANT => "Text Summarizer Assistant",
        Components.ICON_FINDER_ASSISTANT => "Icon Finder Assistant",
        Components.TRANSLATION_ASSISTANT => "Translation Assistant",
        Components.REWRITE_ASSISTANT => "Rewrite Assistant",
        Components.AGENDA_ASSISTANT => "Agenda Assistant",
        Components.CODING_ASSISTANT => "Coding Assistant",
        Components.EMAIL_ASSISTANT => "E-Mail Assistant",
        Components.LEGAL_CHECK_ASSISTANT => "Legal Check Assistant",
        Components.SYNONYMS_ASSISTANT => "Synonym Assistant",
        Components.MY_TASKS_ASSISTANT => "My Tasks Assistant",
        Components.JOB_POSTING_ASSISTANT => "Job Posting Assistant",
        Components.EDI_ASSISTANT => "EDI Server",
        
        Components.CHAT => "New Chat",
        
        _ => Enum.GetName(typeof(Components), component)!,
    };

    public static ComponentsData GetData(this Components destination) => destination switch
    {
        Components.AGENDA_ASSISTANT => new(Event.SEND_TO_AGENDA_ASSISTANT, Routes.ASSISTANT_AGENDA),
        Components.CODING_ASSISTANT => new(Event.SEND_TO_CODING_ASSISTANT, Routes.ASSISTANT_CODING),
        Components.REWRITE_ASSISTANT => new(Event.SEND_TO_REWRITE_ASSISTANT, Routes.ASSISTANT_REWRITE),
        Components.EMAIL_ASSISTANT => new(Event.SEND_TO_EMAIL_ASSISTANT, Routes.ASSISTANT_EMAIL),
        Components.TRANSLATION_ASSISTANT => new(Event.SEND_TO_TRANSLATION_ASSISTANT, Routes.ASSISTANT_TRANSLATION),
        Components.ICON_FINDER_ASSISTANT => new(Event.SEND_TO_ICON_FINDER_ASSISTANT, Routes.ASSISTANT_ICON_FINDER),
        Components.GRAMMAR_SPELLING_ASSISTANT => new(Event.SEND_TO_GRAMMAR_SPELLING_ASSISTANT, Routes.ASSISTANT_GRAMMAR_SPELLING),
        Components.TEXT_SUMMARIZER_ASSISTANT => new(Event.SEND_TO_TEXT_SUMMARIZER_ASSISTANT, Routes.ASSISTANT_SUMMARIZER),
        Components.LEGAL_CHECK_ASSISTANT => new(Event.SEND_TO_LEGAL_CHECK_ASSISTANT, Routes.ASSISTANT_LEGAL_CHECK),
        Components.SYNONYMS_ASSISTANT => new(Event.SEND_TO_SYNONYMS_ASSISTANT, Routes.ASSISTANT_SYNONYMS),
        Components.MY_TASKS_ASSISTANT => new(Event.SEND_TO_MY_TASKS_ASSISTANT, Routes.ASSISTANT_MY_TASKS),
        Components.JOB_POSTING_ASSISTANT => new(Event.SEND_TO_JOB_POSTING_ASSISTANT, Routes.ASSISTANT_JOB_POSTING),
        
        Components.CHAT => new(Event.SEND_TO_CHAT, Routes.CHAT),
        
        _ => new(Event.NONE, Routes.ASSISTANTS),
    };

    public static ConfidenceLevel MinimumConfidence(this Components component, SettingsManager settingsManager) => component switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => settingsManager.ConfigurationData.GrammarSpelling.PreselectOptions ? settingsManager.ConfigurationData.GrammarSpelling.MinimumProviderConfidence : default,
        Components.ICON_FINDER_ASSISTANT => settingsManager.ConfigurationData.IconFinder.PreselectOptions ? settingsManager.ConfigurationData.IconFinder.MinimumProviderConfidence : default,
        Components.REWRITE_ASSISTANT => settingsManager.ConfigurationData.RewriteImprove.PreselectOptions ? settingsManager.ConfigurationData.RewriteImprove.MinimumProviderConfidence : default,
        Components.TRANSLATION_ASSISTANT => settingsManager.ConfigurationData.Translation.PreselectOptions ? settingsManager.ConfigurationData.Translation.MinimumProviderConfidence : default,
        Components.AGENDA_ASSISTANT => settingsManager.ConfigurationData.Agenda.PreselectOptions ? settingsManager.ConfigurationData.Agenda.MinimumProviderConfidence : default,
        Components.CODING_ASSISTANT => settingsManager.ConfigurationData.Coding.PreselectOptions ? settingsManager.ConfigurationData.Coding.MinimumProviderConfidence : default,
        Components.TEXT_SUMMARIZER_ASSISTANT => settingsManager.ConfigurationData.TextSummarizer.PreselectOptions ? settingsManager.ConfigurationData.TextSummarizer.MinimumProviderConfidence : default,
        Components.EMAIL_ASSISTANT => settingsManager.ConfigurationData.EMail.PreselectOptions ? settingsManager.ConfigurationData.EMail.MinimumProviderConfidence : default,
        Components.LEGAL_CHECK_ASSISTANT => settingsManager.ConfigurationData.LegalCheck.PreselectOptions ? settingsManager.ConfigurationData.LegalCheck.MinimumProviderConfidence : default,
        Components.SYNONYMS_ASSISTANT => settingsManager.ConfigurationData.Synonyms.PreselectOptions ? settingsManager.ConfigurationData.Synonyms.MinimumProviderConfidence : default,
        Components.MY_TASKS_ASSISTANT => settingsManager.ConfigurationData.MyTasks.PreselectOptions ? settingsManager.ConfigurationData.MyTasks.MinimumProviderConfidence : default,
        Components.JOB_POSTING_ASSISTANT => settingsManager.ConfigurationData.JobPostings.PreselectOptions ? settingsManager.ConfigurationData.JobPostings.MinimumProviderConfidence : default,
        Components.BIAS_DAY_ASSISTANT => settingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions ? settingsManager.ConfigurationData.BiasOfTheDay.MinimumProviderConfidence : default,
        Components.EDI_ASSISTANT => settingsManager.ConfigurationData.EDI.PreselectOptions ? settingsManager.ConfigurationData.EDI.MinimumProviderConfidence : default,

        _ => default,
    };

    public static Settings.Provider PreselectedProvider(this Components component, SettingsManager settingsManager) => component switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => settingsManager.ConfigurationData.GrammarSpelling.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.GrammarSpelling.PreselectedProvider) : default,
        Components.ICON_FINDER_ASSISTANT => settingsManager.ConfigurationData.IconFinder.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.IconFinder.PreselectedProvider) : default,
        Components.REWRITE_ASSISTANT => settingsManager.ConfigurationData.RewriteImprove.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.RewriteImprove.PreselectedProvider) : default,
        Components.TRANSLATION_ASSISTANT => settingsManager.ConfigurationData.Translation.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Translation.PreselectedProvider) : default,
        Components.AGENDA_ASSISTANT => settingsManager.ConfigurationData.Agenda.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Agenda.PreselectedProvider) : default,
        Components.CODING_ASSISTANT => settingsManager.ConfigurationData.Coding.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Coding.PreselectedProvider) : default,
        Components.TEXT_SUMMARIZER_ASSISTANT => settingsManager.ConfigurationData.TextSummarizer.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.TextSummarizer.PreselectedProvider) : default,
        Components.EMAIL_ASSISTANT => settingsManager.ConfigurationData.EMail.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.EMail.PreselectedProvider) : default,
        Components.LEGAL_CHECK_ASSISTANT => settingsManager.ConfigurationData.LegalCheck.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.LegalCheck.PreselectedProvider) : default,
        Components.SYNONYMS_ASSISTANT => settingsManager.ConfigurationData.Synonyms.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Synonyms.PreselectedProvider) : default,
        Components.MY_TASKS_ASSISTANT => settingsManager.ConfigurationData.MyTasks.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.MyTasks.PreselectedProvider) : default,
        Components.JOB_POSTING_ASSISTANT => settingsManager.ConfigurationData.JobPostings.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.JobPostings.PreselectedProvider) : default,
        Components.BIAS_DAY_ASSISTANT => settingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.BiasOfTheDay.PreselectedProvider) : default,
        Components.EDI_ASSISTANT => settingsManager.ConfigurationData.EDI.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.EDI.PreselectedProvider) : default,

        Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Chat.PreselectedProvider) : default,
        
        _ => default,
    };

    public static Profile PreselectedProfile(this Components component, SettingsManager settingsManager) => component switch
    {
        Components.AGENDA_ASSISTANT => settingsManager.ConfigurationData.Agenda.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Agenda.PreselectedProfile) : default,
        Components.CODING_ASSISTANT => settingsManager.ConfigurationData.Coding.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Coding.PreselectedProfile) : default,
        Components.EMAIL_ASSISTANT => settingsManager.ConfigurationData.EMail.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.EMail.PreselectedProfile) : default,
        Components.LEGAL_CHECK_ASSISTANT => settingsManager.ConfigurationData.LegalCheck.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.LegalCheck.PreselectedProfile) : default,
        Components.MY_TASKS_ASSISTANT => settingsManager.ConfigurationData.MyTasks.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.MyTasks.PreselectedProfile) : default,
        Components.BIAS_DAY_ASSISTANT => settingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.BiasOfTheDay.PreselectedProfile) : default,
        Components.EDI_ASSISTANT => settingsManager.ConfigurationData.EDI.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.EDI.PreselectedProfile) : default,

        Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Chat.PreselectedProfile) : default,
        
        _ => default,
    };
}