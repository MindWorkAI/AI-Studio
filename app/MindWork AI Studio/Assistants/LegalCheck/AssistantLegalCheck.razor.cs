using AIStudio.Chat;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.LegalCheck;

public partial class AssistantLegalCheck : AssistantBaseCore<SettingsDialogLegalCheck>
{
    public override Tools.Components Component => Tools.Components.LEGAL_CHECK_ASSISTANT;
    
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

    protected override Func<Task> SubmitAction => this.AksQuestions;

    protected override bool SubmitDisabled => this.isAgentRunning;
    
    protected override ChatThread ConvertToChatThread => (this.chatThread ?? new()) with
    {
        SystemPrompt = SystemPrompts.DEFAULT,
    };
    
    protected override void ResetForm()
    {
        this.inputLegalDocument = string.Empty;
        this.inputQuestions = string.Empty;
        this.MightPreselectValues();
    }
    
    protected override bool MightPreselectValues() => false;

    private bool isAgentRunning;
    private string inputLegalDocument = string.Empty;
    private string inputQuestions = string.Empty;
    
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
    
    private async Task AksQuestions()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
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