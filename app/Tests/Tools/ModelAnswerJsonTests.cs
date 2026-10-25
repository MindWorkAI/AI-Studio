using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the JSON part is read out of a model answer, for the kinds of answers models write and
/// the callers rely on: the Assistant Builder, the launcher texts, and the Slide Assistant.
/// </summary>
[TestFixture]
public sealed class ModelAnswerJsonTests
{
    [Test]
    public void AnObjectAloneIsReadAsItIs()
    {
        const string ANSWER = """{ "title": "Mars" }""";

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.EqualTo(ANSWER));
    }

    [Test]
    public void AnObjectIsReadOutOfProseAndACodeFence()
    {
        const string ANSWER = """
            Here is your plugin:
            ```json
            {
              "schema_version": "v1",
              "plugin": { "name": "Mails", "categories": ["Writing"] },
              "assistant": { "kind": "FORM", "tool_ids": ["web_search"] }
            }
            ```
            Let me know if you need anything else.
            """;

        const string JSON = """
            {
              "schema_version": "v1",
              "plugin": { "name": "Mails", "categories": ["Writing"] },
              "assistant": { "kind": "FORM", "tool_ids": ["web_search"] }
            }
            """;

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.EqualTo(JSON));
    }

    [Test]
    public void BracesAndBracketsInsideStringsDoNotCount()
    {
        const string JSON = """{ "full_lua": "local t = { \"}\", ']' }\nreturn { [1] = t }", "kind": "FORM" }""";

        Assert.That(ModelAnswerJson.ExtractObject($"Here it is: {JSON} Enjoy!").ToString(), Is.EqualTo(JSON));
    }

    [Test]
    public void EscapedQuotesAndBackslashesInsideStringsAreRespected()
    {
        const string JSON = """{ "path": "C:\\", "quote": "She said \"}\" and left", "pattern": "%\\%s", "next": { "level": 1 } }""";

        Assert.That(ModelAnswerJson.ExtractObject(JSON).ToString(), Is.EqualTo(JSON), "A string ending with an escaped backslash still ends at its quote.");
    }

    [Test]
    public void BracesInProseBeforeTheObjectAreSkipped()
    {
        const string ANSWER = """
            I replaced {name} and { date } in your template:
            { "title": "Mars" }
            """;

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.EqualTo("""{ "title": "Mars" }"""));
    }

    [Test]
    public void AnOpenBraceInProseDoesNotSwallowTheObject()
    {
        const string ANSWER = """
            A Lua table starts with { and ends further below.
            { "title": "Mars" }
            """;

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.EqualTo("""{ "title": "Mars" }"""));
    }

    [Test]
    public void AnObjectMayStartWithAComment()
    {
        const string JSON = """
            {
              // The deck about Mars
              "title": "Mars"
            }
            """;

        Assert.That(ModelAnswerJson.ExtractObject(JSON).ToString(), Is.EqualTo(JSON), "The Slide Assistant allows comments.");
    }

    [Test]
    public void AnEmptyObjectIsAnObject()
    {
        Assert.That(ModelAnswerJson.ExtractObject("Nothing to report: {}").ToString(), Is.EqualTo("{}"));
    }

    [Test]
    public void AnObjectInsideAListIsReadAsTheObject()
    {
        Assert.That(ModelAnswerJson.ExtractObject("""[{ "title": "Mars" }]""").ToString(), Is.EqualTo("""{ "title": "Mars" }"""));
    }

    [Test]
    public void OfTwoObjectsTheFirstIsRead()
    {
        Assert.That(ModelAnswerJson.ExtractObject("""{ "title": "Mars" } or { "title": "Venus" }""").ToString(), Is.EqualTo("""{ "title": "Mars" }"""));
    }

    [Test]
    public void ProseAfterTheObjectIsIgnored()
    {
        Assert.That(ModelAnswerJson.ExtractObject("""{ "title": "Mars" } Replace {name} as you like.""").ToString(), Is.EqualTo("""{ "title": "Mars" }"""));
    }

    [Test]
    public void AnAnswerWhichBreaksOffHasNoObject()
    {
        const string ANSWER = """Here is the deck: { "title": "Mars", "slides": [ { "layout": "L1",""";

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.Empty);
    }

    [Test]
    public void AnAnswerWithoutJsonHasNoObject()
    {
        Assert.That(ModelAnswerJson.ExtractObject("I cannot help with that.").ToString(), Is.Empty);
    }

    [Test]
    public void WithoutAnythingLikeJsonTheFirstBraceCounts()
    {
        Assert.That(ModelAnswerJson.ExtractObject("Here you go: {title: 'Mars'} Done.").ToString(), Is.EqualTo("{title: 'Mars'}"), "The deserializer of the caller tells what is wrong, which a model can repair.");
    }
}