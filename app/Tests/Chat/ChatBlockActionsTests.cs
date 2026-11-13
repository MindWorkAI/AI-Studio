using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that the messages of a chat stay as they are while an answer is being written for it.
/// </summary>
/// <remarks>
/// While an answer is being written, the AI job holds the very same thread as the chat. A message the
/// user removes then does not come back, but the chat waits for an answer nobody sees, and the job
/// stores the conversation while it changes. So every change to a message is turned down until the
/// answer is complete or stopped, and works as before once it is.
/// </remarks>
[TestFixture]
public sealed class ChatBlockActionsTests
{
    private static readonly DateTimeOffset START = new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void RemovingTheAnswerBeingWrittenLeavesTheChatAsItIs()
    {
        var question = Block(ChatRole.USER, "Question", 1);
        var answer = Block(ChatRole.AI, "Answer being", 2);
        var chat = Chat(question, answer);

        var removed = WhileWriting(chat).TryRemove(chat, answer.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.False);
            Assert.That(chat.Blocks.Select(x => x.Content), Is.EqualTo(new[] { question.Content, answer.Content }), "The answer stays, since the job goes on writing into it.");
        }
    }

    [Test]
    public void OlderMessagesStayWhileAnAnswerIsBeingWritten()
    {
        var firstQuestion = Block(ChatRole.USER, "First question", 1);
        var firstAnswer = Block(ChatRole.AI, "First answer", 2);
        var chat = Chat(firstQuestion, firstAnswer, Block(ChatRole.USER, "Second question", 3), Block(ChatRole.AI, "Second answer being", 4));
        var blocks = chat.Blocks.ToList();
        var actions = WhileWriting(chat);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actions.TryRemove(chat, firstQuestion.Content!), Is.False);
            Assert.That(actions.TryRemove(chat, firstAnswer.Content!), Is.False);
            Assert.That(actions.TryRemoveForRegenerate(chat, firstAnswer.Content!), Is.False);
            Assert.That(chat.Blocks, Is.EqualTo(blocks), "The job stores the conversation meanwhile, so no message may change, not even an older one.");
        }
    }

    [Test]
    public void EditingTheQuestionChangesNeitherTheChatNorTheDraft()
    {
        var question = Block(ChatRole.USER, "Question", 1);
        var answer = Block(ChatRole.AI, "Answer being", 2);
        var chat = Chat(question, answer);
        var actions = WhileWriting(chat);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actions.TryEditLastUserBlock(chat, question.Content!), Is.False);
            Assert.That(actions.TryEditLastBlock(chat, question.Content!), Is.False);
            Assert.That(chat.Blocks, Has.Count.EqualTo(2), "Both the question and the answer being written stay.");
            Assert.That(chat.Draft.IsEmpty, Is.True, "The question does not become the draft, since it could not be sent anyway.");
        }
    }

    [Test]
    public void RegeneratingKeepsTheAnswerBeingWritten()
    {
        var answer = Block(ChatRole.AI, "Answer being", 2);
        var chat = Chat(Block(ChatRole.USER, "Question", 1), answer);

        var removed = WhileWriting(chat).TryRemoveForRegenerate(chat, answer.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.False, "Regenerating would leave a second answer next to the one being written.");
            Assert.That(chat.Blocks, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void RollingBackIsTurnedDownWhileAnAnswerIsBeingWritten()
    {
        var firstAnswer = Block(ChatRole.AI, "First answer", 2);
        var chat = Chat(Block(ChatRole.USER, "First question", 1), firstAnswer, Block(ChatRole.USER, "Second question", 3), Block(ChatRole.AI, "Second answer being", 4));

        var rolledBack = WhileWriting(chat).TryRollBack(chat, firstAnswer.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rolledBack, Is.False);
            Assert.That(chat.Blocks, Has.Count.EqualTo(4));
        }
    }

    [Test]
    public void RemovingAMessageWorksOnceTheAnswerIsComplete()
    {
        var question = Block(ChatRole.USER, "Question", 1);
        var answer = Block(ChatRole.AI, "Answer", 2);
        var chat = Chat(question, answer);

        var removed = Done().TryRemove(chat, answer.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.True);
            Assert.That(chat.Blocks.Select(x => x.Content), Is.EqualTo(new[] { question.Content }));
        }
    }

    [Test]
    public void RegeneratingWorksOnceTheAnswerIsComplete()
    {
        var question = Block(ChatRole.USER, "Question", 1);
        var answer = Block(ChatRole.AI, "Answer", 2);
        var chat = Chat(question, answer);

        var removed = Done().TryRemoveForRegenerate(chat, answer.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.True);
            Assert.That(chat.Blocks.Select(x => x.Content), Is.EqualTo(new[] { question.Content }), "The question stays, to be answered anew.");
        }
    }

    [Test]
    public void RollingBackWorksOnceTheAnswerIsComplete()
    {
        var firstQuestion = Block(ChatRole.USER, "First question", 1);
        var firstAnswer = Block(ChatRole.AI, "First answer", 2);
        var chat = Chat(firstQuestion, firstAnswer, Block(ChatRole.USER, "Second question", 3), Block(ChatRole.AI, "Second answer", 4));

        var rolledBack = Done().TryRollBack(chat, firstAnswer.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rolledBack, Is.True);
            Assert.That(chat.Blocks.Select(x => x.Content), Is.EqualTo(new[] { firstQuestion.Content, firstAnswer.Content }));
        }
    }

    [Test]
    public void EditingTheQuestionWorksOnceTheAnswerIsComplete()
    {
        var question = Block(ChatRole.USER, "Question", 1);
        var chat = Chat(question, Block(ChatRole.AI, "Answer", 2));

        var edited = Done().TryEditLastUserBlock(chat, question.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(edited, Is.True);
            Assert.That(chat.Blocks, Is.Empty, "The question goes back into the composer, and the answer to it goes.");
            Assert.That(chat.Draft.Text, Is.EqualTo("Question"));
        }
    }

    [Test]
    public void EditingAQuestionWithoutAnswerWorksOnceTheAnswerIsGone()
    {
        var question = Block(ChatRole.USER, "Question", 1);
        var chat = Chat(question);

        var edited = Done().TryEditLastBlock(chat, question.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(edited, Is.True);
            Assert.That(chat.Blocks, Is.Empty);
            Assert.That(chat.Draft.Text, Is.EqualTo("Question"));
        }
    }

    [Test]
    public void EditingAQuestionOfAnotherChatKeepsTheAnswer()
    {
        var answer = Block(ChatRole.AI, "Answer", 2);
        var chat = Chat(Block(ChatRole.USER, "Question", 1), answer);

        var edited = Done().TryEditLastUserBlock(chat, new ContentText { Text = "Question of another chat" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(edited, Is.False);
            Assert.That(chat.Blocks, Has.Count.EqualTo(2), "Removing the last answer only makes sense together with the question to it.");
            Assert.That(chat.Draft.IsEmpty, Is.True);
        }
    }

    [Test]
    public void AnAnswerBeingWrittenInAnotherChatLocksNothingHere()
    {
        var answer = Block(ChatRole.AI, "Answer", 2);
        var chat = Chat(Block(ChatRole.USER, "Question", 1), answer);
        var otherChatId = Guid.NewGuid();
        var askedChatIds = new List<Guid>();
        var actions = new ChatBlockActions(chatId =>
        {
            askedChatIds.Add(chatId);
            return chatId == otherChatId;
        });

        var removed = actions.TryRemove(chat, answer.Content!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.True);
            Assert.That(askedChatIds, Is.EqualTo(new[] { chat.ChatId }), "Only the chat being changed decides.");
        }
    }

    [Test]
    public void WithoutAChatNothingIsAllowed()
    {
        var actions = Done();
        var content = new ContentText { Text = "Question" };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actions.AreAllowed(null), Is.False);
            Assert.That(actions.TryRemove(null, content), Is.False);
            Assert.That(actions.TryRemoveForRegenerate(null, content), Is.False);
            Assert.That(actions.TryRollBack(null, content), Is.False);
            Assert.That(actions.TryEditLastUserBlock(null, content), Is.False);
            Assert.That(actions.TryEditLastBlock(null, content), Is.False);
        }
    }

    /// <summary>
    /// The actions while an answer is being written for the given chat.
    /// </summary>
    private static ChatBlockActions WhileWriting(ChatThread chat) => new(chatId => chatId == chat.ChatId);

    /// <summary>
    /// The actions while no answer is being written for any chat.
    /// </summary>
    private static ChatBlockActions Done() => new(_ => false);

    private static ChatThread Chat(params ContentBlock[] blocks) => new() { ChatId = Guid.NewGuid(), Blocks = [..blocks] };

    private static ContentBlock Block(ChatRole role, string text, int minute) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text },
        Role = role,
    };
}