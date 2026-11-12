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
}