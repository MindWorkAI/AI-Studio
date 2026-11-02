using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotAnalysisSendToChatTests
{
    private const string ADDRESS_A_MODEL_CHOSE = "https://research.example.org/report";

    [Test]
    public void TheChatGetsTheVisibleRequestAndTheVisibleResultOnly()
    {
        var assistantThread = CreateAssistantThread();
        var result = assistantThread.Blocks.Last();
        var visiblePrompt = VisiblePrompt(result.Time.AddTicks(-1));

        var chatThread = AssistantSWOTAnalysis.CreateChatThreadForResult(assistantThread, visiblePrompt, result);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatThread.Blocks, Has.Count.EqualTo(2));
            Assert.That(chatThread.Blocks[0], Is.SameAs(visiblePrompt));
            Assert.That(chatThread.Blocks[1].Role, Is.EqualTo(ChatRole.AI));
            Assert.That(chatThread.Blocks[1].HideFromUser, Is.False);
            Assert.That(((ContentText)chatThread.Blocks[1].Content!).Text, Is.EqualTo("## SWOT Matrix"));
            Assert.That(chatThread.Blocks[1].Content, Is.Not.SameAs(result.Content));
            Assert.That(chatThread.SystemPrompt, Is.EqualTo(SystemPrompts.DEFAULT));
        }
    }

    [Test]
    public void AnAddressAModelWroteIsNotGivenInTheChat()
    {
        var assistantThread = CreateAssistantThread();
        var result = assistantThread.Blocks.Last();

        var chatThread = AssistantSWOTAnalysis.CreateChatThreadForResult(assistantThread, VisiblePrompt(result.Time.AddTicks(-1)), result);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(assistantThread.IsWebAddressGivenToTheModel(new Uri(ADDRESS_A_MODEL_CHOSE)), Is.True, "The hidden request counts as a message of the user.");
            Assert.That(chatThread.IsWebAddressGivenToTheModel(new Uri(ADDRESS_A_MODEL_CHOSE)), Is.False);
        }
    }

    [Test]
    public void TheChatKeepsTheIdAndTheRestrictionsOfTheAssistant()
    {
        var assistantThread = CreateAssistantThread();
        assistantThread.RequireProviderConfidence(ConfidenceLevel.HIGH);
        var result = assistantThread.Blocks.Last();

        var chatThread = AssistantSWOTAnalysis.CreateChatThreadForResult(assistantThread, VisiblePrompt(result.Time.AddTicks(-1)), result);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatThread.ChatId, Is.EqualTo(assistantThread.ChatId));
            Assert.That(chatThread.RequiredProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH));
        }
    }

    [Test]
    public void WithoutAResultTheChatStartsEmpty()
    {
        var chatThread = AssistantSWOTAnalysis.CreateChatThreadForResult(CreateAssistantThread(), null, null);

        Assert.That(chatThread.Blocks, Is.Empty);
    }

    private static ChatThread CreateAssistantThread()
    {
        var time = DateTimeOffset.Now;
        return new ChatThread
        {
            ChatId = Guid.NewGuid(),
            Blocks =
            [
                new ContentBlock
                {
                    Time = time,
                    Role = ChatRole.USER,
                    ContentType = ContentType.TEXT,
                    HideFromUser = true,
                    Content = new ContentText { Text = $"The opportunities rest on {ADDRESS_A_MODEL_CHOSE}." },
                },
                new ContentBlock
                {
                    Time = time.AddSeconds(1),
                    Role = ChatRole.AI,
                    ContentType = ContentType.TEXT,
                    HideFromUser = true,
                    Content = new ContentText { Text = "## SWOT Matrix" },
                },
            ],
        };
    }

    private static ContentBlock VisiblePrompt(DateTimeOffset time) => new()
    {
        Time = time,
        Role = ChatRole.USER,
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = "Create a SWOT analysis of my content" },
    };
}