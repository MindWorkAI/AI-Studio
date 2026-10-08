using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.SWOTAnalysis;

public partial class AssistantSWOTAnalysis : AssistantBaseCore<SettingsDialogSWOTAnalysis>
{
    protected override Tools.Components Component => Tools.Components.SWOT_ANALYSIS_ASSISTANT;

    protected override string Title => T("SWOT Analysis");

    protected override string Description => T("This assistant serves as a strategic planning tool that systematically captures strengths, weaknesses, opportunities, and risks. It can assist in positioning and strategy development for companies, organizations, or individuals. Additionally, it formulates concrete next steps to facilitate strategic decisions.");

    protected override string SystemPrompt =>
        SwotAnalysisFinalization.BuildSystemPrompt(this.OutputLanguageInstruction);

    protected override bool AllowProfiles => false;

    protected override bool ShowResult => !this.IsProcessing && this.analysisResult is not null;

    protected override IReadOnlyList<IButtonData> FooterButtons => [];

    protected override string SubmitText => T("Create SWOT analysis");

    protected override Func<Task> SubmitAction => this.AnalyzeText;

    protected override bool SubmitDisabled => this.isAgentRunning;

    protected override string SendToChatVisibleUserPromptText => T("Create a SWOT analysis of my content");

    protected override void ResetForm()
    {
        this.analysisResult = null;
        this.categoryAnalysisProgress.Clear();
        this.isFinalizingAnalysis = false;
        this.inputText = string.Empty;
        this.analysisGoal = string.Empty;
        this.contextMaterials.Clear();
        if (!this.MightPreselectValues())
        {
            this.showWebContentReader = false;
            this.useContentCleanerAgent = false;
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
            this.importantAspects = string.Empty;
        }
    }

    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.SWOTAnalysis.PreselectOptions)
        {
            this.showWebContentReader = this.SettingsManager.ConfigurationData.SWOTAnalysis.PreselectWebContentReader;
            this.useContentCleanerAgent = this.SettingsManager.ConfigurationData.SWOTAnalysis.PreselectContentCleanerAgent;
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.SWOTAnalysis.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.SWOTAnalysis.PreselectedOtherLanguage;
            this.importantAspects = this.SettingsManager.ConfigurationData.SWOTAnalysis.PreselectedImportantAspects;
            return true;
        }

        return false;
    }

    private bool showWebContentReader;
    private bool useContentCleanerAgent;
    private string inputText = string.Empty;
    private string analysisGoal = string.Empty;
    private string importantAspects = string.Empty;
    private HashSet<FileAttachment> contextMaterials = [];
    private bool isAgentRunning;
    private CommonLanguages selectedTargetLanguage = CommonLanguages.AS_IS;
    private string customTargetLanguage = string.Empty;
    private SwotAnalysisResult? analysisResult;
    private Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress> categoryAnalysisProgress = [];
    private bool isFinalizingAnalysis;
    private string OutputLanguageInstruction => this.selectedTargetLanguage.PromptGeneralPurpose(this.customTargetLanguage);
    private static readonly AssistantSessionStateKey<bool> SHOW_WEB_CONTENT_READER_STATE_KEY = new(nameof(showWebContentReader));
    private static readonly AssistantSessionStateKey<bool> USE_CONTENT_CLEANER_AGENT_STATE_KEY = new(nameof(useContentCleanerAgent));
    private static readonly AssistantSessionStateKey<string> INPUT_TEXT_STATE_KEY = new(nameof(inputText));
    private static readonly AssistantSessionStateKey<string> ANALYSIS_GOAL_STATE_KEY = new(nameof(analysisGoal));
    private static readonly AssistantSessionStateKey<string> IMPORTANT_ASPECTS_STATE_KEY = new(nameof(importantAspects));
    private static readonly AssistantSessionStateKey<HashSet<FileAttachment>> CONTEXT_MATERIALS_STATE_KEY = new(nameof(contextMaterials));
    private static readonly AssistantSessionStateKey<bool> IS_AGENT_RUNNING_STATE_KEY = new(nameof(isAgentRunning));
    private static readonly AssistantSessionStateKey<CommonLanguages> SELECTED_TARGET_LANGUAGE_STATE_KEY = new(nameof(selectedTargetLanguage));
    private static readonly AssistantSessionStateKey<string> CUSTOM_TARGET_LANGUAGE_STATE_KEY = new(nameof(customTargetLanguage));
    private static readonly AssistantSessionStateKey<SwotAnalysisResult?> ANALYSIS_RESULT_STATE_KEY = new(nameof(analysisResult));
    private static readonly AssistantSessionStateKey<Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress>> CATEGORY_ANALYSIS_PROGRESS_STATE_KEY = new(nameof(categoryAnalysisProgress));
    private static readonly AssistantSessionStateKey<bool> IS_FINALIZING_ANALYSIS_STATE_KEY = new(nameof(isFinalizingAnalysis));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(SHOW_WEB_CONTENT_READER_STATE_KEY, this.showWebContentReader);
        state.Set(USE_CONTENT_CLEANER_AGENT_STATE_KEY, this.useContentCleanerAgent);
        state.Set(INPUT_TEXT_STATE_KEY, this.inputText);
        state.Set(ANALYSIS_GOAL_STATE_KEY, this.analysisGoal);
        state.Set(IMPORTANT_ASPECTS_STATE_KEY, this.importantAspects);
        state.SetHashSet(CONTEXT_MATERIALS_STATE_KEY, this.contextMaterials);
        state.Set(IS_AGENT_RUNNING_STATE_KEY, this.isAgentRunning);
        state.Set(SELECTED_TARGET_LANGUAGE_STATE_KEY, this.selectedTargetLanguage);
        state.Set(CUSTOM_TARGET_LANGUAGE_STATE_KEY, this.customTargetLanguage);
        state.Set(ANALYSIS_RESULT_STATE_KEY, this.analysisResult);
        state.SetDictionary(CATEGORY_ANALYSIS_PROGRESS_STATE_KEY, this.categoryAnalysisProgress);
        state.Set(IS_FINALIZING_ANALYSIS_STATE_KEY, this.isFinalizingAnalysis);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(SHOW_WEB_CONTENT_READER_STATE_KEY, value => this.showWebContentReader = value);
        state.Restore(USE_CONTENT_CLEANER_AGENT_STATE_KEY, value => this.useContentCleanerAgent = value);
        state.Restore(INPUT_TEXT_STATE_KEY, value => this.inputText = value);
        state.Restore(ANALYSIS_GOAL_STATE_KEY, value => this.analysisGoal = value);
        state.Restore(IMPORTANT_ASPECTS_STATE_KEY, value => this.importantAspects = value);
        state.RestoreHashSet(CONTEXT_MATERIALS_STATE_KEY, this.contextMaterials);
        state.Restore(IS_AGENT_RUNNING_STATE_KEY, value => this.isAgentRunning = value);
        state.Restore(SELECTED_TARGET_LANGUAGE_STATE_KEY, value => this.selectedTargetLanguage = value);
        state.Restore(CUSTOM_TARGET_LANGUAGE_STATE_KEY, value => this.customTargetLanguage = value);
        state.Restore(ANALYSIS_RESULT_STATE_KEY, value => this.analysisResult = value);
        state.RestoreDictionary(CATEGORY_ANALYSIS_PROGRESS_STATE_KEY, this.categoryAnalysisProgress);
        state.Restore(IS_FINALIZING_ANALYSIS_STATE_KEY, value => this.isFinalizingAnalysis = value);
    }

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.TakeDeferredMessages<string>(Event.SEND_TO_SWOT_ANALYSIS_ASSISTANT).LastOrDefault();
        if (deferredContent is not null)
            this.inputText = deferredContent;

        await base.OnInitializedAsync();
    }

    #endregion

    private string? ValidatingText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return T("Please provide source material for the SWOT analysis. You can enter text, load a document, or import content from a website.");

        return null;
    }

    private string? ValidatingAnalysisGoal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return T("Please describe the goal the SWOT analysis should support.");

        return null;
    }

    private string? ValidateCustomLanguage(string language)
    {
        if (this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");

        return null;
    }
}