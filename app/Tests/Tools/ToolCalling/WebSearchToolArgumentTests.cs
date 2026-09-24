using System.Text.Json;

using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks how the web search reads the arguments a model passes, and what it says about wrong ones.
/// </summary>
/// <remarks>
/// A wrong argument is refused rather than guessed at: a placeholder such as 0 is not a page, and
/// quietly reading it as "no page" would do something the model did not ask for. What makes the
/// refusal work is its message. The model reads it and tries again, so it has to say what arrived,
/// what would have been right, and that an optional argument may simply be left out. A model
/// which believes it has to pass one otherwise keeps trying placeholders, and every attempt costs
/// one of the tool calls an answer may make.
/// </remarks>
[TestFixture]
public sealed class WebSearchToolArgumentTests
{
    [Test]
    public void AnArgumentLeftOutIsNotSet()
    {
        var arguments = Arguments("""{"query":"weather"}""");

        Assert.Multiple(() =>
        {
            Assert.That(WebSearchTool.ReadLanguage(arguments), Is.Null);
            Assert.That(WebSearchTool.ReadTimeRange(arguments), Is.Null);
            Assert.That(WebSearchTool.ReadPage(arguments), Is.Null);
            Assert.That(WebSearchTool.ReadLimit(arguments), Is.Null);
        });
    }

    [Test]
    public void ANullArgumentIsTheSameAsOneLeftOut()
    {
        var arguments = Arguments("""{"query":"weather","language":null,"time_range":null,"page":null,"limit":null}""");

        Assert.Multiple(() =>
        {
            Assert.That(WebSearchTool.ReadLanguage(arguments), Is.Null);
            Assert.That(WebSearchTool.ReadTimeRange(arguments), Is.Null);
            Assert.That(WebSearchTool.ReadPage(arguments), Is.Null);
            Assert.That(WebSearchTool.ReadLimit(arguments), Is.Null);
        });
    }

    [Test]
    public void ValidArgumentsComeThrough()
    {
        var arguments = Arguments("""{"query":"  weather  ","language":"de-DE","time_range":"day","page":2,"limit":7}""");

        Assert.Multiple(() =>
        {
            Assert.That(WebSearchTool.ReadQuery(arguments), Is.EqualTo("weather"));
            Assert.That(WebSearchTool.ReadLanguage(arguments), Is.EqualTo("de-DE"));
            Assert.That(WebSearchTool.ReadTimeRange(arguments), Is.EqualTo("day"));
            Assert.That(WebSearchTool.ReadPage(arguments), Is.EqualTo(2));
            Assert.That(WebSearchTool.ReadLimit(arguments), Is.EqualTo(7));
        });
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("2.5")]
    [TestCase("\"5\"")]
    [TestCase("true")]
    public void AWrongPageIsRefusedWithWhatArrived(string value)
    {
        var message = Refusal(() => WebSearchTool.ReadPage(Arguments($$"""{"query":"weather","page":{{value}}}""")));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'page'").And.Contain("a positive integer"));
            Assert.That(message, Does.Contain($"but was {value}."), "Without the value, the model cannot tell which of its arguments the tool means.");
            Assert.That(message, Does.Contain("Leave it out"), "The way out a model needs when it believes the argument is required.");
        });
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("\"5\"")]
    public void AWrongLimitIsRefusedWithWhatArrived(string value)
    {
        var message = Refusal(() => WebSearchTool.ReadLimit(Arguments($$"""{"query":"weather","limit":{{value}}}""")));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'limit'").And.Contain("a positive integer"));
            Assert.That(message, Does.Contain($"but was {value}."));
            Assert.That(message, Does.Contain("Leave it out"));
        });
    }

    [TestCase("\"\"")]
    [TestCase("\"Day\"")]
    [TestCase("\"week\"")]
    [TestCase("5")]
    public void AWrongTimeRangeIsRefusedWithTheValuesThatWouldDo(string value)
    {
        var message = Refusal(() => WebSearchTool.ReadTimeRange(Arguments($$"""{"query":"weather","time_range":{{value}}}""")));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'time_range'").And.Contain("one of day, month, year"));
            Assert.That(message, Does.Contain($"but was {value}."));
            Assert.That(message, Does.Contain("Leave it out"));
        });
    }

    [Test]
    public void ALanguageWhichIsNoStringIsRefused()
    {
        var message = Refusal(() => WebSearchTool.ReadLanguage(Arguments("""{"query":"weather","language":5}""")));
        Assert.That(message, Does.Contain("'language'").And.Contain("but was 5.").And.Contain("Leave it out"));
    }

    [Test]
    public void AMissingQueryIsRefused()
    {
        var message = Refusal(() => WebSearchTool.ReadQuery(Arguments("""{"page":1}""")));
        Assert.That(message, Does.Contain("Missing required argument 'query'"));
    }

    [Test]
    public void AQueryWhichIsNoStringIsRefusedWithoutOfferingToLeaveItOut()
    {
        var message = Refusal(() => WebSearchTool.ReadQuery(Arguments("""{"query":42}""")));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Contain("'query'").And.Contain("but was 42."));
            Assert.That(message, Does.Not.Contain("Leave it out"), "The query is required, so leaving it out is no way out.");
        });
    }

    [Test]
    public void ALongValueIsShortenedInTheMessage()
    {
        var longValue = new string('x', 500);
        var message = Refusal(() => WebSearchTool.ReadTimeRange(Arguments($$"""{"query":"weather","time_range":"{{longValue}}"}""")));

        Assert.Multiple(() =>
        {
            Assert.That(message, Does.Not.Contain(longValue), "The model sent the value itself; repeating all of it back only costs tokens.");
            Assert.That(message, Does.Contain("..."));
        });
    }

    private static JsonElement Arguments(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    /// <summary>
    /// Runs a reader which has to refuse its argument and returns what it said.
    /// </summary>
    private static string Refusal(TestDelegate read) => Assert.Throws<ArgumentException>(read)!.Message;
}