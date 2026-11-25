using AIStudio.Chat;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that a transcript holds what the user sees of a chat, and nothing else.
/// </summary>
[TestFixture]
public sealed class ChatTranscriptTests
{
    [Test]
    public void TheTranscriptHoldsTheVisibleMessagesInTheirOrder()
    {
        var thread = Thread(
            Block(ChatRole.USER, new ContentText { Text = "A hidden opening." }, hidden: true),
            Block(ChatRole.USER, new ContentText { Text = "Summarize the report." }),
            Block(ChatRole.AI, new ContentText { Text = "<think>Let me read it.</think>The report says 42." }),
            Block(ChatRole.USER, new ContentText { Text = "Thanks." }));

        Assert.That(ChatTranscript.Of(thread), Is.EqualTo("[User]\nSummarize the report.\n\n[Assistant]\nThe report says 42.\n\n[User]\nThanks."));
    }

    [Test]
    public void ToolCallsAreOneShortLineWithoutTheirResults()
    {
        var answer = new ContentText
        {
            Text = "Found it.",
            ToolInvocations =
            [
                new ToolInvocationTrace { Order = 2, ToolName = "read_web_page", Arguments = new() { ["url"] = "https://example.org" }, Result = "The whole page." },
                new ToolInvocationTrace { Order = 1, ToolName = "web_search", Arguments = new() { ["query"] = new string('q', 100) + "\nmore" }, Result = "Many results." },
            ],
        };

        var transcript = ChatTranscript.Of(Thread(Block(ChatRole.AI, answer)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(transcript, Does.StartWith($"[Assistant]\n[Tool call: web_search(query={new string('q', ChatTranscript.MAX_ARGUMENT_LENGTH)}…)]\n[Tool call: read_web_page(url=https://example.org)]\nFound it."));
            Assert.That(transcript, Does.Not.Contain("Many results."));
            Assert.That(transcript, Does.Not.Contain("The whole page."));
        }
    }

    [Test]
    public void AttachedFilesAreNamedWithoutTheirContent()
    {
        var message = new ContentText
        {
            Text = "Please check these.",
            FileAttachments = [new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", "/tmp/report.pdf", 100), new FileAttachment(FileAttachmentType.DOCUMENT, "notes.md", "/tmp/notes.md", 10)],
        };

        Assert.That(ChatTranscript.Of(Thread(Block(ChatRole.USER, message))), Is.EqualTo("[User]\nPlease check these.\n[Attached files: report.pdf, notes.md]"));
    }

    [Test]
    public void AChatWithoutVisibleMessagesHasAnEmptyTranscript()
    {
        var thread = Thread(Block(ChatRole.AI, new ContentText { Text = "   " }), Block(ChatRole.USER, new ContentText { Text = "Hidden." }, hidden: true));

        Assert.That(ChatTranscript.Of(thread), Is.Empty);
    }

    private static ChatThread Thread(params ContentBlock[] blocks) => new() { Blocks = [.. blocks] };

    private static ContentBlock Block(ChatRole role, ContentText content, bool hidden = false) => new()
    {
        Time = DateTimeOffset.UnixEpoch,
        ContentType = ContentType.TEXT,
        Role = role,
        Content = content,
        HideFromUser = hidden,
    };
}