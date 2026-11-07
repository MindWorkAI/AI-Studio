using AIStudio.Chat;
using AIStudio.Chat.Compression;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks what the AI reads when it summarizes a chat, and how a long conversation is split into parts.
/// </summary>
/// <remarks>
/// Compressing is asked for when a chat has grown too large, so the request for the summary must
/// not fail for the same reason: it leaves out the contents of the attached files, and it splits
/// whatever is still too long into parts which fit the model.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionInputTests
{
    private static readonly DateTimeOffset TIME = new(2026, 11, 7, 14, 3, 0, TimeSpan.Zero);

    [Test]
    public void AMessageNamesItsNumberAuthorAndTime()
    {
        var rendered = ChatCompressionInput.Render(Block(ChatRole.USER, "  What does the report say?  "), 3);

        Assert.That(rendered, Is.EqualTo("[Message 3, user, 2026-11-07 14:03]\nWhat does the report say?\n"));
    }

    [Test]
    public void AttachedFilesStandInByTheirNamesOnly()
    {
        var block = Block(ChatRole.USER, "Please compare both.");
        var text = (ContentText)block.Content!;
        text.FileAttachments =
        [
            new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", "C:/files/report.pdf", 1_000),
            new FileAttachment(FileAttachmentType.IMAGE, "chart.png", "C:/files/chart.png", 1_000),
            new ManagedTranscriptAttachment("meeting-1.md", "C:/data/meeting-1.md", 1_000, "meeting.mp4", false),
        ];

        var rendered = ChatCompressionInput.Render(block, 1);

        Assert.That(rendered, Does.EndWith("Attached files: report.pdf, chart.png (image), meeting.mp4 (transcript)\n"), "The contents of the files would make the request as large as the chat, so the AI learns their names only.");
    }

    [Test]
    public void HiddenMessagesAndTheLastAnswerAreMarked()
    {
        var hidden = ChatCompressionInput.Render(Block(ChatRole.USER, "Prompt of an assistant", hidden: true), 1);
        var lastAnswer = ChatCompressionInput.Render(Block(ChatRole.AI, "The answer"), 2, isLastAnswer: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hidden, Does.StartWith("[Message 1, user, 2026-11-07 14:03, not shown to the user]"));
            Assert.That(lastAnswer, Does.StartWith("[Message 2, AI, 2026-11-07 14:03, the last answer, which follows your summary word for word]"));
        }
    }

    [Test]
    public void MessagesWhichFitStayInOnePart()
    {
        var parts = ChatCompressionInput.SplitIntoParts([new("first", 10), new("second", 10), new("third", 10)], 100);

        Assert.That(parts, Is.EqualTo(new[] { "first\nsecond\nthird" }));
    }

    [Test]
    public void ALongConversationIsSplitBetweenMessages()
    {
        var parts = ChatCompressionInput.SplitIntoParts([new("first", 40), new("second", 40), new("third", 40), new("fourth", 10)], 100);

        Assert.That(parts, Is.EqualTo(new[] { "first\nsecond", "third\nfourth" }), "A part never ends in the middle of a message.");
    }

    [Test]
    public void AMessageLargerThanAPartIsCutInTheMiddleAndStandsAlone()
    {
        var longMessage = new string('a', 600) + new string('b', 400);
        var parts = ChatCompressionInput.SplitIntoParts([new("first", 10), new(longMessage, 1_000), new("last", 10)], 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parts, Has.Count.EqualTo(3));
            Assert.That(parts[0], Is.EqualTo("first"));
            Assert.That(parts[1], Does.StartWith("aaaa").And.EndWith("bbbb").And.Contain("characters left out"), "The beginning and the end of a message are what a conversation refers to most.");
            Assert.That(parts[1].Length, Is.LessThan(150), "The cut message has to fit into its part.");
            Assert.That(parts[2], Is.EqualTo("last"));
        }
    }

    [Test]
    public void APartTakesWhatFitsFromWhereTheLastOneEnded()
    {
        CountedMessage[] messages = [new("first", 40), new("second", 40), new("third", 40), new("fourth", 40)];

        var (firstPart, afterFirstPart) = ChatCompressionInput.TakePart(messages, 0, 100);
        var (secondPart, afterSecondPart) = ChatCompressionInput.TakePart(messages, afterFirstPart, 50);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPart, Is.EqualTo("first\nsecond"));
            Assert.That(afterFirstPart, Is.EqualTo(2));
            Assert.That(secondPart, Is.EqualTo("third"), "The summary so far takes room in the second request, so the second part is smaller.");
            Assert.That(afterSecondPart, Is.EqualTo(3));
        }
    }

    [Test]
    public void CuttingNeverSplitsASurrogatePair()
    {
        var text = string.Concat(Enumerable.Repeat("😀", 100));

        // Chosen so that both cuts would fall between the halves of a pair: 17 characters stay, 11 from the beginning and 6 from the end:
        var cut = ChatCompressionInput.CutMiddle(text, 1_000, 95);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cut, Does.Contain("characters left out"));
            Assert.That(cut.Where(char.IsHighSurrogate).Count(), Is.EqualTo(cut.Where(char.IsLowSurrogate).Count()), "Half a surrogate pair is no character, and no JSON writer takes it.");
            Assert.That(char.IsHighSurrogate(cut[^1]), Is.False);
        }
    }

    [Test]
    public void ATextWhichFitsIsNotCut()
    {
        Assert.That(ChatCompressionInput.CutMiddle("short", 5, 10), Is.EqualTo("short"));
    }

    private static ContentBlock Block(ChatRole role, string text, bool hidden = false) => new()
    {
        Time = TIME,
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text },
        Role = role,
        HideFromUser = hidden,
    };
}