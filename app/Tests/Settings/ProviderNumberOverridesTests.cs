using System.Text.Json;

using AIStudio.Models;
using AIStudio.Provider;
using AIStudio.Settings;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what a person's own numbers do to what the rules worked out.
/// </summary>
/// <remarks>
/// Three surfaces write these numbers and all three are checked here, because a number which
/// survives one of them and is lost by another is worse than no number at all: the expert dialog
/// writes the record, an organization writes a Lua table, and both end up in a settings file which
/// has to be read back the way it was written.
/// </remarks>
[TestFixture]
public sealed class ProviderNumberOverridesTests
{
    private static readonly Guid PLUGIN_ID = new("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// A model the rules have a lot to say about, so that an override has something to contradict.
    /// </summary>
    private static readonly ModelProfile WHAT_THE_RULES_SAY = new()
    {
        Capabilities = Capability.TEXT_INPUT | Capability.TEXT_OUTPUT | Capability.MULTIPLE_IMAGE_INPUT,
        Kind = ModelKind.CHAT,
        Context = ContextWindow.Of(131_072, 262_144),
        Images = new(null, 20),
    };

    [Test]
    public void AStatedWindowReplacesTheWholeWindow()
    {
        var overrides = new ProviderCapabilityOverrides { ContextWindowTokens = 32_768 };
        var after = overrides.ApplyTo(WHAT_THE_RULES_SAY);

        Assert.Multiple(() =>
        {
            Assert.That(after.Context.DefaultTokens, Is.EqualTo(32_768));
            Assert.That(after.Context.RaisableToTokens, Is.Null, "What the model card says it could be raised to is not a property of this installation.");
            Assert.That(after.Images, Is.EqualTo(WHAT_THE_RULES_SAY.Images), "Stating a window says nothing about pictures.");
            Assert.That(after.Capabilities, Is.EqualTo(WHAT_THE_RULES_SAY.Capabilities));
        });
    }

    [Test]
    public void SayingNothingKeepsEverythingTheRulesWorkedOut()
    {
        var after = new ProviderCapabilityOverrides().ApplyTo(WHAT_THE_RULES_SAY);

        Assert.Multiple(() =>
        {
            Assert.That(after.Context, Is.EqualTo(WHAT_THE_RULES_SAY.Context));
            Assert.That(after.Images, Is.EqualTo(WHAT_THE_RULES_SAY.Images));
        });
    }

    [Test]
    public void EachImageLimitStandsForItself()
    {
        var overrides = new ProviderCapabilityOverrides { MaxImagesPerMessage = 4 };
        var after = overrides.ApplyTo(WHAT_THE_RULES_SAY);

        Assert.Multiple(() =>
        {
            Assert.That(after.Images.MaxPerMessage, Is.EqualTo(4));
            Assert.That(after.Images.MaxPerRequest, Is.EqualTo(20), "Nobody contradicted the request limit, so it stands.");
            Assert.That(after.Images.MaxInOneMessage, Is.EqualTo(4));
        });
    }

    [Test]
    public void TheSmallerLimitStillDecidesWhatFitsIntoAMessage()
    {
        //
        // A person raising the request limit alone may well see no change, and that is the right
        // answer rather than a defect: the limit standing in their way is the other one, which they
        // have not said anything about. The dialog shows them what is in effect for that reason.
        //
        var rules = WHAT_THE_RULES_SAY with { Images = new(3, null) };
        var after = new ProviderCapabilityOverrides { MaxImagesPerRequest = 100 }.ApplyTo(rules);

        Assert.Multiple(() =>
        {
            Assert.That(after.Images.MaxPerRequest, Is.EqualTo(100));
            Assert.That(after.Images.MaxInOneMessage, Is.EqualTo(3));
        });
    }

    [Test]
    public void NoImagesAtAllIsAnAnswerAndNotAGap()
    {
        var after = new ProviderCapabilityOverrides { MaxImagesPerRequest = 0 }.ApplyTo(WHAT_THE_RULES_SAY);

        Assert.Multiple(() =>
        {
            Assert.That(after.Images.IsKnown, Is.True);
            Assert.That(after.Images.MaxInOneMessage, Is.EqualTo(0));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void AWindowWhichIsNoWidthIsIgnoredRatherThanRepaired(int tokens)
    {
        //
        // Both surfaces which take a number refuse this one with a message, so a value like it came
        // out of a settings file somebody edited by hand. Falling back to what the rules say is the
        // one answer nobody has to invent.
        //
        var after = new ProviderCapabilityOverrides { ContextWindowTokens = tokens }.ApplyTo(WHAT_THE_RULES_SAY);
        Assert.That(after.Context, Is.EqualTo(WHAT_THE_RULES_SAY.Context));
    }

    [Test]
    public void ANegativeCountOfImagesIsIgnoredRatherThanRepaired()
    {
        var after = new ProviderCapabilityOverrides { MaxImagesPerRequest = -5 }.ApplyTo(WHAT_THE_RULES_SAY);
        Assert.That(after.Images, Is.EqualTo(WHAT_THE_RULES_SAY.Images));
    }

    [Test]
    public void AProviderCarryingNothingButANumberIsStillWorthSaving()
    {
        //
        // The dialog throws the record away when this says false, so a person who set nothing but a
        // window would watch their number disappear on the way out of the dialog.
        //
        Assert.Multiple(() =>
        {
            Assert.That(new ProviderCapabilityOverrides { ContextWindowTokens = 8_192 }.HasOverrides, Is.True);
            Assert.That(new ProviderCapabilityOverrides { MaxImagesPerMessage = 1 }.HasOverrides, Is.True);
            Assert.That(new ProviderCapabilityOverrides { MaxImagesPerRequest = 0 }.HasOverrides, Is.True, "Zero is a statement, and the person made it.");
            Assert.That(new ProviderCapabilityOverrides().HasOverrides, Is.False);
        });
    }

    [Test]
    public void ASettingsFileReadsBackWhatItWasWritten()
    {
        var written = new ProviderCapabilityOverrides
        {
            VideoInput = false,
            ContextWindowTokens = 32_768,
            MaxImagesPerMessage = 4,
            MaxImagesPerRequest = 0,
        };

        var json = JsonSerializer.Serialize(written);
        var read = JsonSerializer.Deserialize<ProviderCapabilityOverrides>(json);

        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(written));
            Assert.That(json, Does.Contain("\"CONTEXT_WINDOW\""), "The key names are the surface an administrator sees; they are not free to change.");
            Assert.That(json, Does.Contain("\"MAX_IMAGES_PER_MESSAGE\""));
            Assert.That(json, Does.Contain("\"MAX_IMAGES_PER_REQUEST\""));
            Assert.That(json, Does.Not.Contain("AUDIO_INPUT"), "Saying nothing is not the same as saying null, and a settings file should not be full of it.");
        });
    }

    [Test]
    public async Task WhatTheAppExportsIsWhatAConfigurationPluginCanReadBack()
    {
        var written = new ProviderCapabilityOverrides
        {
            FunctionCalling = true,
            ContextWindowTokens = 65_536,
            MaxImagesPerMessage = 2,
            MaxImagesPerRequest = 8,
        };

        var read = await ParseAsync(written.ExportAsLuaTable(string.Empty));
        Assert.That(read, Is.EqualTo(written));
    }

    [Test]
    public async Task ANumberIsReadTheWayAnAdministratorWroteIt()
    {
        var read = await ParseAsync("""
                                    ["CapabilityOverrides"] = {
                                        ["CONTEXT_WINDOW"] = 32768,
                                        ["max_images_per_request"] = 4,
                                    },
                                    """);

        Assert.That(read, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(read!.ContextWindowTokens, Is.EqualTo(32_768));
            Assert.That(read.MaxImagesPerRequest, Is.EqualTo(4), "The capability words are read loosely too, and a table is read by the app rather than by a compiler.");
        });
    }

    [TestCase("[\"CONTEXT_WINDOW\"] = 0", TestName = "A window of no tokens")]
    [TestCase("[\"CONTEXT_WINDOW\"] = -1", TestName = "A window of negative tokens")]
    [TestCase("[\"CONTEXT_WINDOW\"] = \"32768\"", TestName = "A window written as text")]
    [TestCase("[\"CONTEXT_WINDOW\"] = true", TestName = "A window written as a switch")]
    [TestCase("[\"MAX_IMAGES_PER_REQUEST\"] = -1", TestName = "A negative count of images")]
    [TestCase("[\"MAX_IMAGES_PER_REQUEST\"] = false", TestName = "A count of images written as a switch")]
    public async Task ANumberWhichIsNoneLeavesTheRestOfTheTableStanding(string entry)
    {
        //
        // One unusable line is the line to lose, not the table around it. An organization rolling
        // out a typo would otherwise lose every switch they got right along with it.
        //
        var read = await ParseAsync($$"""
                                      ["CapabilityOverrides"] = {
                                          ["VIDEO_INPUT"] = false,
                                          {{entry}},
                                      },
                                      """);

        Assert.That(read, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(read!.VideoInput, Is.EqualTo(false));
            Assert.That(read.ContextWindowTokens, Is.Null);
            Assert.That(read.MaxImagesPerRequest, Is.Null);
        });
    }

    [Test]
    public async Task ATableOfNothingUsableIsNoOverrideAtAll()
    {
        var read = await ParseAsync("""
                                    ["CapabilityOverrides"] = {
                                        ["CONTEXT_WINDOW"] = 0,
                                    },
                                    """);

        Assert.That(read, Is.Null, "A provider with nothing to say about itself is saved without a record, the way it was before anybody typed.");
    }

    /// <summary>
    /// Reads a provider entry the way a configuration plugin states it.
    /// </summary>
    /// <param name="providerEntry">The lines of the provider table.</param>
    /// <returns>The overrides read from it, or null when there are none.</returns>
    private static async Task<ProviderCapabilityOverrides?> ParseAsync(string providerEntry)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    PROVIDER = {
                                        {{providerEntry}}
                                    }
                                    """);

        if (!state.Environment["PROVIDER"].TryRead<LuaTable>(out var table))
            throw new InvalidOperationException("The entry of this test is not a Lua table.");

        return ProviderCapabilityOverrides.TryParseFromLuaTable(1, table, PLUGIN_ID, NullLogger.Instance);
    }
}