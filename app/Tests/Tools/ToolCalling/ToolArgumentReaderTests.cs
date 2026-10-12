using System.Globalization;
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
/// tool offered, and the refusal has to say which ones those are. The mail tools add booleans,
/// dates, and single lines which go into a search.
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

    [TestCase("true", true)]
    [TestCase("false", false)]
    public void ABooleanComesThroughAsItIs(string value, bool expected)
    {
        Assert.That(ToolArgumentReader.ReadOptionalBoolean(Arguments($$"""{"is_unread":{{value}}}"""), "is_unread", WHEN_LEFT_OUT), Is.EqualTo(expected));
    }

    [TestCase("\"true\"")]
    [TestCase("1")]
    [TestCase("\"yes\"")]
    public void AnythingButABooleanIsRefused(string value)
    {
        var message = Refusal(() => ToolArgumentReader.ReadOptionalBoolean(Arguments($$"""{"is_unread":{{value}}}"""), "is_unread", WHEN_LEFT_OUT));

        Assert.That(message, Does.Contain("'is_unread' must be true or false").And.Contain($"Leave it out {WHEN_LEFT_OUT}."));
    }

    [TestCase("2026-09-01", "2026-09-01T00:00:00+02:00")]
    [TestCase("2026-09-01T14:30", "2026-09-01T14:30:00+02:00")]
    [TestCase("2026-09-01T14:30:15", "2026-09-01T14:30:15+02:00")]
    [TestCase("2026-09-01T14:30:00+05:00", "2026-09-01T14:30:00+05:00")]
    [TestCase("2026-09-01T14:30:00Z", "2026-09-01T14:30:00+00:00")]
    public void ADateWithoutAnOffsetIsReadInTheTimeZoneOfTheUser(string value, string expected)
    {
        var pointInTime = ToolArgumentReader.ReadOptionalDateTime(Arguments($$"""{"after":"{{value}}"}"""), "after", UserTimeZone(), WHEN_LEFT_OUT);
        var expectedPointInTime = DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(pointInTime, Is.EqualTo(expectedPointInTime));
            Assert.That(pointInTime?.Offset, Is.EqualTo(expectedPointInTime.Offset), "The same instant with another offset would hide which zone was assumed.");
        });
    }

    [TestCase("\"yesterday\"")]
    [TestCase("\"01.09.2026\"")]
    [TestCase("\"2026-9-1\"")]
    [TestCase("\"2026-02-30\"")]
    [TestCase("20260901")]
    public void ADateInAnotherFormIsRefused(string value)
    {
        var message = Refusal(() => ToolArgumentReader.ReadOptionalDateTime(Arguments($$"""{"after":{{value}}}"""), "after", UserTimeZone(), WHEN_LEFT_OUT));

        Assert.That(message, Does.Contain("'after' must be a date such as 2026-09-01").And.Contain($"Leave it out {WHEN_LEFT_OUT}."));
    }

    [Test]
    public void ALineComesThroughTrimmed()
    {
        Assert.That(ToolArgumentReader.ReadOptionalLine(Arguments("""{"from":"  alice@example.org "}"""), "from", 20, WHEN_LEFT_OUT), Is.EqualTo("alice@example.org"));
    }

    [TestCase("\"\"", "must not be empty")]
    [TestCase("\"   \"", "must not be empty")]
    [TestCase("\"alice@example.org and bob@example.org\"", "must be at most 20 characters long, but had 37")]
    [TestCase("\"alice\\nbob\"", "must not contain control characters")]
    public void AnEmptyOrLongOrBrokenLineIsRefused(string value, string expectedReason)
    {
        var message = Refusal(() => ToolArgumentReader.ReadOptionalLine(Arguments($$"""{"from":{{value}}}"""), "from", 20, WHEN_LEFT_OUT));

        Assert.That(message, Does.Contain($"'from' {expectedReason}").And.Contain($"Leave it out {WHEN_LEFT_OUT}."), "In a search, an empty line would match everything. Whatever was wrong, leaving the argument out is always a way out.");
    }

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    /// <summary>
    /// A time zone two hours ahead of UTC, all year round, so that no test depends on the computer or on daylight saving time.
    /// </summary>
    private static TimeZoneInfo UserTimeZone() => TimeZoneInfo.CreateCustomTimeZone("AI Studio test zone", TimeSpan.FromHours(2), "AI Studio test zone", "AI Studio test zone");

    /// <summary>
    /// Runs a reader which has to refuse its argument and returns what it said.
    /// </summary>
    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;
}