using AIStudio.Chat;
using AIStudio.Tools.RAG;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks what a block keeps when it leaves its chat, e.g., for the example conversation of a chat template.
/// </summary>
/// <remarks>
/// What a chat received lies in the provenance store and is deleted with the chat. A template made
/// from a chat, and every chat started from that template, must not refer to it: they would lose
/// their passages and attachments as soon as the first chat is deleted.
/// </remarks>
[TestFixture]
public sealed class ContentBlockApartFromChatTests
{
    [Test]
    public void ABlockApartFromItsChatRefersToNothingOfItsStore()
    {
        var chat = new ChatThread { ChatId = Guid.NewGuid(), Blocks = [Question(Guid.NewGuid()), Answer(Guid.NewGuid())] };

        var template = new ChatThread { ChatId = Guid.NewGuid(), Blocks = chat.Blocks.Select(block => block.CloneApartFromChat()).ToList() };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(template.CollectProvenanceIds(), Is.Empty, "The entries belong to the chat and are deleted with it.");
            Assert.That(template.Blocks.Select(block => ((ContentText)block.Content!).Text), Is.EqualTo(new[] { "Summarize the minutes.", "The board met on Monday." }));
            Assert.That(template.Blocks[0].Content!.FileAttachments.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { "/tmp/minutes.txt" }), "The file is read anew by every chat it reaches.");
            Assert.That(chat.CollectProvenanceIds(), Has.Count.EqualTo(2), "The chat itself keeps what it received.");
        }
    }

    [Test]
    public void ABlockApartFromItsChatCanBeHidden()
    {
        var visible = new ContentBlock { ContentType = ContentType.TEXT, Role = ChatRole.USER, Content = new ContentText { Text = "Hello" } };

        Assert.That(visible.CloneApartFromChat(true).HideFromUser, Is.True, "A template made from a chat hides its blocks, as before.");
    }

    private static ContentBlock Question(Guid attachmentId) => new()
    {
        ContentType = ContentType.TEXT,
        Role = ChatRole.USER,
        Content = new ContentText
        {
            Text = "Summarize the minutes.",
            FileAttachments = [new FileAttachment(FileAttachmentType.DOCUMENT, "minutes.txt", "/tmp/minutes.txt", 24) { ProvenanceId = attachmentId }],
        },
    };

    private static ContentBlock Answer(Guid passageId) => new()
    {
        ContentType = ContentType.TEXT,
        Role = ChatRole.AI,
        Content = new ContentText
        {
            Text = "The board met on Monday.",
            SourcePassages = [new SourcePassage { SourceUrl = "file:///C:/docs/minutes.pdf", ProvenanceId = passageId }],
        },
    };
}