using System.Text.Json;

using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks the readers every tool shares, where the web search tests do not already.
/// </summary>
/// <remarks>
/// The web search tests cover strings, positive integers, and a single choice through the
/// arguments of that tool. What is left are a required string which arrives empty and a list of
/// choices, which the web search does not have: a data source a model names has to be one the
/// tool offered, and the refusal has to say which ones those are.
/// </remarks>
[TestFixture]
public sealed class ToolArgumentReaderTests
{
    private static readonly string[] OFFERED = ["alpha", "beta", "gamma"];

    private const string WHEN_LEFT_OUT = "to search all of them";

    [TestCase("""{"query":""}""")]
    [TestCase("""{"query":"   "}""")]
    public void AnEmptyRequiredStringIsRefusedAsEmpty(string json)
    {
        var message = Refusal(() => ToolArgumentReader.ReadRequiredString(Arguments(json), "query"));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'query'").And.Contain("a non-empty string"), "The argument arrived, so calling it missing would make the model look for a typo in the name.");
            Assert.That(message, Does.Not.Contain("Leave it out"));
        });
    }

    [TestCase("""{}""")]
    [TestCase("""{"ids":null}""")]
    public void AListLeftOutIsNotSet(string json)
    {
        Assert.That(ToolArgumentReader.ReadOptionalChoices(Arguments(json), "ids", OFFERED, WHEN_LEFT_OUT), Is.Null);
    }

    [Test]
    public void OfferedValuesComeThroughOnceEachInTheirOrder()
    {
        var choices = ToolArgumentReader.ReadOptionalChoices(Arguments("""{"ids":["gamma"," alpha ","gamma"]}"""), "ids", OFFERED, WHEN_LEFT_OUT);
        Assert.That(choices, Is.EqualTo(new[] { "gamma", "alpha" }));
    }

    [TestCase("[]")]
    [TestCase("\"alpha\"")]
    [TestCase("5")]
    public void AnEmptyListOrNoListIsRefusedWithTheValuesThatWouldDo(string value)
    {
        var message = Refusal(() => ToolArgumentReader.ReadOptionalChoices(Arguments($$"""{"ids":{{value}}}"""), "ids", OFFERED, WHEN_LEFT_OUT));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'ids'").And.Contain("a list of one or more of alpha, beta, gamma"));
            Assert.That(message, Does.Contain($"but was {value}."));
            Assert.That(message, Does.Contain($"Leave it out {WHEN_LEFT_OUT}."), "An empty list asks for nothing; what leaving it out does is the way the model wanted.");
        });
    }

    [TestCase("\"delta\"")]
    [TestCase("\"Alpha\"")]
    [TestCase("5")]
    [TestCase("null")]
    public void AValueNotOfferedIsRefusedOnItsOwn(string value)
    {
        var message = Refusal(() => ToolArgumentReader.ReadOptionalChoices(Arguments($$"""{"ids":["alpha",{{value}}]}"""), "ids", OFFERED, WHEN_LEFT_OUT));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'ids'").And.Contain("one of alpha, beta, gamma"));
            Assert.That(message, Does.Contain($"but one was {value}."), "The model has to find out which of its values the tool means.");
            Assert.That(message, Does.Not.Contain("\"alpha\""), "Only the wrong value comes back, not the whole list the model sent.");
            Assert.That(message, Does.Contain("Leave it out"));
        });
    }

    [Test]
    public void AGuidIsRepeatedBackWhole()
    {
        var id = Guid.NewGuid().ToString();
        var message = Refusal(() => ToolArgumentReader.ReadOptionalChoices(Arguments($$"""{"ids":["{{id}}"]}"""), "ids", OFFERED, WHEN_LEFT_OUT));

        Assert.That(message, Does.Contain($"but one was \"{id}\"."), "Data sources are named by their GUIDs; a shortened one would leave the model guessing.");
    }

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    /// <summary>
    /// Runs a reader which has to refuse its argument and returns what it said.
    /// </summary>
    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;
}