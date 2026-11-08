using AIStudio.Chat;
using AIStudio.Chat.Compression;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks which blocks compressing a chat summarizes, which it keeps, and which it hands to the new chat's draft.
/// </summary>
/// <remarks>
/// The new chat holds the example conversation of the chat template, the summary, and the last
/// exchange: the last message of the user and the last answer of the AI. Whatever the plan gets
/// wrong ends up either twice in the new chat or nowhere, and the user would not notice either
/// until the AI answers from a conversation which never was.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionPlanTests
{
    private static readonly DateTimeOffset START = new(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheLastExchangeStaysAndEverythingBeforeItIsSummarized()
    {
        var lastQuestion = Block(ChatRole.USER, "Second question", 3);
        var lastAnswer = Block(ChatRole.AI, "Second answer", 4);
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            lastQuestion,
            lastAnswer);

        var plan = ChatCompressionPlan.Of(chat, 0, draftIsEmpty: true);

        Assert.That(plan, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan!.Preamble, Is.Empty);
            Assert.That(Texts(plan.ToSummarize), Is.EqualTo(new[] { "First question", "First answer" }));
            Assert.That(plan.LastQuestion, Is.SameAs(lastQuestion), "The last message of the user follows the summary word for word, with its files.");
            Assert.That(plan.LastAnswer, Is.SameAs(lastAnswer), "The next message of the user mostly refers to the last answer, so the new chat keeps it word for word.");
            Assert.That(plan.PendingQuestion, Is.Null);
        }
    }

    [Test]
    public void TheExampleConversationOfTheChatTemplateStays()
    {
        var chat = Chat(
            Block(ChatRole.USER, "Example question", 1, hidden: true),
            Block(ChatRole.AI, "Example answer", 2, hidden: true),
            Block(ChatRole.USER, "First question", 3),
            Block(ChatRole.AI, "First answer", 4),
            Block(ChatRole.USER, "Second question", 5),
            Block(ChatRole.AI, "Second answer", 6));

        var plan = ChatCompressionPlan.Of(chat, 2, draftIsEmpty: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Texts(plan!.Preamble), Is.EqualTo(new[] { "Example question", "Example answer" }), "The chat template still holds in the new chat, so its example conversation is kept rather than summarized.");
            Assert.That(Texts(plan.ToSummarize), Is.EqualTo(new[] { "First question", "First answer" }));
        }
    }

    [Test]
    public void HiddenPromptsBeyondTheExampleConversationAreSummarized()
    {
        var chat = Chat(
            Block(ChatRole.USER, "Prompt of an assistant", 1, hidden: true),
            Block(ChatRole.AI, "Result of the assistant", 2),
            Block(ChatRole.USER, "Follow-up question", 3),
            Block(ChatRole.AI, "Follow-up answer", 4));

        var plan = ChatCompressionPlan.Of(chat, 0, draftIsEmpty: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan!.Preamble, Is.Empty, "Without a chat template, a hidden block at the start is part of the conversation, e.g., the prompt of an assistant.");
            Assert.That(Texts(plan.ToSummarize), Is.EqualTo(new[] { "Prompt of an assistant", "Result of the assistant" }));
            Assert.That(Texts([plan.LastQuestion!]), Is.EqualTo(new[] { "Follow-up question" }));
        }
    }

    [Test]
    public void AHiddenLastMessageIsSummarized()
    {
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "Prompt of an assistant", 3, hidden: true),
            Block(ChatRole.AI, "Result of the assistant", 4));

        var plan = ChatCompressionPlan.Of(chat, 0, draftIsEmpty: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan!.LastQuestion, Is.Null, "The new chat could show the hidden message only together with the summary, which the user has to see.");
            Assert.That(Texts(plan.ToSummarize), Is.EqualTo(new[] { "First question", "First answer", "Prompt of an assistant" }), "Rather than getting lost, the hidden message is summarized.");
        }
    }

    [Test]
    public void TheExampleConversationEndsWhereTheHiddenBlocksEnd()
    {
        var chat = Chat(
            Block(ChatRole.USER, "Example question", 1, hidden: true),
            Block(ChatRole.USER, "First question", 2),
            Block(ChatRole.AI, "First answer", 3),
            Block(ChatRole.USER, "Second question", 4),
            Block(ChatRole.AI, "Second answer", 5));

        var plan = ChatCompressionPlan.Of(chat, 2, draftIsEmpty: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Texts(plan!.Preamble), Is.EqualTo(new[] { "Example question" }), "The template changed since the chat started, so the chat keeps what it actually starts with.");
            Assert.That(Texts(plan.ToSummarize), Is.EqualTo(new[] { "First question", "First answer" }));
        }
    }

    [Test]
    public void AnUnansweredLastMessageGoesIntoTheDraftOfTheNewChat()
    {
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "Second question", 3),
            Block(ChatRole.AI, "Second answer", 4),
            Block(ChatRole.USER, "Question which was too long for the model", 5),
            Block(ChatRole.AI, string.Empty, 6));

        var plan = ChatCompressionPlan.Of(chat, 0, draftIsEmpty: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan!.PendingQuestion?.Text, Is.EqualTo("Question which was too long for the model"), "The question never got an answer, so the user gets it back to send it in the new chat.");
            Assert.That(Texts(plan.ToSummarize), Is.EqualTo(new[] { "First question", "First answer" }), "The pending question is not summarized as well, and the failed answer without text never reached the AI.");
            Assert.That(Texts([plan.LastQuestion!, plan.LastAnswer]), Is.EqualTo(new[] { "Second question", "Second answer" }));
        }
    }

    [Test]
    public void AnUnansweredLastMessageIsSummarizedWhileTheUserWritesAnotherOne()
    {
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "Unanswered question", 3));

        var plan = ChatCompressionPlan.Of(chat, 0, draftIsEmpty: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan!.PendingQuestion, Is.Null, "The message the user is writing wins the draft of the new chat.");
            Assert.That(Texts(plan.ToSummarize), Is.EqualTo(new[] { "Unanswered question" }), "Rather than getting lost, the unanswered question is summarized.");
            Assert.That(Texts([plan.LastQuestion!]), Is.EqualTo(new[] { "First question" }));
        }
    }

    [Test]
    public void TheOrderIsTheOneOfTheTimeStampsNotTheOneOfTheList()
    {
        var chat = Chat(
            Block(ChatRole.AI, "Second answer", 4),
            Block(ChatRole.USER, "Second question", 3),
            Block(ChatRole.AI, "First answer", 2),
            Block(ChatRole.USER, "First question", 1));

        var plan = ChatCompressionPlan.Of(chat, 0, draftIsEmpty: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Texts(plan!.ToSummarize), Is.EqualTo(new[] { "First question", "First answer" }));
            Assert.That(Texts([plan.LastQuestion!, plan.LastAnswer]), Is.EqualTo(new[] { "Second question", "Second answer" }));
        }
    }

    [Test]
    public void NothingIsCompressedWithoutAFinishedAnswer()
    {
        var unanswered = Chat(Block(ChatRole.USER, "First question", 1));
        var streaming = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "Half an answer", 2, streaming: true));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionPlan.Of(unanswered, 0, draftIsEmpty: true), Is.Null);
            Assert.That(ChatCompressionPlan.HasFinishedAnswer(unanswered), Is.False);
            Assert.That(ChatCompressionPlan.Of(streaming, 0, draftIsEmpty: true), Is.Null, "An answer which is still being written is no answer the new chat could keep.");
            Assert.That(ChatCompressionPlan.HasFinishedAnswer(streaming), Is.False);
        }
    }

    [Test]
    public void NothingIsCompressedWhenThereIsNothingBeforeTheLastAnswer()
    {
        var chat = Chat(
            Block(ChatRole.USER, "Example question", 1, hidden: true),
            Block(ChatRole.AI, "Example answer", 2, hidden: true),
            Block(ChatRole.AI, "Result sent here from an assistant", 3));

        Assert.That(ChatCompressionPlan.Of(chat, 2, draftIsEmpty: true), Is.Null, "A summary of nothing would only make the chat longer.");
    }

    [Test]
    public void NothingIsCompressedWhenTheLastExchangeIsAllThereIs()
    {
        var chat = Chat(
            Block(ChatRole.USER, "First question", 1),
            Block(ChatRole.AI, "First answer", 2));

        Assert.That(ChatCompressionPlan.Of(chat, 0, draftIsEmpty: true), Is.Null, "The new chat keeps the last exchange word for word, so there would be nothing left to summarize.");
    }

    private static ChatThread Chat(params ContentBlock[] blocks) => new()
    {
        Blocks = [..blocks],
    };

    private static ContentBlock Block(ChatRole role, string text, int minute, bool hidden = false, bool streaming = false) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text, IsStreaming = streaming },
        Role = role,
        HideFromUser = hidden,
    };

    private static string[] Texts(IEnumerable<ContentBlock> blocks) => blocks.Select(block => ((ContentText)block.Content!).Text).ToArray();
}