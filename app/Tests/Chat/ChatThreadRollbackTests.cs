using AIStudio.Agents;
using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks what rolling a chat back to an earlier answer removes and what it keeps.
/// </summary>
/// <remarks>
/// A rollback has to remove more than the user sees. The prompts an assistant sends into a chat are
/// hidden, yet they are part of the conversation; a chat which kept them would continue with
/// messages the user can neither see nor remove. The example conversation of a chat template is
/// hidden as well, but it comes before everything else and has to keep working.
///
/// What a rollback must not remove is anything which tells the providers what this chat has seen.
/// Removing the message which brought confidential data in does not unsee it, so the chat keeps
/// demanding a self-hosted provider, and the confidence which that data asked for.
/// </remarks>
[TestFixture]
public sealed class ChatThreadRollbackTests
{
    private static readonly DateTimeOffset START = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void HiddenPromptsAfterTheAnswerGoAsWell()
    {
        var answer = Block(ChatRole.AI, "First answer", 2);
        var thread = new ChatThread
        {
            Blocks =
            [
                Block(ChatRole.USER, "First question", 1),
                answer,
                Block(ChatRole.USER, "Prompt of an assistant", 3, hidden: true),
                Block(ChatRole.AI, "Answer to the assistant", 4),
            ],
        };

        var rolledBack = thread.RollBackTo(answer.Content!);

        Assert.Multiple(() =>
        {
            Assert.That(rolledBack, Is.True, "Two blocks came after the answer, so the rollback removed something.");
            Assert.That(Texts(thread), Is.EqualTo(new[] { "First question", "First answer" }), "The hidden prompt goes with the answer to it, although the user never saw it.");
        });
    }

    [Test]
    public void TheExampleConversationOfAChatTemplateStays()
    {
        var answer = Block(ChatRole.AI, "First answer", 4);
        var thread = new ChatThread
        {
            Blocks =
            [
                Block(ChatRole.USER, "Example question", 1, hidden: true),
                Block(ChatRole.AI, "Example answer", 2, hidden: true),
                Block(ChatRole.USER, "First question", 3),
                answer,
                Block(ChatRole.USER, "Second question", 5),
                Block(ChatRole.AI, "Second answer", 6),
            ],
        };

        thread.RollBackTo(answer.Content!);

        Assert.That(Texts(thread), Is.EqualTo(new[] { "Example question", "Example answer", "First question", "First answer" }), "Hidden blocks before the answer belong to what the user rolls back to, so the chat template keeps its example conversation.");
    }

    [Test]
    public void TheOrderIsTheOneOfTheTimeStampsNotTheOneOfTheList()
    {
        var answer = Block(ChatRole.AI, "First answer", 2);
        var thread = new ChatThread
        {
            Blocks =
            [
                Block(ChatRole.USER, "Second question", 3),
                Block(ChatRole.AI, "Second answer", 4),
                answer,
                Block(ChatRole.USER, "First question", 1),
            ],
        };

        thread.RollBackTo(answer.Content!);

        Assert.That(Texts(thread), Is.EqualTo(new[] { "First question", "First answer" }), "The chat shows its blocks by time, so a rollback has to count by time as well, wherever a block sits in the list.");
    }

    [Test]
    public void TheLastRetrievalIsDroppedButTheChoiceOfDataSourcesStays()
    {
        var answer = Block(ChatRole.AI, "First answer", 2);
        var options = new DataSourceOptions
        {
            DisableDataSources = false,
            AutomaticDataSourceSelection = true,
            PreselectedDataSourceIds = ["handbook"],
        };

        var thread = new ChatThread
        {
            DataSourceOptions = options,
            AISelectedDataSources = [SelectedHandbook()],
            AugmentedData = "A chunk from the handbook",
            Blocks =
            [
                Block(ChatRole.USER, "First question", 1),
                answer,
                Block(ChatRole.USER, "Second question", 3),
                Block(ChatRole.AI, "Second answer", 4),
            ],
        };

        thread.RollBackTo(answer.Content!);

        Assert.Multiple(() =>
        {
            Assert.That(thread.AugmentedData, Is.Empty, "Nobody knows whether the retrieved data belongs to a kept or to a removed message, so it must not reach the next system prompt.");
            Assert.That(thread.AISelectedDataSources, Is.Empty, "The data sources an agent picked belong to the same retrieval as the data.");
            Assert.That(thread.DataSourceOptions, Is.SameAs(options), "The data source options are the user's choice, not the result of a message.");
            Assert.That(options.DisableDataSources, Is.False);
            Assert.That(options.AutomaticDataSourceSelection, Is.True);
            Assert.That(options.PreselectedDataSourceIds, Is.EqualTo(new[] { "handbook" }));
        });
    }

    [Test]
    public void WhatTheChatHasSeenKeepsRestrictingTheProviders()
    {
        var answer = Block(ChatRole.AI, "First answer", 2);
        var thread = new ChatThread
        {
            DataSecurity = DataSourceSecurity.SELF_HOSTED,
            Blocks =
            [
                Block(ChatRole.USER, "First question", 1),
                answer,
                Block(ChatRole.USER, "Question about the confidential handbook", 3),
                Block(ChatRole.AI, "Answer from the confidential handbook", 4),
            ],
        };

        thread.RequireProviderConfidence(ConfidenceLevel.HIGH);

        thread.RollBackTo(answer.Content!);

        Assert.Multiple(() =>
        {
            Assert.That(thread.DataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED), "The confidential data was seen by this chat, so no cloud provider may continue it.");
            Assert.That(thread.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH), "The confidence the data demanded stays, although the message which brought it in is gone.");
        });
    }

    [Test]
    public void RollingBackToTheLastAnswerChangesNothing()
    {
        var answer = Block(ChatRole.AI, "First answer", 2);
        var thread = ThreadWithRetrieval(Block(ChatRole.USER, "First question", 1), answer);

        var rolledBack = thread.RollBackTo(answer.Content!);

        AssertUnchanged(thread, rolledBack, "First question", "First answer");
    }

    [Test]
    public void RollingBackToUnknownContentChangesNothing()
    {
        var thread = ThreadWithRetrieval(Block(ChatRole.USER, "First question", 1), Block(ChatRole.AI, "First answer", 2));

        var rolledBack = thread.RollBackTo(new ContentText { Text = "Answer of another chat" });

        AssertUnchanged(thread, rolledBack, "First question", "First answer");
    }

    /// <summary>
    /// A chat whose last retrieval is still in place, so that a rollback which should do nothing
    /// shows when it resets it anyway.
    /// </summary>
    /// <param name="blocks">The blocks of the chat.</param>
    /// <returns>The chat.</returns>
    private static ChatThread ThreadWithRetrieval(params ContentBlock[] blocks) => new()
    {
        AISelectedDataSources = [SelectedHandbook()],
        AugmentedData = "A chunk from the handbook",
        Blocks = [..blocks],
    };

    private static void AssertUnchanged(ChatThread thread, bool rolledBack, params string[] expectedTexts)
    {
        Assert.Multiple(() =>
        {
            Assert.That(rolledBack, Is.False, "Nothing came after the content, so there was nothing to roll back.");
            Assert.That(Texts(thread), Is.EqualTo(expectedTexts), "A rollback without anything to remove leaves the blocks alone.");
            Assert.That(thread.AugmentedData, Is.EqualTo("A chunk from the handbook"), "A rollback without anything to remove keeps the last retrieval, because it still belongs to the last message.");
            Assert.That(thread.AISelectedDataSources, Has.Count.EqualTo(1));
        });
    }

    private static ContentBlock Block(ChatRole role, string text, int minute, bool hidden = false) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text },
        Role = role,
        HideFromUser = hidden,
    };

    private static DataSourceAgentSelected SelectedHandbook() => new()
    {
        DataSource = new DataSourceLocalDirectory { Id = "handbook", Name = "Handbook" },
        AIDecision = new SelectedDataSource("handbook", "The question is about the handbook.", 0.9f),
        Selected = true,
    };

    /// <summary>
    /// The texts of the chat in the order the chat shows them.
    /// </summary>
    /// <param name="thread">The chat.</param>
    /// <returns>The texts.</returns>
    private static string[] Texts(ChatThread thread) => thread.Blocks.OrderBy(x => x.Time).Select(x => ((ContentText)x.Content!).Text).ToArray();
}