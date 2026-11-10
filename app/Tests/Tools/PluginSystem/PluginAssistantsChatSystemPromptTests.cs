using AIStudio.Assistants;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

using Lua;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks how an assistant plugin chooses the system prompt of a chat which continues its conversation.
/// </summary>
/// <remarks>
/// The choice is optional, so plugins written before it existed keep loading, and their chats keep the
/// default prompt of AI Studio. A choice which cannot work, though, keeps the plugin from loading: its
/// author would otherwise only notice it in the chat, if at all.
/// </remarks>
[TestFixture]
public sealed class PluginAssistantsChatSystemPromptTests
{
    private const string PROMPT_FOR_THE_CHAT = "You explain plain language and answer follow-up questions about the translation.";

    [Test]
    public async Task WithoutAChoiceTheChatGetsTheDefaultPrompt()
    {
        using var state = PluginFactory.CreateLuaState(null);
        var table = await AssistantTableAsync(state, """["Title"] = "Plain language" """);

        var isValid = PluginAssistants.TryReadChatSystemPrompt(table, out var choice, out var rawCustomPrompt, out var message);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True, message);
            Assert.That(choice.Source, Is.EqualTo(ChatSystemPromptSource.AI_STUDIO_DEFAULT));
            Assert.That(rawCustomPrompt, Is.Empty);
        }
    }

    [TestCase("AI_STUDIO_DEFAULT", ChatSystemPromptSource.AI_STUDIO_DEFAULT)]
    [TestCase("ASSISTANT", ChatSystemPromptSource.ASSISTANT)]
    [TestCase("assistant", ChatSystemPromptSource.ASSISTANT)]
    public async Task APluginChoosesTheDefaultPromptOrItsOwnSystemPrompt(string value, ChatSystemPromptSource expectedSource)
    {
        using var state = PluginFactory.CreateLuaState(null);
        var table = await AssistantTableAsync(state, $"""["ChatSystemPrompt"] = "{value}" """);

        var isValid = PluginAssistants.TryReadChatSystemPrompt(table, out var choice, out var rawCustomPrompt, out var message);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True, message);
            Assert.That(choice.Source, Is.EqualTo(expectedSource));
            Assert.That(rawCustomPrompt, Is.Empty);
        }
    }

    [Test]
    public async Task APromptForTheChatGetsTheSecurityPolicyOfThePlugin()
    {
        using var state = PluginFactory.CreateLuaState(null);
        var table = await AssistantTableAsync(state, $"""["ChatSystemPrompt"] = "CUSTOM", ["CustomChatSystemPrompt"] = "  {PROMPT_FOR_THE_CHAT}  " """);

        var isValid = PluginAssistants.TryReadChatSystemPrompt(table, out var choice, out var rawCustomPrompt, out var message);
        var prompt = choice.CreatePrompt("The system prompt of the assistant.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True, message);
            Assert.That(choice.Source, Is.EqualTo(ChatSystemPromptSource.CUSTOM));
            Assert.That(rawCustomPrompt, Is.EqualTo(PROMPT_FOR_THE_CHAT), "The audit reads the prompt as the plugin states it.");
            Assert.That(prompt, Does.Contain(PROMPT_FOR_THE_CHAT));
            Assert.That(prompt, Does.StartWith("You are a secure assistant"), "The prompt for the chat must not escape the security policy every prompt of a plugin gets.");
            Assert.That(prompt, Does.Contain("Security reminder"));
        }
    }

    [TestCase("\"UNKNOWN\"")]
    [TestCase("\"1\"")]
    [TestCase("1")]
    [TestCase("true")]
    [TestCase("\"\"")]
    public async Task AnInvalidChoiceKeepsThePluginFromLoading(string luaValue)
    {
        using var state = PluginFactory.CreateLuaState(null);
        var table = await AssistantTableAsync(state, $"""["ChatSystemPrompt"] = {luaValue}""");

        var isValid = PluginAssistants.TryReadChatSystemPrompt(table, out _, out _, out var message);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(message, Is.Not.Empty);
        }
    }

    [TestCase("""["ChatSystemPrompt"] = "CUSTOM" """)]
    [TestCase("""["ChatSystemPrompt"] = "CUSTOM", ["CustomChatSystemPrompt"] = "   " """)]
    [TestCase("""["ChatSystemPrompt"] = "CUSTOM", ["CustomChatSystemPrompt"] = 42""")]
    [TestCase("""["ChatSystemPrompt"] = "ASSISTANT", ["CustomChatSystemPrompt"] = "Answer follow-up questions." """)]
    [TestCase("""["CustomChatSystemPrompt"] = "Answer follow-up questions." """)]
    public async Task APromptForTheChatNeedsTheCustomChoiceAndViceVersa(string fields)
    {
        using var state = PluginFactory.CreateLuaState(null);
        var table = await AssistantTableAsync(state, fields);

        var isValid = PluginAssistants.TryReadChatSystemPrompt(table, out _, out _, out var message);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(message, Is.Not.Empty);
        }
    }

    private static async Task<LuaTable> AssistantTableAsync(LuaState state, string fields)
    {
        var results = await state.DoStringAsync($"return {{ {fields} }}");
        Assert.That(results[0].TryRead<LuaTable>(out var table), Is.True, "The test needs a Lua table.");
        return table;
    }
}