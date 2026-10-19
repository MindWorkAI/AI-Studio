using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Lua;
using Lua.Standard;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how the providers of a configuration plugin read the options for the API key of the user.
/// </summary>
/// <remarks>
/// All three kinds of providers read both options through UserProvidedAPIKeyOptions. The tests read each
/// kind the way a configuration plugin states it, so a kind which drops one of the options fails here.
/// </remarks>
[TestFixture]
public sealed class UserProvidedAPIKeyOptionsTests
{
    private const string PROVIDER_ID = "8b2f4c6d-1e3a-4f5b-9c7d-2a4e6f8b0c1d";

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public async Task WithoutAnOptionTheOrganizationManagesTheAPIKey(PluginConfigurationObjectType type)
    {
        Assert.That(await ReadOptionsAsync(type), Is.EqualTo(new UserProvidedAPIKeyOptions(AllowUserProvidedAPIKey: false, PromptForUserAPIKey: false)));
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public async Task AllowingTheAPIKeyOfTheUserDoesNotAskForIt(PluginConfigurationObjectType type)
    {
        Assert.That(await ReadOptionsAsync(type, """["AllowUserProvidedAPIKey"] = true"""), Is.EqualTo(new UserProvidedAPIKeyOptions(AllowUserProvidedAPIKey: true, PromptForUserAPIKey: false)));
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public async Task AskingForTheAPIKeyOfTheUserAlsoAllowsIt(PluginConfigurationObjectType type)
    {
        Assert.That(await ReadOptionsAsync(type, """["PromptForUserAPIKey"] = true"""), Is.EqualTo(new UserProvidedAPIKeyOptions(AllowUserProvidedAPIKey: true, PromptForUserAPIKey: true)));
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public async Task AskingForTheAPIKeyOfTheUserOverridesAllowingNone(PluginConfigurationObjectType type)
    {
        Assert.That(await ReadOptionsAsync(type, """["AllowUserProvidedAPIKey"] = false""", """["PromptForUserAPIKey"] = true"""), Is.EqualTo(new UserProvidedAPIKeyOptions(AllowUserProvidedAPIKey: true, PromptForUserAPIKey: true)));
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public async Task AnOptionWhichIsNoBooleanCountsAsNotSet(PluginConfigurationObjectType type)
    {
        Assert.That(await ReadOptionsAsync(type, """["PromptForUserAPIKey"] = "yes" """), Is.EqualTo(new UserProvidedAPIKeyOptions(AllowUserProvidedAPIKey: false, PromptForUserAPIKey: false)));
    }

    /// <summary>
    /// Exports a provider, adds the given options to it, and reads it back the way a configuration plugin does.
    /// </summary>
    /// <param name="type">The kind of provider.</param>
    /// <param name="options">The options as Lua table fields, e.g., ["PromptForUserAPIKey"] = true.</param>
    /// <returns>The options of the provider which was read back.</returns>
    private static async Task<UserProvidedAPIKeyOptions> ReadOptionsAsync(PluginConfigurationObjectType type, params string[] options)
    {
        var luaCode = type switch
        {
            PluginConfigurationObjectType.LLM_PROVIDER => new AIStudio.Settings.Provider(1, PROVIDER_ID, "Chat provider", LLMProviders.OPEN_AI, new Model("gpt-6", "GPT-6")).ExportAsConfigurationSection(),
            PluginConfigurationObjectType.EMBEDDING_PROVIDER => new EmbeddingProvider(1, PROVIDER_ID, "Embedding provider", LLMProviders.OPEN_AI, new Model("text-embedding-3-small", "text-embedding-3-small")).ExportAsConfigurationSection(),
            PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER => new TranscriptionProvider(1, PROVIDER_ID, "Transcription provider", LLMProviders.OPEN_AI, new Model("whisper-1", "Whisper")).ExportAsConfigurationSection(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "This type is no provider.")
        };

        var descriptor = PluginConfigurationObjectDescriptors.Get(type);
        var section = descriptor.LuaTableName;
        var optionLines = string.Join(Environment.NewLine, options.Select(option => $"CONFIG[\"{section}\"][1]{option}"));

        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    CONFIG = {}
                                    CONFIG["{{section}}"] = {}
                                    {{luaCode}}
                                    {{optionLines}}
                                    """);

        if (!state.Environment["CONFIG"].TryRead<LuaTable>(out var configTable) ||
            !configTable[section].TryRead<LuaTable>(out var providersTable) ||
            !providersTable[1].TryRead<LuaTable>(out var providerTable))
            throw new InvalidOperationException("The code of this test did not produce a provider table.");

        if (!descriptor.TryParseSnippet(providerTable, out var provider) || provider is not IUserProvidedAPIKey apiKeyOptions)
            throw new InvalidOperationException("The provider of this test could not be read.");

        return new UserProvidedAPIKeyOptions(apiKeyOptions.AllowUserProvidedAPIKey, apiKeyOptions.PromptForUserAPIKey);
    }
}