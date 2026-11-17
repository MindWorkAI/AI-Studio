using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the images of the material go along with a request of the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The images belong to the request only: the stored chat must never hold a path to them, the answer streams into
/// the chat while the request runs, and the tokens a provider reports belong to a chat with as many messages.
/// </remarks>
[TestFixture]
public sealed class BflRequestImageTests
{
    [Test]
    public void TheImagesGoAlongWithTheFirstMessage()
    {
        var thread = Conversation();
        var image = new FileAttachmentImage("Poster.png", "/data/beyondFactsLab/p/materials/m.png", 1_024);

        var request = BflConversationService.WithImages(thread, [image]);
        var firstMessage = (ContentText)request.Blocks[0].Content!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstMessage.FileAttachments, Is.EqualTo(new FileAttachment[] { image }));
            Assert.That(firstMessage.Text, Is.EqualTo("Eröffnung"));
            Assert.That(request.Blocks[0].HideFromUser, Is.True, "The opening request stays hidden.");
            Assert.That(request.ChatId, Is.EqualTo(thread.ChatId));
        }
    }

    [Test]
    public void TheStoredChatHoldsNoImage()
    {
        var thread = Conversation();

        BflConversationService.WithImages(thread, [new FileAttachmentImage("Poster.png", "/data/m.png", 1_024)]);

        Assert.That(thread.Blocks.Select(block => (block.Content as ContentText)?.FileAttachments.Count ?? 0), Is.All.Zero);
    }

    [Test]
    public void EveryOtherMessageStaysTheSame()
    {
        var thread = Conversation();

        var request = BflConversationService.WithImages(thread, [new FileAttachmentImage("Poster.png", "/data/m.png", 1_024)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request.Blocks, Has.Count.EqualTo(thread.Blocks.Count), "The tokens a provider reports would no longer belong to the chat.");
            Assert.That(request.Blocks[0], Is.Not.SameAs(thread.Blocks[0]));
            for (var index = 1; index < thread.Blocks.Count; index++)
                Assert.That(request.Blocks[index], Is.SameAs(thread.Blocks[index]), "The answer must stream into the chat itself.");
        }
    }

    [Test]
    public void WithoutImagesTheRequestIsTheChat()
    {
        var thread = Conversation();

        Assert.That(BflConversationService.WithImages(thread, []), Is.SameAs(thread));
    }

    /// <summary>
    /// A conversation of a step: the hidden opening request, an answer, a message of the person, and the answer which
    /// streams in.
    /// </summary>
    private static ChatThread Conversation() => new()
    {
        ChatId = Guid.NewGuid(),
        Blocks =
        [
            new() { Role = ChatRole.USER, ContentType = ContentType.TEXT, Content = new ContentText { Text = "Eröffnung" }, HideFromUser = true },
            new() { Role = ChatRole.AI, ContentType = ContentType.TEXT, Content = new ContentText { Text = "Worum geht es?" } },
            new() { Role = ChatRole.USER, ContentType = ContentType.TEXT, Content = new ContentText { Text = "Ein Podium." } },
            new() { Role = ChatRole.AI, ContentType = ContentType.TEXT, Content = new ContentText { InitialRemoteWait = true } },
        ],
    };
}