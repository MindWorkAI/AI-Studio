using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.MyTasks;

public partial class AssistantMyTasks : AssistantBaseCore<SettingsDialogMyTasks>
{
    protected override Tools.Components Component => Tools.Components.MY_TASKS_ASSISTANT;
    
    protected override string Title => T("My Tasks");
    
    protected override string Description => T("You received a cryptic email or document that was sent to many recipients and you are now wondering if you need to do something? Copy the text into the input field, attach one or more documents, or use both. You also need to select a personal profile. In this profile, you should describe your role in the organization. The AI will then try to give you hints on what your tasks might be.");
    
    protected override string SystemPrompt => 
        $"""
        You are a friendly and professional business expert. You receive business emails, protocols,
        reports, etc. as text input and/or attached documents. Additionally, you know the user's role
        in the organization. The user wonders if any tasks arise for them in their role based on the
        provided content. You now try to give hints and advice on whether and what the user should do.
        When you believe there are no tasks for the user, you tell them this. You consider typical
        business etiquette in your advice.
        
        You write your advice in the following language: {this.SystemPromptLanguage()}.
        """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Analyze content");

    protected override Func<Task> SubmitAction => this.AnalyzeText;

    protected override bool ShowProfileSelection => false;
    
    protected override string SendToChatVisibleUserPromptPrefix => T("Analyze the following text and/or attached documents and extract my tasks:");

    protected override string SendToChatVisibleUserPromptContent => this.inputText;

    protected override ChatThread ConvertToChatThread
    {
        get
        {
            var originalChatThread = this.ChatThread ?? new ChatThread();
            if (string.IsNullOrWhiteSpace(this.SendToChatVisibleUserPromptText))
            {
                return originalChatThread with
                {
                    SystemPrompt = SystemPrompts.DEFAULT,
                };
            }

            var earliestBlock = originalChatThread.Blocks.MinBy(x => x.Time);
            var visiblePromptTime = earliestBlock is null
                ? DateTimeOffset.Now
                : earliestBlock.Time == DateTimeOffset.MinValue
                    ? earliestBlock.Time
                    : earliestBlock.Time.AddTicks(-1);

            var transferredBlocks = originalChatThread.Blocks
                .Select(block => block.Role is ChatRole.USER
                    ? this.CloneHiddenUserBlockWithoutAttachments(block)
                    : block.DeepClone())
                .ToList();

            transferredBlocks.Insert(0, new ContentBlock
            {
                Time = visiblePromptTime,
                ContentType = ContentType.TEXT,
                HideFromUser = false,
                Role = ChatRole.USER,
                Content = new ContentText
                {
                    Text = this.SendToChatVisibleUserPromptText,
                    FileAttachments = this.loadedDocumentPaths.ToList(),
                },
            });

            return originalChatThread with
            {
                ChatId = Guid.NewGuid(),
                SystemPrompt = SystemPrompts.DEFAULT,
                Blocks = transferredBlocks,
            };
        }
    }

    protected override void ResetForm()
    {
        this.inputText = string.Empty;
        this.loadedDocumentPaths.Clear();
        if (!this.MightPreselectValues())
        {
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.MyTasks.PreselectOptions)
        {
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.MyTasks.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.MyTasks.PreselectOtherLanguage;
            return true;
        }
        
        return false;
    }

    private string inputText = string.Empty;
    private HashSet<FileAttachment> loadedDocumentPaths = [];
    private CommonLanguages selectedTargetLanguage = CommonLanguages.AS_IS;
    private string customTargetLanguage = string.Empty;
    private static readonly AssistantSessionStateKey<string> INPUT_TEXT_STATE_KEY = new(nameof(inputText));
    private static readonly AssistantSessionStateKey<HashSet<FileAttachment>> LOADED_DOCUMENT_PATHS_STATE_KEY = new(nameof(loadedDocumentPaths));
    private static readonly AssistantSessionStateKey<CommonLanguages> SELECTED_TARGET_LANGUAGE_STATE_KEY = new(nameof(selectedTargetLanguage));
    private static readonly AssistantSessionStateKey<string> CUSTOM_TARGET_LANGUAGE_STATE_KEY = new(nameof(customTargetLanguage));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(INPUT_TEXT_STATE_KEY, this.inputText);
        state.SetHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
        state.Set(SELECTED_TARGET_LANGUAGE_STATE_KEY, this.selectedTargetLanguage);
        state.Set(CUSTOM_TARGET_LANGUAGE_STATE_KEY, this.customTargetLanguage);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(INPUT_TEXT_STATE_KEY, value => this.inputText = value);
        state.RestoreHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
        state.Restore(SELECTED_TARGET_LANGUAGE_STATE_KEY, value => this.selectedTargetLanguage = value);
        state.Restore(CUSTOM_TARGET_LANGUAGE_STATE_KEY, value => this.customTargetLanguage = value);
    }
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_MY_TASKS_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputText = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private string? ValidatingText(string text)
    {
        if(string.IsNullOrWhiteSpace(text) && !this.HasValidInputDocuments())
            return T("Please provide some text or at least one valid document as input. For example, an email.");
        
        return null;
    }

    private bool HasValidInputDocuments() => this.loadedDocumentPaths.Any(n => n is { Exists: true, IsValid: true });

    private async Task OnDocumentsChanged(HashSet<FileAttachment> _)
    {
        if(this.Form is not null)
            await this.Form.Validate();
    }

    private string? ValidateProfile(Profile profile)
    {
        if(profile == Profile.NO_PROFILE)
            return T("Please select one of your profiles.");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }
    
    private string SystemPromptLanguage()
    {
        if(this.selectedTargetLanguage is CommonLanguages.AS_IS)
            return "Use the same language as the input";
        
        if(this.selectedTargetLanguage is CommonLanguages.OTHER)
            return this.customTargetLanguage;
        
        return this.selectedTargetLanguage.Name();
    }

    private ContentBlock CloneHiddenUserBlockWithoutAttachments(ContentBlock block)
    {
        var clone = block.DeepClone(changeHideState: true);
        if (clone.Content is ContentText text)
            text.FileAttachments = [];

        return clone;
    }

    private string BuildUserRequest()
    {
        if(!string.IsNullOrWhiteSpace(this.inputText))
            return this.inputText;

        return "Analyze the attached document(s) and extract my tasks.";
    }
    
    private async Task AnalyzeText()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(this.BuildUserRequest(), false, this.loadedDocumentPaths.ToList());

        await this.AddAIResponseAsync(time);
    }
}
