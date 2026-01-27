using System.Diagnostics.CodeAnalysis;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class ComponentsExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ComponentsExtensions).Namespace, nameof(ComponentsExtensions));
    
    public static bool AllowSendTo(this Components component) => component switch
    {
        Components.NONE => false,
        
        Components.ERI_ASSISTANT => false,
        Components.BIAS_DAY_ASSISTANT => false,
        Components.I18N_ASSISTANT => false,
        Components.DOCUMENT_ANALYSIS_ASSISTANT => false,
        
        Components.APP_SETTINGS => false,
        
        Components.AGENT_TEXT_CONTENT_CLEANER => false,
        Components.AGENT_DATA_SOURCE_SELECTION => false,
        Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION => false,
        
        _ => true,
    };
    
    public static string Name(this Components component) => component switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => TB("Grammar & Spelling Assistant"),
        Components.TEXT_SUMMARIZER_ASSISTANT => TB("Text Summarizer Assistant"),
        Components.ICON_FINDER_ASSISTANT => TB("Icon Finder Assistant"),
        Components.TRANSLATION_ASSISTANT => TB("Translation Assistant"),
        Components.REWRITE_ASSISTANT => TB("Rewrite Assistant"),
        Components.AGENDA_ASSISTANT => TB("Agenda Assistant"),
        Components.CODING_ASSISTANT => TB("Coding Assistant"),
        Components.EMAIL_ASSISTANT => TB("E-Mail Assistant"),
        Components.LEGAL_CHECK_ASSISTANT => TB("Legal Check Assistant"),
        Components.SYNONYMS_ASSISTANT => TB("Synonym Assistant"),
        Components.MY_TASKS_ASSISTANT => TB("My Tasks Assistant"),
        Components.JOB_POSTING_ASSISTANT => TB("Job Posting Assistant"),
        Components.ERI_ASSISTANT => TB("ERI Server"),
        Components.I18N_ASSISTANT => TB("Localization Assistant"),
        Components.DOCUMENT_ANALYSIS_ASSISTANT => TB("Document Analysis Assistant"),
        
        Components.CHAT => TB("New Chat"),
        
        _ => Enum.GetName(component)!,
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
        Components.DOCUMENT_ANALYSIS_ASSISTANT => new(Event.SEND_TO_DOCUMENT_ANALYSIS_ASSISTANT, Routes.ASSISTANT_DOCUMENT_ANALYSIS),
        
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
        Components.ERI_ASSISTANT => settingsManager.ConfigurationData.ERI.PreselectOptions ? settingsManager.ConfigurationData.ERI.MinimumProviderConfidence : default,
        
        #warning Add minimum confidence for DOCUMENT_ANALYSIS_ASSISTANT:
        //Components.DOCUMENT_ANALYSIS_ASSISTANT => settingsManager.ConfigurationData.DocumentAnalysis.PreselectOptions ? settingsManager.ConfigurationData.DocumentAnalysis.MinimumProviderConfidence : default,

        _ => default,
    };

    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    public static AIStudio.Settings.Provider PreselectedProvider(this Components component, SettingsManager settingsManager)
    {
        var preselectedProvider = component switch
        {
            Components.GRAMMAR_SPELLING_ASSISTANT => settingsManager.ConfigurationData.GrammarSpelling.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.GrammarSpelling.PreselectedProvider) : null,
            Components.ICON_FINDER_ASSISTANT => settingsManager.ConfigurationData.IconFinder.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.IconFinder.PreselectedProvider) : null,
            Components.REWRITE_ASSISTANT => settingsManager.ConfigurationData.RewriteImprove.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.RewriteImprove.PreselectedProvider) : null,
            Components.TRANSLATION_ASSISTANT => settingsManager.ConfigurationData.Translation.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Translation.PreselectedProvider) : null,
            Components.AGENDA_ASSISTANT => settingsManager.ConfigurationData.Agenda.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Agenda.PreselectedProvider) : null,
            Components.CODING_ASSISTANT => settingsManager.ConfigurationData.Coding.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Coding.PreselectedProvider) : null,
            Components.TEXT_SUMMARIZER_ASSISTANT => settingsManager.ConfigurationData.TextSummarizer.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.TextSummarizer.PreselectedProvider) : null,
            Components.EMAIL_ASSISTANT => settingsManager.ConfigurationData.EMail.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.EMail.PreselectedProvider) : null,
            Components.LEGAL_CHECK_ASSISTANT => settingsManager.ConfigurationData.LegalCheck.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.LegalCheck.PreselectedProvider) : null,
            Components.SYNONYMS_ASSISTANT => settingsManager.ConfigurationData.Synonyms.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Synonyms.PreselectedProvider) : null,
            Components.MY_TASKS_ASSISTANT => settingsManager.ConfigurationData.MyTasks.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.MyTasks.PreselectedProvider) : null,
            Components.JOB_POSTING_ASSISTANT => settingsManager.ConfigurationData.JobPostings.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.JobPostings.PreselectedProvider) : null,
            Components.BIAS_DAY_ASSISTANT => settingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.BiasOfTheDay.PreselectedProvider) : null,
            Components.ERI_ASSISTANT => settingsManager.ConfigurationData.ERI.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.ERI.PreselectedProvider) : null,
            Components.I18N_ASSISTANT => settingsManager.ConfigurationData.I18N.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.I18N.PreselectedProvider) : null,
            
            #warning Add preselected provider for DOCUMENT_ANALYSIS_ASSISTANT:
            //Components.DOCUMENT_ANALYSIS_ASSISTANT => settingsManager.ConfigurationData.DocumentAnalysis.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.DocumentAnalysis.PreselectedProvider) : null,

            Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Chat.PreselectedProvider) : null,

            Components.AGENT_TEXT_CONTENT_CLEANER => settingsManager.ConfigurationData.TextContentCleaner.PreselectAgentOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.TextContentCleaner.PreselectedAgentProvider) : null,
            Components.AGENT_DATA_SOURCE_SELECTION => settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectAgentOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectedAgentProvider) : null,
            Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION => settingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectAgentOptions ? settingsManager.ConfigurationData.Providers.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectedAgentProvider) : null,

            _ => Settings.Provider.NONE,
        };
        
        return preselectedProvider ?? Settings.Provider.NONE;
    }

    public static Profile PreselectedProfile(this Components component, SettingsManager settingsManager) => component switch
    {
        #warning Add preselected profile for DOCUMENT_ANALYSIS_ASSISTANT:
        // Components.DOCUMENT_ANALYSIS_ASSISTANT => settingsManager.ConfigurationData.DocumentAnalysis.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.DocumentAnalysis.PreselectedProfile) : default,
        Components.AGENDA_ASSISTANT => settingsManager.ConfigurationData.Agenda.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Agenda.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,
        Components.CODING_ASSISTANT => settingsManager.ConfigurationData.Coding.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Coding.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,
        Components.EMAIL_ASSISTANT => settingsManager.ConfigurationData.EMail.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.EMail.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,
        Components.LEGAL_CHECK_ASSISTANT => settingsManager.ConfigurationData.LegalCheck.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.LegalCheck.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,
        Components.MY_TASKS_ASSISTANT => settingsManager.ConfigurationData.MyTasks.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.MyTasks.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,
        Components.BIAS_DAY_ASSISTANT => settingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.BiasOfTheDay.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,
        Components.ERI_ASSISTANT => settingsManager.ConfigurationData.ERI.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.ERI.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,

        Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.ConfigurationData.Profiles.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Chat.PreselectedProfile) ?? Profile.NO_PROFILE : Profile.NO_PROFILE,
        
        _ => Profile.NO_PROFILE,
    };
    
    public static ChatTemplate PreselectedChatTemplate(this Components component, SettingsManager settingsManager) => component switch
    {
        Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.ConfigurationData.ChatTemplates.FirstOrDefault(x => x.Id == settingsManager.ConfigurationData.Chat.PreselectedChatTemplate) ?? ChatTemplate.NO_CHAT_TEMPLATE : ChatTemplate.NO_CHAT_TEMPLATE,
        
        _ => ChatTemplate.NO_CHAT_TEMPLATE,
    };
}