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

    public void MarkUserDraft()
    {
        this.HasUserDraft = true;
    }

    /// <summary>
    /// Swaps the chat template of the message the user is writing, keeping what the user added.
    /// </summary>
    /// <remarks>
    /// Picking a template is something the user did, so the message counts as theirs from now on.
    /// What the previous template brought goes again, as long as the user left it as it was: its text
    /// while it still stands unchanged at the top, in lines of its own, and its attachments. A template
    /// text the user edited stays, since it is the user's text now. The text of the next template goes
    /// above what the user wrote, and its attachments join those of the user.<br/><br/>
    ///
    /// Attachments are told apart by their files only. A file which the user and the previous template
    /// both attached therefore goes along with the template.
    /// </remarks>
    /// <param name="previousTemplate">The template of the message so far, or ChatTemplate.NO_CHAT_TEMPLATE.</param>
    /// <param name="nextTemplate">The template the user picked, or ChatTemplate.NO_CHAT_TEMPLATE.</param>
    /// <returns>The attachments of the previous template which went, so that the draft of the chat lets them go as well.</returns>
    public IReadOnlyList<FileAttachment> SwapTemplate(ChatTemplate previousTemplate, ChatTemplate nextTemplate)
    {
        var textOfUser = WithoutTemplateText(this.UserInput, previousTemplate.PredefinedUserPrompt);
        var textOfTemplate = nextTemplate.PredefinedUserPrompt;
        if (string.IsNullOrWhiteSpace(textOfTemplate))
            this.UserInput = textOfUser;
        else if (string.IsNullOrWhiteSpace(textOfUser))
            this.UserInput = textOfTemplate;
        else
            this.UserInput = $"{textOfTemplate}\n\n{textOfUser}";

        var pathsOfPreviousTemplate = previousTemplate.FileAttachments.Select(attachment => attachment.FilePath).ToHashSet(StringComparer.Ordinal);
        var removed = this.FileAttachments.Where(attachment => pathsOfPreviousTemplate.Contains(attachment.FilePath)).ToList();
        var kept = this.FileAttachments.Where(attachment => !pathsOfPreviousTemplate.Contains(attachment.FilePath)).ToList();
        var keptPaths = kept.Select(attachment => attachment.FilePath).ToHashSet(StringComparer.Ordinal);
        this.ReplaceFileAttachments([..kept, ..nextTemplate.FileAttachments.Where(attachment => !keptPaths.Contains(attachment.FilePath))]);

        this.HasUserDraft = true;
        return removed;
    }

    /// <summary>
    /// The text of a message without the text of its template at the top, as long as that stands there unchanged.
    /// </summary>
    /// <remarks>
    /// The template text has to fill whole lines: one the user wrote on in counts as edited. The
    /// browser hands back every line break as a line feed, while a template may hold others.
    /// </remarks>
    /// <param name="text">The text of the message.</param>
    /// <param name="textOfTemplate">The text of its template.</param>
    /// <returns>The text below the template text, or the whole text when the template text is not there as it was.</returns>
    private static string WithoutTemplateText(string text, string textOfTemplate)
    {
        if (string.IsNullOrWhiteSpace(textOfTemplate))
            return text;

        var message = text.ReplaceLineEndings("\n");
        var template = textOfTemplate.ReplaceLineEndings("\n");
        if (!message.StartsWith(template, StringComparison.Ordinal))
            return text;

        var rest = message[template.Length..];
        return rest.Length is 0 || rest[0] is '\n' || template[^1] is '\n'
            ? rest.TrimStart('\n')
            : text;
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

    /// <summary>
    /// Brings the message the user had not sent yet back into the composer.
    /// </summary>
    /// <remarks>
    /// The draft holds only what the user wrote or attached, so whatever it holds counts as their
    /// message. An empty draft leaves the composer free for a chat template.
    /// </remarks>
    /// <param name="draft">The draft of the chat.</param>
    public void RestoreFromDraft(ChatDraft draft)
    {
        this.UserInput = draft.Text;
        this.ReplaceFileAttachments(draft.FileAttachments);
        this.HasUserDraft = !draft.IsEmpty;
    }
}