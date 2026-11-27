using AIStudio.AIOperations;
using AIStudio.AIOperations.RetrievalContextValidation;
using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.RAG;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks how the validation of retrieval contexts reads what a model answered about a passage.
/// </summary>
/// <remarks>
/// A failed validation once stood for a decision with a confidence of 1.0, so the passage always stayed and
/// moved the threshold of all others. An answer without a usable judgment now says so with IsAnswered,
/// and nothing the model left out is read as a value it never wrote.
/// </remarks>
[TestFixture]
public sealed class RetrievalContextValidationAnswerTests
{
    private static readonly PreparedRetrievalContext PASSAGE = new(new RetrievalTextContext
    {
        DataSourceName = "Handbook",
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "handbook.pdf")),
        MatchedText = "The handbook explains the travel expenses.",
    }, "body", [], null);

    [TestCase("""{"decision": true, "reason": "It answers the question.", "confidence": 0.87}""", true, 0.87f)]
    [TestCase("""{"decision": false, "reason": "It is about something else.", "confidence": 0.9}""", false, 0.9f)]
    [TestCase("""{"decision": true, "reason": "Certain.", "confidence": 1}""", true, 1f)]
    [TestCase("""{"decision": false, "reason": "A pure guess.", "confidence": 0}""", false, 0f)]
    public void AJudgmentIsReadAsTheModelWroteIt(string answer, bool decision, float confidence)
    {
        var result = RetrievalContextValidationOperation.ReadAnswer(answer, PASSAGE);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAnswered, Is.True);
            Assert.That(result.Decision, Is.EqualTo(decision));
            Assert.That(result.Confidence, Is.EqualTo(confidence));
            Assert.That(result.RetrievalContext, Is.SameAs(PASSAGE), "The classic RAG process finds the passages it keeps by reference.");
        });
    }

    [Test]
    public void TheJudgmentIsFoundAmidProseAndACodeFence()
    {
        const string ANSWER = """
                              Here is my judgment:

                              ```json
                              {"decision": true, "reason": "It names the limit for hotels.", "confidence": 0.75}
                              ```
                              """;

        var result = RetrievalContextValidationOperation.ReadAnswer(ANSWER, PASSAGE);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAnswered, Is.True);
            Assert.That(result.Reason, Is.EqualTo("It names the limit for hotels."));
        });
    }

    [Test]
    public void AJudgmentWithoutAReasonStillCounts()
    {
        var result = RetrievalContextValidationOperation.ReadAnswer("""{"decision": true, "confidence": 0.8}""", PASSAGE);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAnswered, Is.True, "The reason only explains the judgment; the judgment stands without it.");
            Assert.That(result.Reason, Is.Empty);
        });
    }

    [TestCase("""{"reason": "It answers the question.", "confidence": 0.87}""", TestName = "Without a decision")]
    [TestCase("""{"decision": true, "reason": "It answers the question."}""", TestName = "Without a confidence")]
    [TestCase("""{"decision": true, "reason": "In percent.", "confidence": 87}""", TestName = "With a confidence above 1")]
    [TestCase("""{"decision": true, "reason": "Negative.", "confidence": -0.1}""", TestName = "With a confidence below 0")]
    [TestCase("""{"decision": "yes", "reason": "A word instead of a value.", "confidence": 0.8}""", TestName = "With a decision which is no boolean")]
    [TestCase("""{"decision": true, "reason": "Broken off""", TestName = "With JSON which breaks off")]
    [TestCase("I think this passage helps a lot.", TestName = "Without any JSON")]
    [TestCase("", TestName = "With an empty answer")]
    public void AnAnswerWithoutAUsableJudgmentIsNotAnswered(string answer)
    {
        var result = RetrievalContextValidationOperation.ReadAnswer(answer, PASSAGE);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsAnswered, Is.False, "Reading a value the model never wrote, e.g., false for a missing decision, would drop a passage nobody judged.");
            Assert.That(result.RetrievalContext, Is.SameAs(PASSAGE));
            Assert.That(result.Reason, Is.Not.Empty, "The log says why the answer was of no use.");
        });
    }

    [Test]
    public async Task AnEmptyPromptLeavesEveryPassageUnjudged()
    {
        var operation = new RetrievalContextValidationOperation(NullLogger<RetrievalContextValidationOperation>.Instance, new SettingsManager(NullLogger<SettingsManager>.Instance, null!));
        var request = new RetrievalContextValidationRequest("You are a helpful assistant.", new ContentText { Text = "   " }, [PASSAGE, PASSAGE], ConfidenceLevel.NONE);

        var results = await operation.RunAsync(request, new AIOperationContext(AIStudio.Settings.Provider.NONE));

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results.Select(result => result.IsAnswered), Is.All.False, "Nobody asked the model, so nothing was judged.");
        });
    }
}