using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Models.Plugins;
using AIStudio.Provider;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Models.Plugins;

/// <summary>
/// Checks what AI Studio makes of a model an organization describes in a plugin of its own.
/// </summary>
/// <remarks>
/// The entries below are written the way they are written in a plugin.lua, and they are read
/// through a real Lua state rather than through a table put together in C#. What is being checked
/// is the wire format an administrator types, so anything between their file and the declaration
/// has to be part of the test.
/// </remarks>
[TestFixture]
public sealed class ModelDeclarationTests
{
    private static readonly Guid PLUGIN_ID = new("11111111-1111-1111-1111-111111111111");

    private const string ORIGIN = "Models of a company";

    private const string A_COMPLETE_DECLARATION = """
                                                  ["PATTERN"] = "acme-assistant",
                                                  ["MATCH"] = "PREFIX",
                                                  ["CAPABILITIES"] = { "TEXT_INPUT", "MULTIPLE_IMAGE_INPUT", "TEXT_OUTPUT", "FUNCTION_CALLING", "CHAT_COMPLETION_API" },
                                                  ["REASONING"] = "ON_BY_DEFAULT",
                                                  ["KIND"] = "CHAT",
                                                  ["CONTEXT_WINDOW"] = 131072,
                                                  ["CONTEXT_WINDOW_RAISABLE_TO"] = 262144,
                                                  ["TOKENIZER_KIND"] = "HUGGING_FACE",
                                                  ["TOKENIZER_ID"] = "acme/assistant",
                                                  ["MAX_IMAGES_PER_MESSAGE"] = 1,
                                                  ["MAX_IMAGES_PER_REQUEST"] = 8,
                                                  ["SOURCE_URL"] = "https://intranet.invalid/ai/acme-assistant",
                                                  ["SOURCE_CHECKED_ON"] = "2026-09-12",
                                                  ["SOURCE_NOTE"] = "Internal model card: tools, images, 128k context",
                                                  """;

    private const string THE_LEAST_A_DECLARATION_CAN_SAY = """
                                                           ["PATTERN"] = "acme-assistant",
                                                           ["CAPABILITIES"] = { "TEXT_INPUT", "TEXT_OUTPUT", "CHAT_COMPLETION_API" },
                                                           ["SOURCE_URL"] = "https://intranet.invalid/ai/acme-assistant",
                                                           ["SOURCE_CHECKED_ON"] = "2026-09-12",
                                                           """;

    [Test]
    public async Task ADeclarationIsReadTheWayItWasWritten()
    {
        var declaration = await ReadAsync(A_COMPLETE_DECLARATION);

        Assert.That(declaration, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(declaration!.Pattern.Text, Is.EqualTo("acme-assistant"));
            Assert.That(declaration.Pattern.Kind, Is.EqualTo(MatchKind.PREFIX));
            Assert.That(declaration.Change.Adds, Is.EqualTo(Capability.TEXT_INPUT | Capability.MULTIPLE_IMAGE_INPUT | Capability.TEXT_OUTPUT | Capability.FUNCTION_CALLING | Capability.CHAT_COMPLETION_API));
            Assert.That(declaration.Change.Reasoning, Is.EqualTo(ReasoningSupport.ON_BY_DEFAULT));
            Assert.That(declaration.Change.Kind, Is.EqualTo(ModelKind.CHAT));
            Assert.That(declaration.Change.Context, Is.EqualTo(ContextWindow.Of(131_072, 262_144)));
            Assert.That(declaration.Change.Tokenizer, Is.EqualTo(new TokenizerRef(TokenizerKind.HUGGING_FACE, "acme/assistant")));
            Assert.That(declaration.Change.Images, Is.EqualTo(new ImageLimits(1, 8)));
            Assert.That(declaration.Source.CheckedOn, Is.EqualTo(new DateOnly(2026, 9, 12)));
            Assert.That(declaration.EnterpriseConfigurationPluginId, Is.EqualTo(PLUGIN_ID));
        });
    }

    [Test]
    public async Task WhatADeclarationLeavesOutIsTheSameAsWhatAFamilyLeavesOut()
    {
        var declaration = await ReadAsync(THE_LEAST_A_DECLARATION_CAN_SAY);

        Assert.That(declaration, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(declaration!.Pattern.Kind, Is.EqualTo(MatchKind.SEGMENT), "The kind to reach for by default, here as everywhere else.");
            Assert.That(declaration.Change.Reasoning, Is.EqualTo(ReasoningSupport.NONE));
            Assert.That(declaration.Change.Kind, Is.EqualTo(ModelKind.CHAT), "A model nobody said anything else about stays visible in the chat lists.");
            Assert.That(declaration.Change.Context, Is.Null);
            Assert.That(declaration.Change.Tokenizer, Is.Null);
            Assert.That(declaration.Change.Images, Is.Null);
        });
    }

    [Test]
    public async Task ADeclarationWithoutCapabilitiesIsRefused()
    {
        //
        // The one thing a declaration cannot leave out. It replaces what AI Studio would otherwise
        // say about these models, so an entry naming only a context window would take away every
        // capability the built-in rules knew -- and it would do so silently, because an entry which
        // matches is an answer.
        //
        var declaration = await ReadAsync("""
                                          ["PATTERN"] = "acme-assistant",
                                          ["CONTEXT_WINDOW"] = 131072,
                                          ["SOURCE_URL"] = "https://intranet.invalid/ai/acme-assistant",
                                          ["SOURCE_CHECKED_ON"] = "2026-09-12",
                                          """);

        Assert.That(declaration, Is.Null);
    }

    [TestCase("""["PATTERN"] = "Acme-Assistant",""", TestName = "A pattern in capitals", Description = "Names arrive in lower case, so this could never match.")]
    [TestCase("""["PATTERN"] = "acme_assistant",""", TestName = "A pattern with an underscore")]
    [TestCase("""["PATTERN"] = "acme assistant",""", TestName = "A pattern with a space")]
    [TestCase("""["PATTERN"] = "",""", TestName = "No pattern at all")]
    public async Task APatternWhichCouldNeverMatchAnythingIsRefused(string pattern)
    {
        var declaration = await ReadAsync($$"""
                                            {{pattern}}
                                            ["CAPABILITIES"] = { "TEXT_INPUT", "TEXT_OUTPUT" },
                                            ["SOURCE_URL"] = "https://intranet.invalid/ai/acme-assistant",
                                            ["SOURCE_CHECKED_ON"] = "2026-09-12",
                                            """);

        Assert.That(declaration, Is.Null, "A pattern which is not written the way a model name is written is a mistake, not a rule which happens to stay quiet.");
    }

    [TestCase("ALWAYS_REASONING")]
    [TestCase("OPTIONAL_REASONING")]
    [TestCase("REASONING_BY_DEFAULT")]
    public async Task ReasoningStatedAsACapabilityIsRefusedRatherThanDropped(string reasoningWord)
    {
        //
        // The three words are the vocabulary of the expert settings, where a person answers three
        // questions with yes and no. Here one key says how a model reasons, and the three of them
        // together can state answers no model can give. A profile drops them anyway, so accepting
        // them would mean an administrator wrote something that never took effect.
        //
        var declaration = await ReadAsync($$"""
                                            ["PATTERN"] = "acme-assistant",
                                            ["CAPABILITIES"] = { "TEXT_INPUT", "TEXT_OUTPUT", "{{reasoningWord}}" },
                                            ["SOURCE_URL"] = "https://intranet.invalid/ai/acme-assistant",
                                            ["SOURCE_CHECKED_ON"] = "2026-09-12",
                                            """);

        Assert.That(declaration, Is.Null);
    }

    [TestCase("""["SOURCE_CHECKED_ON"] = "2026-09-12",""", TestName = "A page nobody named")]
    [TestCase("""["SOURCE_URL"] = "https://intranet.invalid/ai",""", TestName = "A day nobody named")]
    [TestCase("""["SOURCE_URL"] = "https://intranet.invalid/ai", ["SOURCE_CHECKED_ON"] = "12.09.2026",""", TestName = "A day written another way")]
    public async Task ADeclarationHasToSayWhereItWasReadAndWhen(string source)
    {
        //
        // The compiler asks a family in the source for this, and an organization's declaration
        // outlives whoever wrote it just the same. Naming the page and the day is what lets the next
        // administrator find out in a minute whether it still holds.
        //
        var declaration = await ReadAsync($$"""
                                            ["PATTERN"] = "acme-assistant",
                                            ["CAPABILITIES"] = { "TEXT_INPUT", "TEXT_OUTPUT" },
                                            {{source}}
                                            """);

        Assert.That(declaration, Is.Null);
    }

    [TestCase("""["TOKENIZER_KIND"] = "HUGGING_FACE",""", TestName = "A tokenizer kind without an ID")]
    [TestCase("""["TOKENIZER_ID"] = "acme/assistant",""", TestName = "A tokenizer ID without a kind")]
    [TestCase("""["CONTEXT_WINDOW_RAISABLE_TO"] = 262144,""", TestName = "A ceiling without a window")]
    [TestCase("""["CONTEXT_WINDOW"] = 262144, ["CONTEXT_WINDOW_RAISABLE_TO"] = 131072,""", TestName = "A ceiling below the window")]
    [TestCase("""["CONTEXT_WINDOW"] = 0,""", TestName = "A window of no tokens")]
    [TestCase("""["MAX_IMAGES_PER_REQUEST"] = -1,""", TestName = "Fewer than no images")]
    [TestCase("""["KIND"] = "SOMETHING_ELSE",""", TestName = "A kind of model nobody knows")]
    [TestCase("""["MATCH"] = "REGEX",""", TestName = "A way of matching which does not exist")]
    [TestCase("""["ONLY_ON"] = "ACME_CLOUD",""", TestName = "A provider which does not exist")]
    public async Task AnEntryWhichSaysSomethingUnreadableIsRefusedAsAWhole(string addition)
    {
        //
        // Never read in part: a declaration is one statement, and half of one would answer for the
        // models it matches just as firmly as a complete one, with the unreadable half missing and
        // nothing on screen saying so.
        //
        var declaration = await ReadAsync($"""
                                           {THE_LEAST_A_DECLARATION_CAN_SAY}
                                           {addition}
                                           """);

        Assert.That(declaration, Is.Null);
    }

    [Test]
    public async Task TwoDeclarationsCollideExactlyWhenTheyClaimTheSameNames()
    {
        //
        // What identifies a declaration is its pattern, because that is what a collision is here.
        // Two of them claiming the same names would both enter the index and tie there, and a tie
        // is something only a person can settle. Two about different names never meet.
        //
        var declaration = await ReadAsync(A_COMPLETE_DECLARATION);
        var theSameNames = await ReadAsync(A_COMPLETE_DECLARATION.Replace("""["CONTEXT_WINDOW"] = 131072,""", """["CONTEXT_WINDOW"] = 65536,""", StringComparison.Ordinal));
        var otherNames = await ReadAsync(A_COMPLETE_DECLARATION.Replace("""["MATCH"] = "PREFIX",""", """["MATCH"] = "SEGMENT",""", StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(theSameNames?.Id, Is.EqualTo(declaration?.Id), "The same pattern, so one of the two has to win.");
            Assert.That(otherNames?.Id, Is.Not.EqualTo(declaration?.Id), "Bound to the name differently, so they claim different sets of names.");
        });
    }

    private static async Task<ModelDeclaration?> ReadAsync(string entry)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    MODEL = {
                                        {{entry}}
                                    }
                                    """);

        if (!state.Environment["MODEL"].TryRead<LuaTable>(out var table))
            throw new InvalidOperationException("The entry of this test is not a Lua table.");

        return ModelDeclaration.TryParse(1, table, PLUGIN_ID, ORIGIN, NullLogger.Instance, out var declaration) ? declaration : null;
    }
}