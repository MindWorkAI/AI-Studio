using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Components;

public sealed class ChatComposerState
{
    public string UserInput { get; private set; } = string.Empty;

    public HashSet<FileAttachment> FileAttachments { get; } = [];

    public bool HasUserDraft { get; private set; }

    public bool HasComposerContent => !string.IsNullOrWhiteSpace(this.UserInput) || this.FileAttachments.Count > 0;

    public bool HasVisibleUserDraft => this.HasUserDraft && (!string.IsNullOrWhiteSpace(this.UserInput) || this.FileAttachments.Count > 0);

    public void ApplyTemplate(ChatTemplate chatTemplate)
    {
        this.UserInput = chatTemplate.PredefinedUserPrompt;
        this.FileAttachments.Clear();
        foreach (var attachment in chatTemplate.FileAttachments)
            this.FileAttachments.Add(attachment.Normalize());

        this.HasUserDraft = false;
    }

    public void SetUserInput(string? userInput)
    {
        this.UserInput = userInput ?? string.Empty;
        this.HasUserDraft = !string.IsNullOrWhiteSpace(userInput);
    }

    public void SetSystemInput(string? userInput)
    {
        this.UserInput = userInput ?? string.Empty;
        this.HasUserDraft = false;
    }

    public void MarkUserDraft()
    {
        this.HasUserDraft = true;
    }

    public void ReplaceFileAttachments(IEnumerable<FileAttachment> fileAttachments)
    {
        this.FileAttachments.Clear();
        foreach (var attachment in fileAttachments)
            this.FileAttachments.Add(attachment.Normalize());
    }

    public void Clear()
    {
        this.UserInput = string.Empty;
        this.FileAttachments.Clear();
        this.HasUserDraft = false;
    }

    public void RestoreFromTextBlock(ContentText textBlock)
    {
        this.UserInput = textBlock.Text;
        this.ReplaceFileAttachments(textBlock.FileAttachments);
        this.HasUserDraft = true;
    }

    public void Restore(string? userInput, IEnumerable<FileAttachment> fileAttachments, bool hasUserDraft)
    {
        this.UserInput = userInput ?? string.Empty;
        this.ReplaceFileAttachments(fileAttachments);
        this.HasUserDraft = hasUserDraft;
    }
}
