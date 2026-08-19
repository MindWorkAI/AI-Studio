using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Media;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class ComponentsExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ComponentsExtensions).Namespace, nameof(ComponentsExtensions));

    /// <summary>
    /// Gets the preview feature a component belongs to. Components that are generally available
    /// return <see cref="PreviewFeatures.NONE"/>. This is the single place that maps a component to
    /// its preview feature, so visibility checks never need to special-case one assistant.
    /// </summary>
    /// <param name="component">The component to look up.</param>
    /// <returns>The required preview feature.</returns>
    public static PreviewFeatures RequiredPreviewFeature(this Components component) => component switch
    {
        Components.VISUAL_BRIEFING_ASSISTANT => PreviewFeatures.PRE_VISUAL_BRIEFING_ASSISTANT_2026,

        _ => PreviewFeatures.NONE,
    };

    /// <summary>
    /// Gets whether a component owns exactly one assistant session slot, so that a running session
    /// blocks starting another one and inactive sessions can be cleared as a group.
    /// </summary>
    /// <remarks>
    /// Components return <c>false</c> for two different reasons. The chat has no assistant sessions
    /// at all. The visual briefing assistant keys its sessions per briefing, so it owns one slot per
    /// stored briefing rather than one per component. Both must be excluded from the single-slot
    /// checks, which is why this is a capability and not a component comparison.
    /// </remarks>
    /// <param name="component">The component to look up.</param>
    /// <returns><c>true</c> when the component owns exactly one session slot.</returns>
    public static bool HasSingleSessionSlot(this Components component) => component switch
    {
        Components.CHAT => false,
        Components.VISUAL_BRIEFING_ASSISTANT => false,

        _ => true,
    };

    /// <summary>
    /// Gets the kind of media-import owner a component creates for its attachments.
    /// </summary>
    /// <param name="component">The component to look up.</param>
    /// <returns>The media-import owner kind.</returns>
    public static MediaImportOwnerKind MediaOwnerKind(this Components component) => component switch
    {
        Components.VISUAL_BRIEFING_ASSISTANT => MediaImportOwnerKind.VISUAL_BRIEFING,

        _ => MediaImportOwnerKind.ASSISTANT,
    };

    public static bool AllowSendTo(this Components component) => component switch
    {
        Components.NONE => false,
        
        Components.ERI_ASSISTANT => false,
        Components.BIAS_DAY_ASSISTANT => false,
        Components.I18N_ASSISTANT => false,
        Components.DOCUMENT_ANALYSIS_ASSISTANT => false,
        Components.BATCH_PROCESSING_ASSISTANT => false,
        Components.LOG_VIEWER_ASSISTANT => false,
        
        Components.APP_SETTINGS => false,
        Components.WRITER => false,
        
        Components.AGENT_TEXT_CONTENT_CLEANER => false,
        Components.AGENT_DATA_SOURCE_SELECTION => false,
        Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION => false,
        Components.AGENT_ASSISTANT_PLUGIN_AUDIT => false,
        Components.META_ASSISTANT => false,
        
        _ => true,
    };
    
    public static string Name(this Components component) => component switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => TB("Grammar & Spelling Assistant"),
        Components.TEXT_SUMMARIZER_ASSISTANT => TB("Text Summarizer Assistant"),
        Components.ICON_FINDER_ASSISTANT => TB("Icon Finder Assistant"),
        Components.TRANSLATION_ASSISTANT => TB("Translation Assistant"),
        Components.REWRITE_ASSISTANT => TB("Rewrite Assistant"),
        Components.PROMPT_OPTIMIZER_ASSISTANT => TB("Prompt Optimizer Assistant"),
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
        Components.BATCH_PROCESSING_ASSISTANT => TB("Batch Processing Assistant"),
        Components.SLIDE_BUILDER_ASSISTANT => TB("Slide Planner Assistant"),
        Components.VISUAL_BRIEFING_ASSISTANT => TB("Visual Briefing Assistant"),
        Components.META_ASSISTANT => TB("Assistant Builder"),
        Components.LOG_VIEWER_ASSISTANT => TB("Log Viewer Assistant"),
        
        Components.CHAT => TB("New Chat"),
        
        _ => Enum.GetName(component)!,
    };

    public static ComponentsData GetData(this Components destination) => destination switch
    {
        Components.AGENDA_ASSISTANT => new(Event.SEND_TO_AGENDA_ASSISTANT, Routes.ASSISTANT_AGENDA),
        Components.CODING_ASSISTANT => new(Event.SEND_TO_CODING_ASSISTANT, Routes.ASSISTANT_CODING),
        Components.REWRITE_ASSISTANT => new(Event.SEND_TO_REWRITE_ASSISTANT, Routes.ASSISTANT_REWRITE),
        Components.PROMPT_OPTIMIZER_ASSISTANT => new(Event.SEND_TO_PROMPT_OPTIMIZER_ASSISTANT, Routes.ASSISTANT_PROMPT_OPTIMIZER),
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
        Components.SLIDE_BUILDER_ASSISTANT => new(Event.SEND_TO_SLIDE_BUILDER_ASSISTANT, Routes.ASSISTANT_SLIDE_BUILDER),
        Components.VISUAL_BRIEFING_ASSISTANT => new(Event.SEND_TO_VISUAL_BRIEFING_ASSISTANT, Routes.ASSISTANT_VISUAL_BRIEFING),
        
        Components.CHAT => new(Event.SEND_TO_CHAT, Routes.CHAT),
        
        _ => new(Event.NONE, Routes.ASSISTANTS),
    };

    public static ConfidenceLevel MinimumConfidence(this Components component, SettingsManager settingsManager) => component switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => settingsManager.ConfigurationData.GrammarSpelling.PreselectOptions ? settingsManager.ConfigurationData.GrammarSpelling.MinimumProviderConfidence : default,
        Components.ICON_FINDER_ASSISTANT => settingsManager.ConfigurationData.IconFinder.PreselectOptions ? settingsManager.ConfigurationData.IconFinder.MinimumProviderConfidence : default,
        Components.REWRITE_ASSISTANT => settingsManager.ConfigurationData.RewriteImprove.PreselectOptions ? settingsManager.ConfigurationData.RewriteImprove.MinimumProviderConfidence : default,
        Components.PROMPT_OPTIMIZER_ASSISTANT => settingsManager.ConfigurationData.PromptOptimizer.PreselectOptions ? settingsManager.ConfigurationData.PromptOptimizer.MinimumProviderConfidence : default,
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
        Components.SLIDE_BUILDER_ASSISTANT => settingsManager.ConfigurationData.SlideBuilder.PreselectOptions ? settingsManager.ConfigurationData.SlideBuilder.MinimumProviderConfidence : default,
        Components.VISUAL_BRIEFING_ASSISTANT => settingsManager.ConfigurationData.VisualBriefing.MinimumProviderConfidence,
        
        // The minimum confidence for the Document Analysis Assistant is set per policy.
        // We do this inside the Document Analysis Assistant component:
        Components.DOCUMENT_ANALYSIS_ASSISTANT => ConfidenceLevel.NONE,

        // A policy-specific minimum is merged with this component default inside
        // the Batch Processing Assistant; the stricter level wins.
        Components.BATCH_PROCESSING_ASSISTANT => settingsManager.ConfigurationData.BatchProcessing.PreselectOptions ? settingsManager.ConfigurationData.BatchProcessing.MinimumProviderConfidence : default,

        _ => default,
    };

    public static AIStudio.Settings.Provider PreselectedProvider(this Components component, SettingsManager settingsManager) => component switch
    {
        Components.GRAMMAR_SPELLING_ASSISTANT => settingsManager.ConfigurationData.GrammarSpelling.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.GrammarSpelling.PreselectedProvider) : Settings.Provider.NONE,
        Components.ICON_FINDER_ASSISTANT => settingsManager.ConfigurationData.IconFinder.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.IconFinder.PreselectedProvider) : Settings.Provider.NONE,
        Components.REWRITE_ASSISTANT => settingsManager.ConfigurationData.RewriteImprove.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.RewriteImprove.PreselectedProvider) : Settings.Provider.NONE,
        Components.PROMPT_OPTIMIZER_ASSISTANT => settingsManager.ConfigurationData.PromptOptimizer.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.PromptOptimizer.PreselectedProvider) : Settings.Provider.NONE,
        Components.TRANSLATION_ASSISTANT => settingsManager.ConfigurationData.Translation.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.Translation.PreselectedProvider) : Settings.Provider.NONE,
        Components.AGENDA_ASSISTANT => settingsManager.ConfigurationData.Agenda.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.Agenda.PreselectedProvider) : Settings.Provider.NONE,
        Components.CODING_ASSISTANT => settingsManager.ConfigurationData.Coding.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.Coding.PreselectedProvider) : Settings.Provider.NONE,
        Components.TEXT_SUMMARIZER_ASSISTANT => settingsManager.ConfigurationData.TextSummarizer.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.TextSummarizer.PreselectedProvider) : Settings.Provider.NONE,
        Components.EMAIL_ASSISTANT => settingsManager.ConfigurationData.EMail.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.EMail.PreselectedProvider) : Settings.Provider.NONE,
        Components.LEGAL_CHECK_ASSISTANT => settingsManager.ConfigurationData.LegalCheck.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.LegalCheck.PreselectedProvider) : Settings.Provider.NONE,
        Components.SYNONYMS_ASSISTANT => settingsManager.ConfigurationData.Synonyms.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.Synonyms.PreselectedProvider) : Settings.Provider.NONE,
        Components.MY_TASKS_ASSISTANT => settingsManager.ConfigurationData.MyTasks.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.MyTasks.PreselectedProvider) : Settings.Provider.NONE,
        Components.JOB_POSTING_ASSISTANT => settingsManager.ConfigurationData.JobPostings.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.JobPostings.PreselectedProvider) : Settings.Provider.NONE,
        Components.BIAS_DAY_ASSISTANT => settingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.BiasOfTheDay.PreselectedProvider) : Settings.Provider.NONE,
        Components.ERI_ASSISTANT => settingsManager.ConfigurationData.ERI.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.ERI.PreselectedProvider) : Settings.Provider.NONE,
        Components.I18N_ASSISTANT => settingsManager.ConfigurationData.I18N.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.I18N.PreselectedProvider) : Settings.Provider.NONE,
        Components.SLIDE_BUILDER_ASSISTANT => settingsManager.ConfigurationData.SlideBuilder.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.SlideBuilder.PreselectedProvider) : Settings.Provider.NONE,
        Components.VISUAL_BRIEFING_ASSISTANT => settingsManager.GetProviderById(settingsManager.ConfigurationData.VisualBriefing.PreselectedProvider),

        // The Document Analysis Assistant does not have a preselected provider at the component level.
        // The provider is selected per policy instead. We do this inside the Document Analysis Assistant component.
        Components.DOCUMENT_ANALYSIS_ASSISTANT => Settings.Provider.NONE,

        Components.BATCH_PROCESSING_ASSISTANT => settingsManager.ConfigurationData.BatchProcessing.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.BatchProcessing.PreselectedProvider) : Settings.Provider.NONE,

        Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.Chat.PreselectedProvider) : Settings.Provider.NONE,

        Components.AGENT_TEXT_CONTENT_CLEANER => settingsManager.ConfigurationData.TextContentCleaner.PreselectAgentOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.TextContentCleaner.PreselectedAgentProvider) : Settings.Provider.NONE,
        Components.AGENT_DATA_SOURCE_SELECTION => settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectAgentOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectedAgentProvider) : Settings.Provider.NONE,
        Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION => settingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectAgentOptions ? settingsManager.GetProviderById(settingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectedAgentProvider) : Settings.Provider.NONE,
        Components.AGENT_ASSISTANT_PLUGIN_AUDIT => settingsManager.GetProviderById(settingsManager.ConfigurationData.AssistantPluginAudit.PreselectedAgentProvider),

        _ => Settings.Provider.NONE,
    };

    public static ProfilePreselection GetProfilePreselection(this Components component, SettingsManager settingsManager)
    {
        var storedValue = component switch
        {
            Components.AGENDA_ASSISTANT => settingsManager.ConfigurationData.Agenda.PreselectOptions ? settingsManager.ConfigurationData.Agenda.PreselectedProfile : string.Empty,
            Components.CODING_ASSISTANT => settingsManager.ConfigurationData.Coding.PreselectOptions ? settingsManager.ConfigurationData.Coding.PreselectedProfile : string.Empty,
            Components.EMAIL_ASSISTANT => settingsManager.ConfigurationData.EMail.PreselectOptions ? settingsManager.ConfigurationData.EMail.PreselectedProfile : string.Empty,
            Components.LEGAL_CHECK_ASSISTANT => settingsManager.ConfigurationData.LegalCheck.PreselectOptions ? settingsManager.ConfigurationData.LegalCheck.PreselectedProfile : string.Empty,
            Components.MY_TASKS_ASSISTANT => settingsManager.ConfigurationData.MyTasks.PreselectOptions ? settingsManager.ConfigurationData.MyTasks.PreselectedProfile : string.Empty,
            Components.BIAS_DAY_ASSISTANT => settingsManager.ConfigurationData.BiasOfTheDay.PreselectOptions ? settingsManager.ConfigurationData.BiasOfTheDay.PreselectedProfile : string.Empty,
            Components.ERI_ASSISTANT => settingsManager.ConfigurationData.ERI.PreselectOptions ? settingsManager.ConfigurationData.ERI.PreselectedProfile : string.Empty,
            Components.SLIDE_BUILDER_ASSISTANT => settingsManager.ConfigurationData.SlideBuilder.PreselectOptions ? settingsManager.ConfigurationData.SlideBuilder.PreselectedProfile : string.Empty,
            Components.VISUAL_BRIEFING_ASSISTANT => settingsManager.ConfigurationData.VisualBriefing.PreselectedProfile,
            Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.ConfigurationData.Chat.PreselectedProfile : string.Empty,

            // The Document Analysis Assistant does not have a preselected profile at the component level.
            // The profile is selected per policy instead. We do this inside the Document Analysis Assistant component:
            Components.DOCUMENT_ANALYSIS_ASSISTANT => Profile.NO_PROFILE.Id,

            _ => string.Empty,
        };

        return ProfilePreselection.FromStoredValue(storedValue);
    }
    
    public static ChatTemplate PreselectedChatTemplate(this Components component, SettingsManager settingsManager) => component switch
    {
        Components.CHAT => settingsManager.ConfigurationData.Chat.PreselectOptions ? settingsManager.GetChatTemplateById(settingsManager.ConfigurationData.Chat.PreselectedChatTemplate) : ChatTemplate.NO_CHAT_TEMPLATE,
        
        _ => ChatTemplate.NO_CHAT_TEMPLATE,
    };
}