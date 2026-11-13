using AIStudio.Assistants;
using AIStudio.Chat;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks which system prompt a chat gets for each choice an assistant can make.
/// </summary>
[TestFixture]
public sealed class ChatSystemPromptChoiceTests
{
    private const string PROMPT_OF_ASSISTANT = "You are a translation engine. Only translate the text.";
    private const string PROMPT_FOR_THE_CHAT = "You are an expert in German and answer questions about a translation.";

    [Test]
    public void TheDefaultChoiceGivesTheDefaultPromptOfAIStudio() => Assert.That(ChatSystemPromptChoice.AI_STUDIO_DEFAULT.CreatePrompt(PROMPT_OF_ASSISTANT), Is.EqualTo(SystemPrompts.DEFAULT));

    [Test]
    public void TheAssistantChoiceGivesThePromptOfTheAssistant() => Assert.That(ChatSystemPromptChoice.ASSISTANT.CreatePrompt(PROMPT_OF_ASSISTANT), Is.EqualTo(PROMPT_OF_ASSISTANT));

    [Test]
    public void ACustomChoiceGivesThePromptTheAssistantWroteForTheChat()
    {
        var choice = ChatSystemPromptChoice.Custom(() => PROMPT_FOR_THE_CHAT);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Source, Is.EqualTo(ChatSystemPromptSource.CUSTOM));
            Assert.That(choice.CreatePrompt(PROMPT_OF_ASSISTANT), Is.EqualTo(PROMPT_FOR_THE_CHAT));
        }
    }

    [Test]
    public void ACustomPromptIsBuiltOnlyWhenTheChatNeedsIt()
    {
        var builds = 0;
        var choice = ChatSystemPromptChoice.Custom(() =>
        {
            builds++;
            return PROMPT_FOR_THE_CHAT;
        });

        Assert.That(choice.Source, Is.EqualTo(ChatSystemPromptSource.CUSTOM));
        Assert.That(builds, Is.Zero, "Reading the choice must not build the prompt, which may need the state of the assistant.");

        choice.CreatePrompt(PROMPT_OF_ASSISTANT);
        Assert.That(builds, Is.EqualTo(1));
    }
}