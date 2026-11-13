using System.Text;

using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantLaunch;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Assistants.Coding;

public partial class AssistantCoding : AssistantBaseCore<SettingsDialogCoding>
{
    protected override Tools.Components Component => Tools.Components.CODING_ASSISTANT;
    
    protected override string Title => T("Coding Assistant");
    
    protected override string Description => TA("This coding assistant supports you in writing code. Ask your coding question and optionally attach source files as context. When you have compiler messages, you can paste them into the input fields to get help with debugging as well.");
    
    // This prompt describes a developer for any coding question, which suits the follow-up questions of the chat:
    protected override ChatSystemPromptChoice ChatSystemPrompt => ChatSystemPromptChoice.ASSISTANT;

    protected override string SystemPrompt =>
        """
        You are a friendly, helpful senior software developer with extensive experience in various programming languages
        and concepts. You are familiar with principles like DRY, KISS, YAGNI, and SOLID and can apply and explain them.
        You know object-oriented programming, as well as functional programming and procedural programming. You are also
        familiar with design patterns and can explain them. You are an expert of debugging and can help with compiler
        messages. You can also help with code refactoring and optimization.

        The user may attach source files, project files, configuration files, logs, or other documents as coding context.
        Treat attached files as source context for the user's question. Use the file paths and file contents provided in
        the message to reason about the code. Do not invent files or APIs that are not present in the user's question or
        attached context. If the question conflicts with attached context, prioritize the user's explicit question and
        explain any relevant mismatch.
        
        When the user asks in a different language than English, you answer in the same language!
        """;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Get Support");

    protected override Func<Task> SubmitAction => this.GetSupport;

    protected override string SendToChatVisibleUserPromptPrefix => T("Help me with the following coding question:");

    protected override string SendToChatVisibleUserPromptContent => this.questions;

    protected override ChatThread ConvertToChatThread
    {
        get
        {
            var originalChatThread = this.ChatThread ?? new ChatThread();
            if (string.IsNullOrWhiteSpace(this.SendToChatVisibleUserPromptText))
                return originalChatThread;

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
                    Text = this.BuildVisibleChatPrompt(),
                    FileAttachments = this.loadedDocumentPaths.ToList(),
                },
            });

            return originalChatThread with
            {
                ChatId = Guid.NewGuid(),
                Name = T("Coding Assistant Session"),
                Blocks = transferredBlocks,
            };
        }
    }

    protected override void ResetForm()
    {
        this.loadedDocumentPaths.Clear();
        this.compilerMessages = string.Empty;
        this.questions = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.provideCompilerMessages = false;
        }
    }

    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.Coding.PreselectOptions)
        {
            this.provideCompilerMessages = this.SettingsManager.ConfigurationData.Coding.PreselectCompilerMessages;
            return true;
        }
        
        return false;
    }
    
    private HashSet<FileAttachment> loadedDocumentPaths = [];
    private bool provideCompilerMessages;
    private string compilerMessages = string.Empty;
    private string questions = string.Empty;
    private static readonly AssistantSessionStateKey<HashSet<FileAttachment>> LOADED_DOCUMENT_PATHS_STATE_KEY = new(nameof(loadedDocumentPaths));
    private static readonly AssistantSessionStateKey<bool> PROVIDE_COMPILER_MESSAGES_STATE_KEY = new(nameof(provideCompilerMessages));
    private static readonly AssistantSessionStateKey<string> COMPILER_MESSAGES_STATE_KEY = new(nameof(compilerMessages));
    private static readonly AssistantSessionStateKey<string> QUESTIONS_STATE_KEY = new(nameof(questions));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.SetHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
        state.Set(PROVIDE_COMPILER_MESSAGES_STATE_KEY, this.provideCompilerMessages);
        state.Set(COMPILER_MESSAGES_STATE_KEY, this.compilerMessages);
        state.Set(QUESTIONS_STATE_KEY, this.questions);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.RestoreHashSet(LOADED_DOCUMENT_PATHS_STATE_KEY, this.loadedDocumentPaths);
        state.Restore(PROVIDE_COMPILER_MESSAGES_STATE_KEY, value => this.provideCompilerMessages = value);
        state.Restore(COMPILER_MESSAGES_STATE_KEY, value => this.compilerMessages = value);
        state.Restore(QUESTIONS_STATE_KEY, value => this.questions = value);
    }

    #region Overrides of AssistantBase

    protected override void ApplyTextLaunchPayload(TextLaunchPayload payload) => this.questions = payload.Text;

    #endregion

    private string? ValidatingCompilerMessages(string checkCompilerMessages)
    {
        if(!this.provideCompilerMessages)
            return null;
        
        if(string.IsNullOrWhiteSpace(checkCompilerMessages))
            return TA("Please provide the compiler messages.");
        
        return null;
    }
    
    private string? ValidateQuestions(string checkQuestions)
    {
        if(string.IsNullOrWhiteSpace(checkQuestions))
            return TA("Please provide your questions.");
        
        return null;
    }

    private ContentBlock CloneHiddenUserBlockWithoutAttachments(ContentBlock block)
    {
        var clone = block.DeepClone(changeHideState: true);
        if (clone.Content is ContentText text)
            text.FileAttachments = [];

        return clone;
    }

    private string BuildVisibleChatPrompt()
    {
        if (!this.provideCompilerMessages)
            return this.SendToChatVisibleUserPromptText ?? string.Empty;

        return $"""
                I have the following compiler messages:

                ```
                {this.compilerMessages}
                ```

                My questions are:
                {this.questions}
                """;
    }

    private async Task GetSupport()
    {
        await this.Form!.ValidateAsync();
        if (!this.InputIsValid)
            return;

        var sbCompilerMessages = new StringBuilder();
        if (this.provideCompilerMessages)
        {
            sbCompilerMessages.AppendLineLF("I have the following compiler messages:");
            sbCompilerMessages.AppendLineLF();
            sbCompilerMessages.AppendLineLF("```");
            sbCompilerMessages.AppendLineLF(this.compilerMessages);
            sbCompilerMessages.AppendLineLF("```");
            sbCompilerMessages.AppendLineLF();
        }

        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                {sbCompilerMessages}
                
                My questions are:
                {this.questions}
             """,
            false,
            this.loadedDocumentPaths.ToList());

        await this.AddAIResponseAsync(time);
    }
}