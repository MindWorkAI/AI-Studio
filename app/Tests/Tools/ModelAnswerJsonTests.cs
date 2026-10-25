using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the JSON part is read out of a model answer, for the kinds of answers models write and
/// the callers rely on: the Assistant Builder, the launcher texts, the Slide Assistant, the assistant
/// audit, the retrieval context validation, the data source selection, and the Prompt Optimizer.
/// </summary>
[TestFixture]
public sealed class ModelAnswerJsonTests
{
    /// <summary>
    /// An optimized prompt as the Prompt Optimizer asks for it. The prompt itself holds a code fence,
    /// a placeholder, and quotes, as prompts often do.
    /// </summary>
    private const string OPTIMIZATION = """
        {
          "optimized_prompt": "You are a reviewer.\n```python\nprint({name})\n```\nAnswer with \"yes\" or \"no\".",
          "recommendations": { "clarity_and_directness": "Name the goal first.", "language_choice": "Keep it in English." }
        }
        """;

    /// <summary>
    /// A data source selection as the agent is asked to write it.
    /// </summary>
    private const string SELECTION = """
        [
          {"id": "4f2c", "reason": "It holds the meeting notes.", "confidence": 0.87},
          {"id": "9a1b", "reason": "It holds the [draft] of the report.", "confidence": 0.54}
        ]
        """;

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

    [Test]
    public void AnAuditIsReadWithAllItsFindings()
    {
        const string JSON = """
            {
              "level": "CAUTION",
              "summary": "The plugin writes below C:\\",
              "confidence": 0.8,
              "findings": [
                { "severity": "medium", "category": "file access", "location": "plugin.lua", "description": "Joins paths with \"\\\\\"" },
                { "severity": "low", "category": "prompt", "location": "system prompt", "description": "Asks for {user_input} twice" }
              ]
            }
            """;

        Assert.That(ModelAnswerJson.ExtractObject($"Here is my audit:\n```json\n{JSON}\n```").ToString(), Is.EqualTo(JSON), "A summary ending with an escaped backslash ends at its quote, so the findings still belong to the audit.");
    }

    [Test]
    public void AValidationDecisionIsReadAsItIs()
    {
        const string JSON = """{"decision": true, "reason": "The context names the launch date.", "confidence": 0.87}""";

        Assert.That(ModelAnswerJson.ExtractObject(JSON).ToString(), Is.EqualTo(JSON));
    }

    [Test]
    public void AValidationDecisionIsReadWhateverTheOrderOfItsNames()
    {
        const string JSON = """{"reason": "The context names the launch date.", "confidence": 0.87, "decision": true}""";

        Assert.That(ModelAnswerJson.ExtractObject(JSON).ToString(), Is.EqualTo(JSON));
    }

    [Test]
    public void AValidationReasonMayHoldBracesAndQuotes()
    {
        const string JSON = """{"decision": false, "reason": "The context only shows the template {date}} and a 5\" screen.", "confidence": 0.6}""";

        Assert.That(ModelAnswerJson.ExtractObject(JSON).ToString(), Is.EqualTo(JSON));
    }

    [Test]
    public void AValidationDecisionIsReadOutOfProse()
    {
        const string ANSWER = """
            After reading the retrieval context, my decision is:
            {"decision": true, "reason": "It answers the question.", "confidence": 0.9}
            """;

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.EqualTo("""{"decision": true, "reason": "It answers the question.", "confidence": 0.9}"""));
    }

    [Test]
    public void AListWithItsBracketsOnLinesOfTheirOwnIsRead()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModelAnswerJson.ExtractObjectList(SELECTION).ToString(), Is.EqualTo(SELECTION));
            Assert.That(ModelAnswerJson.ExtractObjectList($"```json\n{SELECTION}\n```").ToString(), Is.EqualTo(SELECTION));
        });
    }

    [Test]
    public void AListOnASingleLineIsRead()
    {
        const string JSON = """[{"id": "4f2c", "reason": "It holds the notes.", "confidence": 0.87}]""";

        Assert.That(ModelAnswerJson.ExtractObjectList(JSON).ToString(), Is.EqualTo(JSON));
    }

    [Test]
    public void AnEmptyListIsAList()
    {
        Assert.That(ModelAnswerJson.ExtractObjectList("No data source is needed: []").ToString(), Is.EqualTo("[]"));
    }

    [Test]
    public void AListInACodeFenceAfterProseIsRead()
    {
        const string ANSWER = $"These data sources fit your question:\n\n```json\n{SELECTION}\n```\n\nI left out the others.";

        Assert.That(ModelAnswerJson.ExtractObjectList(ANSWER).ToString(), Is.EqualTo(SELECTION));
    }

    [Test]
    public void AListAfterSeveralParagraphsOfProseIsRead()
    {
        const string ANSWER = $"I looked at every data source.\n\nTwo of them fit.\n\n{SELECTION}";

        Assert.That(ModelAnswerJson.ExtractObjectList(ANSWER).ToString(), Is.EqualTo(SELECTION));
    }

    [Test]
    public void AListMayStartAtTheEndOfALineOfProse()
    {
        const string ANSWER = $"Here you go: {SELECTION}";

        Assert.That(ModelAnswerJson.ExtractObjectList(ANSWER).ToString(), Is.EqualTo(SELECTION));
    }

    [Test]
    public void BracketsInProseBeforeTheListAreSkipped()
    {
        const string ANSWER = $"As described in [1] and in [the manual](https://example.org), these fit:\n{SELECTION}";

        Assert.That(ModelAnswerJson.ExtractObjectList(ANSWER).ToString(), Is.EqualTo(SELECTION));
    }

    [Test]
    public void BracketsInsideStringsDoNotEndTheList()
    {
        const string JSON = """[{"id": "4f2c", "reason": "See chapter ] and [ appendix", "confidence": 0.87}]""";

        Assert.That(ModelAnswerJson.ExtractObjectList(JSON).ToString(), Is.EqualTo(JSON));
    }

    [Test]
    public void AListWhichBreaksOffIsNoList()
    {
        Assert.That(ModelAnswerJson.ExtractObjectList("""[{"id": "4f2c", "reason": "It holds the""").ToString(), Is.Empty);
    }

    [Test]
    public void AnAnswerWithoutAListHasNoList()
    {
        Assert.That(ModelAnswerJson.ExtractObjectList("No data source fits your question.").ToString(), Is.Empty);
    }

    [Test]
    public void AnOptimizationAloneIsRead()
    {
        Assert.That(ModelAnswerJson.ExtractObject($"\n  {OPTIMIZATION}  \n").ToString(), Is.EqualTo(OPTIMIZATION));
    }

    [Test]
    public void AnOptimizedPromptMayHoldFencesPlaceholdersAndQuotes()
    {
        const string ANSWER = $"```json\n{OPTIMIZATION}\n```";

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.EqualTo(OPTIMIZATION));
    }

    [Test]
    public void AnOptimizationInACodeFenceAfterProseWithBracesIsRead()
    {
        const string ANSWER = $"I kept your placeholders {{topic}} and {{ audience }} as they were:\n```json\n{OPTIMIZATION}\n```\nGood luck with {{topic}}!";

        Assert.That(ModelAnswerJson.ExtractObject(ANSWER).ToString(), Is.EqualTo(OPTIMIZATION));
    }
}