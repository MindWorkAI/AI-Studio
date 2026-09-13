using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks what a conversation is counted as costing before it is sent.
/// </summary>
/// <remarks>
/// The number under the input field used to count the sentence being typed and nothing else, which
/// answers a question nobody asks: what decides whether the next message fits is everything that
/// travels with it. So what is collected here has to be what the message builder actually sends --
/// no more, because a number which counts something that stays behind is wrong in the direction
/// that makes a person stop writing.
/// </remarks>
[TestFixture]
public sealed class ConversationPartsTests
{
    private string directory = string.Empty;

    [SetUp]
    public void CreateFiles()
    {
        this.directory = Path.Combine(Path.GetTempPath(), $"ai-studio-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.directory);
    }

    [TearDown]
    public void RemoveFiles()
    {
        if (Directory.Exists(this.directory))
            Directory.Delete(this.directory, true);
    }

    [Test]
    public void TheWholeConversationCountsAndNotOnlyWhatIsBeingTyped()
    {
        var thread = new ChatThread
        {
            SystemPrompt = "You are helpful.",
            Blocks =
            [
                Block("What is the capital of France?"),
                Block("Paris."),
            ],
        };

        var parts = ConversationParts.Of(thread, "You are helpful.", "And of Italy?", null, imagesAreSent: true);

        Assert.Multiple(() =>
        {
            Assert.That(parts.Texts, Is.EqualTo(new[] { "You are helpful.", "What is the capital of France?", "Paris." }));
            Assert.That(parts.GrowingTexts, Is.EqualTo(new[] { "And of Italy?" }));
        });
    }

    [Test]
    public void WhatIsStillBeingWrittenIsKeptApartFromWhatStands()
    {
        //
        // Both cost the same and both are counted. They are kept apart because of what happens
        // afterwards: a message which stands says the same thing forever and its count is worth
        // remembering, while the answer being streamed is a different text three seconds later.
        //
        var streaming = Block("The answer so far");
        ((ContentText)streaming.Content!).IsStreaming = true;
        var thread = new ChatThread { Blocks = [Block("A question."), streaming] };

        var parts = ConversationParts.Of(thread, string.Empty, "a draft", null, imagesAreSent: true);

        Assert.Multiple(() =>
        {
            Assert.That(parts.Texts, Is.EqualTo(new[] { "A question." }));
            Assert.That(parts.GrowingTexts, Is.EqualTo(new[] { "The answer so far", "a draft" }));
        });
    }

    [Test]
    public void AnAnswerWhichIsFinishedStandsLikeAnyOtherMessage()
    {
        var finished = Block("The whole answer.");
        ((ContentText)finished.Content!).IsStreaming = false;

        var parts = ConversationParts.Of(new() { Blocks = [finished] }, string.Empty, string.Empty, null, imagesAreSent: true);

        Assert.Multiple(() =>
        {
            Assert.That(parts.Texts, Is.EqualTo(new[] { "The whole answer." }));
            Assert.That(parts.GrowingTexts, Is.Empty);
        });
    }

    [Test]
    public void TheSystemPromptCountedIsTheOneWhichWouldBeSent()
    {
        //
        // Not the one standing in the thread. A chat template may replace it, retrieved data is
        // appended to it, a profile adds a paragraph and the tool policy adds another -- and
        // switching a profile while writing has to move the number, which it cannot do if the
        // thread's own field is what gets counted.
        //
        var thread = new ChatThread { SystemPrompt = "What the person typed." };

        var parts = ConversationParts.Of(thread, "What the request carries.", string.Empty, null, imagesAreSent: true);

        Assert.That(parts.Texts, Is.EqualTo(new[] { "What the request carries." }));
    }

    [Test]
    public void ABlockHiddenFromTheUserStillCosts()
    {
        //
        // Hidden on the screen, not in the request: the message builder sends it like any other
        // block, so its tokens are gone whether or not anybody can see where they went.
        //
        var hidden = Block("An instruction the user does not see.");
        var thread = new ChatThread { Blocks = [new() { ContentType = hidden.ContentType, Role = hidden.Role, Content = hidden.Content, HideFromUser = true }] };

        var parts = ConversationParts.Of(thread, string.Empty, string.Empty, null, imagesAreSent: true);

        Assert.That(parts.Texts, Is.EqualTo(new[] { "An instruction the user does not see." }));
    }

    [Test]
    public void WithoutAConversationOnlyTheDraftCounts()
    {
        var parts = ConversationParts.Of(null, string.Empty, "Hello", null, imagesAreSent: true);

        Assert.Multiple(() =>
        {
            Assert.That(parts.Texts, Is.Empty);
            Assert.That(parts.GrowingTexts, Is.EqualTo(new[] { "Hello" }));
        });
    }

    [TestCase("")]
    [TestCase("   ")]
    public void NothingWrittenIsNothingToCount(string draft)
    {
        var parts = ConversationParts.Of(null, string.Empty, draft, null, imagesAreSent: true);

        Assert.Multiple(() =>
        {
            Assert.That(parts.Texts, Is.Empty);
            Assert.That(parts.GrowingTexts, Is.Empty);
        });
    }

    [Test]
    public void ABlockWithoutTextIsSkippedBecauseItIsNeverSent()
    {
        //
        // The message builder drops a block whose text is empty, whatever else hangs off it. A
        // count which added that block's attachments would report tokens for a message which is
        // never built.
        //
        var document = this.WriteFile("notes.txt", "some content");
        var empty = Block(string.Empty);
        ((ContentText)empty.Content!).FileAttachments.Add(FileAttachment.FromPath(document));

        var parts = ConversationParts.Of(new() { Blocks = [empty] }, string.Empty, string.Empty, null, imagesAreSent: true);

        Assert.Multiple(() =>
        {
            Assert.That(parts.Texts, Is.Empty);
            Assert.That(parts.Documents, Is.Empty);
        });
    }

    [Test]
    public void AttachmentsOfTheConversationAndOfTheComposerBothCount()
    {
        //
        // A document attached three messages ago is sent again with every further message, so it
        // costs its tokens again every time. That is exactly the thing a person cannot see and
        // which this number is for.
        //
        var older = this.WriteFile("older.txt", "older content");
        var draft = this.WriteFile("draft.txt", "draft content");
        var block = Block("Please read this.");
        ((ContentText)block.Content!).FileAttachments.Add(FileAttachment.FromPath(older));

        var parts = ConversationParts.Of(new() { Blocks = [block] }, string.Empty, "And this one.", [FileAttachment.FromPath(draft)], imagesAreSent: true);

        Assert.That(parts.Documents.Select(document => document.FileName), Is.EqualTo(new[] { "older.txt", "draft.txt" }));
    }

    [Test]
    public void AnAttachmentWhoseFileIsGoneCountsForNothing()
    {
        //
        // It is not sent either: the message builder reports it as unavailable and leaves it out.
        //
        var attachment = FileAttachment.FromPath(Path.Combine(this.directory, "never-existed.txt"));

        var parts = ConversationParts.Of(null, string.Empty, "Here", [attachment], imagesAreSent: true);

        Assert.That(parts.Documents, Is.Empty);
    }

    [Test]
    public void ImagesAreCountedSeparatelyFromDocuments()
    {
        var document = this.WriteFile("notes.txt", "content");
        var image = this.WriteFile("photo.png", "not really a png");

        var parts = ConversationParts.Of(null, string.Empty, "Look", [FileAttachment.FromPath(document), FileAttachment.FromPath(image)], imagesAreSent: true);

        Assert.Multiple(() =>
        {
            Assert.That(parts.Documents.Select(entry => entry.FileName), Is.EqualTo(new[] { "notes.txt" }));
            Assert.That(parts.Images, Is.EqualTo(1));
        });
    }

    [Test]
    public void AModelWhichTakesNoImagesIsSentNoneAndIsToldAboutNone()
    {
        //
        // The message builder leaves the pictures out entirely for such a model, so reporting them
        // as uncounted would tell a person about a cost which is not there.
        //
        var image = this.WriteFile("photo.png", "not really a png");

        var parts = ConversationParts.Of(null, string.Empty, "Look", [FileAttachment.FromPath(image)], imagesAreSent: false);

        Assert.That(parts.Images, Is.Zero);
    }

    private static ContentBlock Block(string text) => new()
    {
        ContentType = ContentType.TEXT,
        Role = ChatRole.USER,
        Content = new ContentText { Text = text },
    };

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(this.directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
