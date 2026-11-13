using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks what the composer counts as a message the user started.
/// </summary>
/// <remarks>
/// That decides whether a chat template may still fill the composer, and whether leaving the chat
/// warns about unsaved changes. A draft brought back from a chat is always the user's own, while a
/// chat template only suggests a message.
/// </remarks>
[TestFixture]
public sealed class ChatComposerStateTests
{
    [Test]
    public void ADraftBroughtBackIsTheMessageOfTheUser()
    {
        var draft = new ChatDraft();
        draft.SetText("Summarize the meeting");
        draft.AddAttachments([new FileAttachment(FileAttachmentType.DOCUMENT, "agenda.pdf", "/documents/agenda.pdf", 1024)]);
        var composer = new ChatComposerState();

        composer.RestoreFromDraft(draft);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composer.UserInput, Is.EqualTo("Summarize the meeting"));
            Assert.That(composer.FileAttachments.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { "/documents/agenda.pdf" }));
            Assert.That(composer.HasUserDraft, Is.True, "A chat template must not replace the message the user came back to.");
        }
    }

    [Test]
    public void AnEmptyDraftLeavesTheComposerFree()
    {
        var composer = new ChatComposerState();
        composer.SetUserInput("A message of the chat before");

        composer.RestoreFromDraft(new ChatDraft());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composer.UserInput, Is.Empty, "The message of the chat before belongs to that chat.");
            Assert.That(composer.HasUserDraft, Is.False, "Nobody started a message in this chat, so a chat template may still fill the composer.");
        }
    }

    [Test]
    public void TheMessageOfAChatTemplateIsNoDraftOfTheUser()
    {
        var composer = new ChatComposerState();

        composer.ApplyTemplate(new ChatTemplate { PredefinedUserPrompt = "Translate the following text:" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composer.UserInput, Is.EqualTo("Translate the following text:"));
            Assert.That(composer.HasUserDraft, Is.False, "Leaving the chat must not warn about a message the user never wrote.");
        }
    }

    [Test]
    public void APickedTemplateKeepsWhatTheUserAdded()
    {
        var composer = new ChatComposerState();
        composer.SetUserInput("Here are my notes.");
        composer.ReplaceFileAttachments([Attachment("notes.pdf")]);
        var template = new ChatTemplate { PredefinedUserPrompt = "Summarize the following:", FileAttachments = [Attachment("agenda.pdf")] };

        var removed = composer.SwapTemplate(ChatTemplate.NO_CHAT_TEMPLATE, template);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composer.UserInput, Is.EqualTo("Summarize the following:\n\nHere are my notes."), "The text of the template goes above the text of the user.");
            Assert.That(PathsOf(composer), Is.EquivalentTo(new[] { "/documents/notes.pdf", "/documents/agenda.pdf" }), "The files of the template join those of the user.");
            Assert.That(removed, Is.Empty);
            Assert.That(composer.HasUserDraft, Is.True, "Picking a template is something the user did.");
        }
    }

    [Test]
    public void SwitchingTemplatesTakesBackOnlyWhatThePreviousTemplateBrought()
    {
        var composer = new ChatComposerState();
        var previousTemplate = new ChatTemplate { PredefinedUserPrompt = "Summarize the following:", FileAttachments = [Attachment("agenda.pdf")] };
        var nextTemplate = new ChatTemplate { PredefinedUserPrompt = "Translate the following text:", FileAttachments = [Attachment("glossary.pdf")] };
        composer.ApplyTemplate(previousTemplate);
        composer.SetUserInput("Summarize the following:\n\nHere are my notes.");
        composer.ReplaceFileAttachments([..composer.FileAttachments, Attachment("notes.pdf")]);

        var removed = composer.SwapTemplate(previousTemplate, nextTemplate);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composer.UserInput, Is.EqualTo("Translate the following text:\n\nHere are my notes."));
            Assert.That(PathsOf(composer), Is.EquivalentTo(new[] { "/documents/notes.pdf", "/documents/glossary.pdf" }));
            Assert.That(removed.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { "/documents/agenda.pdf" }), "The draft of the chat has to let the file go as well.");
        }
    }

    [Test]
    public void ATemplateTextTheUserEditedStays()
    {
        var composer = new ChatComposerState();
        var previousTemplate = new ChatTemplate { PredefinedUserPrompt = "Summarize the following" };
        var nextTemplate = new ChatTemplate { PredefinedUserPrompt = "Translate the following text:" };
        composer.ApplyTemplate(previousTemplate);
        composer.SetUserInput("Summarize the following in three sentences:\n\nHere are my notes.");

        composer.SwapTemplate(previousTemplate, nextTemplate);

        Assert.That(composer.UserInput, Is.EqualTo("Translate the following text:\n\nSummarize the following in three sentences:\n\nHere are my notes."), "A line the user wrote on in is the user's text now.");
    }

    [Test]
    public void PickingNoTemplateTakesTheTemplateBack()
    {
        var composer = new ChatComposerState();
        var previousTemplate = new ChatTemplate { PredefinedUserPrompt = "Summarize the following\r\nin three sentences:", FileAttachments = [Attachment("agenda.pdf")] };
        composer.ApplyTemplate(previousTemplate);

        // The browser hands back every line break as a line feed:
        composer.SetUserInput("Summarize the following\nin three sentences:\n\nHere are my notes.");

        composer.SwapTemplate(previousTemplate, ChatTemplate.NO_CHAT_TEMPLATE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(composer.UserInput, Is.EqualTo("Here are my notes."));
            Assert.That(composer.FileAttachments, Is.Empty);
        }
    }

    private static FileAttachment Attachment(string fileName) => new(FileAttachmentType.DOCUMENT, fileName, $"/documents/{fileName}", 1024);

    private static IEnumerable<string> PathsOf(ChatComposerState composer) => composer.FileAttachments.Select(attachment => attachment.FilePath);
}