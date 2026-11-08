using AIStudio.Chat;
using AIStudio.Chat.Compression;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks the new chat a compressed chat continues in.
/// </summary>
/// <remarks>
/// The new chat starts as a copy of the compressed one, so it keeps everything which is not a
/// message, the restrictions above all: they say what the conversation has seen, and the summary
/// carries that content along. Only the messages give way to the summary and the last exchange.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionTests
{
    private static readonly DateTimeOffset START = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheSummaryGoesAheadOfTheLastMessageOfTheUser()
    {
        var chat = Chat(
            Block(ChatRole.USER, "Example question", 1, hidden: true),
            Block(ChatRole.AI, "Example answer", 2, hidden: true),
            Block(ChatRole.USER, "First question", 3),
            Block(ChatRole.AI, "First answer", 4),
            Block(ChatRole.USER, "Second question", 5),
            Block(ChatRole.AI, "Second answer", 6));

        ChatCompression.TurnIntoContinuation(chat, 2, "The summary", ChatRestrictions.NONE, "provider-1");

        var ordered = chat.Blocks.OrderBy(block => block.Time).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Texts(chat), Is.EqualTo(new[] { "Example question", "Example answer", ChatCompression.WithLastMessage("The summary", "Second question"), "Second answer" }), "The order of the time stamps is the order of the conversation.");
            Assert.That(ordered.Select(block => block.Role), Is.EqualTo(new[] { ChatRole.USER, ChatRole.AI, ChatRole.USER, ChatRole.AI }), "The summary shares the message of the user, so the roles keep alternating.");
            Assert.That(ordered[2].HideFromUser, Is.False, "The user sees what the new chat continues from.");
            Assert.That(ordered[2].Time, Is.EqualTo(START.AddMinutes(5)), "The last message of the user keeps its time.");
            Assert.That(ordered[3].Time, Is.EqualTo(START.AddMinutes(6)), "The last answer keeps its time.");
            Assert.That(chat.Blocks, Is.EqualTo(ordered), "The list is in the order of the conversation as well.");
        }
    }

    [Test]
    public void TheFilesOfTheLastMessageStayAttached()
    {
        var lastQuestion = Block(ChatRole.USER, "Please compare both.", 3);
        ((ContentText)lastQuestion.Content!).FileAttachments =
        [
            new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", "C:/files/report.pdf", 1_000),
            new FileAttachmentImage("chart.png", "C:/files/chart.png", 1_000),
        ];

        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            lastQuestion,
            Block(ChatRole.AI, "The comparison", 4));

        ChatCompression.TurnIntoContinuation(chat, 0, "The summary", ChatRestrictions.NONE, "provider-1");

        var message = (ContentText)chat.Blocks.OrderBy(block => block.Time).First().Content!;
        Assert.That(message.FileAttachments.Select(attachment => attachment.FileName), Is.EqualTo(new[] { "report.pdf", "chart.png" }), "The AI receives the files of the last message in full again.");
    }

    [Test]
    public void TheSummaryStandsAloneWhenTheLastMessageWasHidden()
    {
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "Prompt of an assistant", 3, hidden: true),
            Block(ChatRole.AI, "Result of the assistant", 4));

        ChatCompression.TurnIntoContinuation(chat, 0, "The summary", ChatRestrictions.NONE, "provider-1");

        var ordered = chat.Blocks.OrderBy(block => block.Time).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Texts(chat), Is.EqualTo(new[] { "The summary", "Result of the assistant" }), "The hidden message was summarized, so the summary stands as a message of its own.");
            Assert.That(ordered[0].Role, Is.EqualTo(ChatRole.USER));
            Assert.That(ordered[0].HideFromUser, Is.False);
            Assert.That(ordered[0].Time, Is.LessThan(ordered[1].Time), "The summary goes right before the last answer.");
        }
    }

    [Test]
    public void WhatDescribedTheLastRequestIsReset()
    {
        var lastAnswer = Block(ChatRole.AI, "Second answer", 4);
        ((ContentText)lastAnswer.Content!).ReportedUsage = new() { PromptTokens = 12_345, ModelId = "model", BlockCount = 4 };
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "Second question", 3),
            lastAnswer);

        chat.AugmentedData = "A chunk from the handbook";
        chat.SelectedProvider = "provider-of-the-old-chat";

        ChatCompression.TurnIntoContinuation(chat, 0, "The summary", ChatRestrictions.NONE, "provider-1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((ContentText)lastAnswer.Content!).ReportedUsage, Is.Null, "What the provider reported was for the request of the compressed chat, which held far more.");
            Assert.That(chat.AugmentedData, Is.Empty, "The retrieved data belonged to the last request of the compressed chat.");
            Assert.That(chat.SelectedProvider, Is.EqualTo("provider-1"), "The new chat continues with the provider which wrote the summary.");
        }
    }

    [Test]
    public void TheRestrictionsOnlyTighten()
    {
        var chat = Chat(
            Block(ChatRole.USER, "Question about the confidential handbook", 1),
            Block(ChatRole.AI, "Answer from the confidential handbook", 2),
            Block(ChatRole.USER, "Follow-up question", 3),
            Block(ChatRole.AI, "Follow-up answer", 4));

        chat.DataSecurity = DataSourceSecurity.SELF_HOSTED;
        chat.RequireProviderConfidence(ConfidenceLevel.MEDIUM);

        ChatCompression.TurnIntoContinuation(chat, 0, "The summary", ChatRestrictions.NONE with { ProviderConfidence = ConfidenceLevel.HIGH }, "provider-1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chat.DataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED), "The summary carries the confidential content along, so the new chat stays as restricted as the old one.");
            Assert.That(chat.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH), "What the summary demands is added.");
        }
    }

    [Test]
    public void AnUnansweredQuestionBecomesTheDraft()
    {
        var pending = Block(ChatRole.USER, "Question which was too long for the model", 5);
        var report = new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", "C:/files/report.pdf", 1_000);
        ((ContentText)pending.Content!).FileAttachments = [report];
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "Second question", 3),
            Block(ChatRole.AI, "Second answer", 4),
            pending);

        ChatCompression.TurnIntoContinuation(chat, 0, "The summary", ChatRestrictions.NONE, "provider-1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chat.Draft.Text, Is.EqualTo("Question which was too long for the model"), "The user finds the question in the field for the next message and can send it again.");
            Assert.That(chat.Draft.FileAttachments.Select(attachment => attachment.FileName), Is.EqualTo(new[] { "report.pdf" }));
            Assert.That(Texts(chat), Is.EqualTo(new[] { ChatCompression.WithLastMessage("The summary", "Second question"), "Second answer" }), "The question is in the draft, so it is not a message of the new chat as well.");
        }
    }

    [Test]
    public void AChatWithNothingToCompressIsRefused()
    {
        var chat = Chat(Block(ChatRole.USER, "First question", 1));

        Assert.That(() => ChatCompression.TurnIntoContinuation(chat, 0, "The summary", ChatRestrictions.NONE, "provider-1"), Throws.InvalidOperationException, "A copy which cannot become a continuation must not be stored.");
    }

    [TestCase("Plan", "Plan (continued)")]
    [TestCase("Plan (continued)", "Plan (continued 2)")]
    [TestCase("Plan (continued 2)", "Plan (continued 3)")]
    [TestCase("Plan (continued 2) notes", "Plan (continued 2) notes (continued)")]
    [TestCase("(continued)", "(continued) (continued)")]
    public void AContinuationCountsUpRatherThanRepeatingTheSuffix(string name, string expected)
    {
        Assert.That(ChatCompression.ContinuedName(name, "{0} (continued)", "{0} (continued {1})"), Is.EqualTo(expected));
    }

    [Test]
    public void TheNameFollowsTheTranslation()
    {
        Assert.That(ChatCompression.ContinuedName("Budget (Fortsetzung)", "{0} (Fortsetzung)", "{0} (Fortsetzung {1})"), Is.EqualTo("Budget (Fortsetzung 2)"));
    }

    [Test]
    public void TheSummaryMessageNamesTheChatItContinues()
    {
        var message = ChatCompression.SummaryMessage("Budget 2027", "  1. Primary request and intent: ...  ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Does.StartWith("**Summary of the chat 'Budget 2027'**"));
            Assert.That(message, Does.EndWith("\n\n1. Primary request and intent: ..."));
        }
    }

    [Test]
    public void TheLastMessageFollowsTheSummaryUnchanged()
    {
        var message = ChatCompression.WithLastMessage("**Summary of the chat 'Budget 2027'**", "What about  the travel costs?\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Does.StartWith("**Summary of the chat 'Budget 2027'**\n\n---\n\n"));
            Assert.That(message, Does.EndWith("**\n\nWhat about  the travel costs?\n"), "The message of the user stays word for word.");
        }
    }

    private static ChatThread Chat(params ContentBlock[] blocks) => new()
    {
        Blocks = [..blocks],
    };

    private static ContentBlock Block(ChatRole role, string text, int minute, bool hidden = false) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text },
        Role = role,
        HideFromUser = hidden,
    };

    private static string[] Texts(ChatThread chat) => chat.Blocks.OrderBy(block => block.Time).Select(block => ((ContentText)block.Content!).Text).ToArray();
}