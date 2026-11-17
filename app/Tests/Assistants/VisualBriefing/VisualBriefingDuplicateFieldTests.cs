using AIStudio.Assistants.VisualBriefing;

namespace AIStudio.Tests.Assistants.VisualBriefing;

/// <summary>
/// Checks that the Visual Briefing assistant refuses a model answer which names a field twice.
/// </summary>
/// <remarks>
/// The checks of the assistant would see one of the two values, while the browser which runs the
/// finished briefing may see the other. The repair turn names the field, so that the model can fix it.
/// </remarks>
[TestFixture]
public sealed class VisualBriefingDuplicateFieldTests
{
    [TestCase("""{"contractVersion":1,"contractVersion":2}""", "contractVersion", "$.contractVersion", TestName = "A field of the answer")]
    [TestCase("""{"contractVersion":1,"facts":[{"id":"fact-1","id":"fact-2"}]}""", "id", "$.facts[0].id", TestName = "A field of an object in a list")]
    [TestCase("""{"facts":[{"value":{"unit":"EUR","unit":"USD"}}]}""", "unit", "$.facts[0].value.unit", TestName = "A field deep inside a value")]
    [TestCase("```json\n{\"contractVersion\":1,\"contractVersion\":2}\n```", "contractVersion", "$.contractVersion", TestName = "A field of an answer in a code block")]
    public void AnAnswerWhichNamesAFieldTwiceIsRefused(string answer, string repeatedField, string path)
    {
        var result = VisualBriefingStructuredResponseProcessor.Process<VisualBriefingEvidenceResponse>(answer, _ => null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Response, Is.Null);
            Assert.That(result.Issue?.Code, Is.EqualTo(VisualBriefingFailureCode.RESPONSE_JSON_INVALID));
            Assert.That(result.Issue?.Rule, Is.EqualTo(VisualBriefingValidationRule.JSON_INVALID));
            Assert.That(result.Issue?.Diagnostic?.IssueKind, Is.EqualTo(VisualBriefingStructuredResponseIssueKind.DUPLICATE_FIELD));
            Assert.That(result.Issue?.Diagnostic?.FieldName, Is.EqualTo(repeatedField));
            Assert.That(result.Issue?.Diagnostic?.JsonPath, Is.EqualTo(path));
            Assert.That(result.Issue?.Issue, Does.Contain($"'{repeatedField}'"), "The repair turn names the field.");
        }
    }

    [Test]
    public void TheSameFieldInTwoObjectsIsNoDuplicate()
    {
        var result = VisualBriefingStructuredResponseProcessor.Process<VisualBriefingEvidenceResponse>(
            """{"contractVersion":1,"facts":[{"id":"fact-1"},{"id":"fact-2"}]}""",
            _ => null);

        Assert.That(result.Issue?.Diagnostic?.IssueKind, Is.Not.EqualTo(VisualBriefingStructuredResponseIssueKind.DUPLICATE_FIELD));
    }
}