using AIStudio.Chat;
using AIStudio.AIOperations.ConversationSummary;

namespace AIStudio.Tests.AIOperations.ConversationSummary;

/// <summary>
/// Checks what the AI reads when it summarizes a chat, and how a long conversation is split into parts.
/// </summary>
/// <remarks>
/// The summary takes in the conversation with the content of its documents. Compressing
/// is asked for when a chat has grown too large, so the request for the summary must not fail for
/// the same reason: whatever is too long for one request is split into parts which fit the model,
/// without losing anything.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionInputTests
{
    private static readonly DateTimeOffset TIME = new(2026, 11, 7, 14, 3, 0, TimeSpan.Zero);

    [Test]
    public void AMessageNamesItsNumberAuthorAndTime()
    {
        var rendered = ConversationSummaryInput.Render(Block(ChatRole.USER), 3, "  What does the report say?  ", KeptMessage.NONE);

        Assert.That(rendered, Is.EqualTo("[Message 3, user, 2026-11-07 14:03]\nWhat does the report say?\n"));
    }

    [Test]
    public void AttachedFilesAreNamed()
    {
        var block = Block(ChatRole.USER);
        ((ContentText)block.Content!).FileAttachments =
        [
            new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", "C:/files/report.pdf", 1_000),
            new FileAttachmentImage("chart.png", "C:/files/chart.png", 1_000),
            new ManagedTranscriptAttachment("meeting-1.md", "C:/data/meeting-1.md", 1_000, "meeting.mp4", false),
        ];

        var rendered = ConversationSummaryInput.Render(block, 1, "Please compare both.\n\nThe following files are attached to this message: ...", KeptMessage.NONE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rendered, Does.Contain("The following files are attached to this message: ..."), "The text of a summarized message is the one the AI received, with the content of its documents.");
            Assert.That(rendered, Does.EndWith("Attached files: report.pdf, chart.png (image), meeting.mp4 (transcript)\n"), "An image does not go along, but the summary learns that there was one, so that it can relate what the conversation said about it.");
        }
    }

    [Test]
    public void HiddenMessagesAndTheLastExchangeAreMarked()
    {
        var hidden = ConversationSummaryInput.Render(Block(ChatRole.USER, hidden: true), 1, "Prompt of an assistant", KeptMessage.NONE);
        var lastQuestion = ConversationSummaryInput.Render(Block(ChatRole.USER), 2, "The question", KeptMessage.LAST_QUESTION);
        var lastAnswer = ConversationSummaryInput.Render(Block(ChatRole.AI), 3, "The answer", KeptMessage.LAST_ANSWER);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hidden, Does.StartWith("[Message 1, user, 2026-11-07 14:03, not shown to the user]"));
            Assert.That(lastQuestion, Does.StartWith("[Message 2, user, 2026-11-07 14:03, the last message of the user, which follows your summary word for word together with its files]"));
            Assert.That(lastAnswer, Does.StartWith("[Message 3, AI, 2026-11-07 14:03, the last answer, which follows your summary word for word]"));
        }
    }

    [Test]
    public void MessagesWhichFitStayInOnePart()
    {
        var parts = ConversationSummaryInput.SplitIntoParts([new("first", 10), new("second", 10), new("third", 10)], 100);

        Assert.That(parts, Is.EqualTo(new[] { "first\nsecond\nthird" }));
    }

    [Test]
    public void ALongConversationIsSplitBetweenMessages()
    {
        var parts = ConversationSummaryInput.SplitIntoParts([new("first", 40), new("second", 40), new("third", 40), new("fourth", 10)], 100);

        Assert.That(parts, Is.EqualTo(new[] { "first\nsecond", "third\nfourth" }), "A message which fits into a part of its own is never split.");
    }

    [Test]
    public void AMessageLargerThanAPartIsSplitAcrossPartsWithoutLosingAnything()
    {
        var longMessage = new string('a', 600) + new string('b', 400);
        var parts = ConversationSummaryInput.SplitIntoParts([new("first", 10), new(longMessage, 1_000), new("last", 10)], 100);

        var pieces = parts.Skip(1).Select(part => part.Replace(ConversationSummaryInput.CONTINUATION_MARKER, string.Empty).Replace(ConversationSummaryInput.TO_BE_CONTINUED_MARKER, string.Empty)).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(parts[0], Is.EqualTo("first"), "The long message starts a part of its own.");
            Assert.That(parts.Count, Is.GreaterThan(10), "A message of 1,000 tokens takes more than ten parts of 100 tokens.");
            Assert.That(string.Concat(pieces).Replace("\n", string.Empty), Is.EqualTo(longMessage + "last"), "Nothing of the long message gets lost, as a document attached to it would.");
            Assert.That(pieces.Take(pieces.Count - 1), Has.All.Length.LessThanOrEqualTo(100), "Each piece fits into its part.");
            Assert.That(parts[1], Does.EndWith(ConversationSummaryInput.TO_BE_CONTINUED_MARKER), "The AI learns that the message goes on.");
            Assert.That(parts[2], Does.StartWith(ConversationSummaryInput.CONTINUATION_MARKER), "The AI learns where the message comes from.");
        }
    }

    [Test]
    public void APartTakesWhatFitsFromWhereTheLastOneEnded()
    {
        CountedMessage[] messages = [new("first", 40), new("second", 40), new("third", 40), new("fourth", 40)];

        var firstPart = ConversationSummaryInput.TakePart(messages, ConversationSummaryPosition.START, 100);
        var secondPart = ConversationSummaryInput.TakePart(messages, firstPart.Next, 50);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPart.Text, Is.EqualTo("first\nsecond"));
            Assert.That(firstPart.Next, Is.EqualTo(new ConversationSummaryPosition(2, 0)));
            Assert.That(secondPart.Text, Is.EqualTo("third"), "The summary so far takes room in the second request, so the second part is smaller.");
            Assert.That(secondPart.Next.Message, Is.EqualTo(3));
        }
    }

    [Test]
    public void ASplitPrefersTheEndOfALine()
    {
        var text = new string('a', 85) + "\n" + new string('b', 200);

        var part = ConversationSummaryInput.TakePart([new(text, text.Length)], ConversationSummaryPosition.START, 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(part.Text, Is.EqualTo(new string('a', 85) + "\n" + ConversationSummaryInput.TO_BE_CONTINUED_MARKER), "A line of a document stays whole when it ends close to where the part would.");
            Assert.That(part.Next, Is.EqualTo(new ConversationSummaryPosition(0, 86)));
        }
    }

    [Test]
    public void SplittingNeverSplitsASurrogatePair()
    {
        var text = string.Concat(Enumerable.Repeat("😀", 100));

        // Chosen so that the piece would end between the halves of a pair: 45 characters would fit:
        var part = ConversationSummaryInput.TakePart([new(text, 200)], ConversationSummaryPosition.START, 50);

        var piece = part.Text.Replace(ConversationSummaryInput.TO_BE_CONTINUED_MARKER, string.Empty);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(part.Next.Offset % 2, Is.Zero, "The next part begins with a whole character.");
            Assert.That(piece.Where(char.IsHighSurrogate).Count(), Is.EqualTo(piece.Where(char.IsLowSurrogate).Count()), "Half a surrogate pair is no character, and no JSON writer takes it.");
        }
    }

    private static ContentBlock Block(ChatRole role, bool hidden = false) => new()
    {
        Time = TIME,
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = "The stored text, which the rendering does not read" },
        Role = role,
        HideFromUser = hidden,
    };
}