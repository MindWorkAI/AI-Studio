using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.LegalCheck;

public partial class AssistantLegalCheck : AssistantBaseCore<SettingsDialogLegalCheck>
{
    protected override Tools.Components Component => Tools.Components.LEGAL_CHECK_ASSISTANT;
    
    protected override string Title => T("Legal Check");
    
    protected override string Description => T("Provide a legal document and ask a question about it. This assistant does not replace legal advice. Consult a lawyer to get professional advice. Remember that LLMs can invent answers and facts. Please do not rely on this answers.");
    
    protected override string SystemPrompt => 
        """
        You are an expert in legal matters. You have studied international law and are familiar with the law in
        various countries. You receive a legal document and answer the user's questions. You are a friendly and
        professional assistant. You respond to the user in the language in which the questions were asked. If
        you are unsure, unfamiliar with the legal area, or do not know the answers, you inform the user.
        Never invent facts!
        """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Ask your questions");

    protected override Func<Task> SubmitAction => this.AskQuestions;

    protected override bool SubmitDisabled => this.isAgentRunning;

    protected override string SendToChatVisibleUserPromptPrefix => T("Answer the following questions about a legal document:");

    protected override string SendToChatVisibleUserPromptContent => this.inputQuestions;
    
    protected override void ResetForm()
    {
        this.inputLegalDocument = string.Empty;
        this.inputQuestions = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.showWebContentReader = false;
            this.useContentCleanerAgent = false;
        }
    }

    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.LegalCheck.PreselectOptions)
        {
            this.showWebContentReader = this.SettingsManager.ConfigurationData.LegalCheck.PreselectWebContentReader;
            this.useContentCleanerAgent = this.SettingsManager.ConfigurationData.LegalCheck.PreselectContentCleanerAgent;
            return true;
        }
        
        return false;
    }

    private bool showWebContentReader;
    private bool useContentCleanerAgent;
    private bool isAgentRunning;
    private string inputLegalDocument = string.Empty;
    private string inputQuestions = string.Empty;
    private static readonly AssistantSessionStateKey<bool> SHOW_WEB_CONTENT_READER_STATE_KEY = new(nameof(showWebContentReader));
    private static readonly AssistantSessionStateKey<bool> USE_CONTENT_CLEANER_AGENT_STATE_KEY = new(nameof(useContentCleanerAgent));
    private static readonly AssistantSessionStateKey<bool> IS_AGENT_RUNNING_STATE_KEY = new(nameof(isAgentRunning));
    private static readonly AssistantSessionStateKey<string> INPUT_LEGAL_DOCUMENT_STATE_KEY = new(nameof(inputLegalDocument));
    private static readonly AssistantSessionStateKey<string> INPUT_QUESTIONS_STATE_KEY = new(nameof(inputQuestions));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(SHOW_WEB_CONTENT_READER_STATE_KEY, this.showWebContentReader);
        state.Set(USE_CONTENT_CLEANER_AGENT_STATE_KEY, this.useContentCleanerAgent);
        state.Set(IS_AGENT_RUNNING_STATE_KEY, this.isAgentRunning);
        state.Set(INPUT_LEGAL_DOCUMENT_STATE_KEY, this.inputLegalDocument);
        state.Set(INPUT_QUESTIONS_STATE_KEY, this.inputQuestions);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(SHOW_WEB_CONTENT_READER_STATE_KEY, value => this.showWebContentReader = value);
        state.Restore(USE_CONTENT_CLEANER_AGENT_STATE_KEY, value => this.useContentCleanerAgent = value);
        state.Restore(IS_AGENT_RUNNING_STATE_KEY, value => this.isAgentRunning = value);
        state.Restore(INPUT_LEGAL_DOCUMENT_STATE_KEY, value => this.inputLegalDocument = value);
        state.Restore(INPUT_QUESTIONS_STATE_KEY, value => this.inputQuestions = value);
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_LEGAL_CHECK_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputQuestions = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string? ValidatingLegalDocument(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return T("Please provide a legal document as input. You might copy the desired text from a document or a website.");
        
        return null;
    }
    
    private string? ValidatingQuestions(string text)
    {
        if(string.IsNullOrWhiteSpace(text))
            return T("Please provide your questions as input.");
        
        return null;
    }
    
    private async Task AskQuestions()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                # The legal document
                {this.inputLegalDocument}
                
                # The questions
                {this.inputQuestions}
             """);

        await this.AddAIResponseAsync(time);
    }
}