using System.Text.Json;
using System.Text.Json.Serialization;

using AIStudio.Assistants.VisualBriefing;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools.StructuredOutput;

/// <summary>
/// Verifies the provider-neutral structured-response contract and its visual briefing adapter.
/// </summary>
[TestFixture]
public sealed class StructuredResponseProcessorTests
{
    private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new(JsonSerializerDefaults.Web);

    [Test]
    public void AValidBareJsonObjectIsReturned()
    {
        var result = Process("""{"value": 7, "identifier": "e6763e02-6526-4b47-9e75-578d176b0a45"}""");

        Assert.Multiple(() =>
        {
            Assert.That(result.Response?.Value, Is.EqualTo(7));
            Assert.That(result.Issue, Is.Null);
        });
    }

    [Test]
    public void TheLastValidMarkdownCandidateIsReturned()
    {
        var result = Process(
            """
            Preliminary response:
            ```json
            {"value": 1, "identifier": "e6763e02-6526-4b47-9e75-578d176b0a45"}
            ```
            Corrected response:
            ```json
            {"value": 2, "identifier": "f9d07d4b-cdcb-41c4-a2b6-e475741959de"}
            ```
            """);

        Assert.Multiple(() =>
        {
            Assert.That(result.Response?.Value, Is.EqualTo(2));
            Assert.That(result.Issue, Is.Null);
        });
    }

    [Test]
    public void AnUnknownFieldIsAContractFailure()
    {
        var result = Process(
            """{"value": 7, "identifier": "e6763e02-6526-4b47-9e75-578d176b0a45", "extra": true}""");

        Assert.Multiple(() =>
        {
            Assert.That(result.Response, Is.Null);
            Assert.That(result.Issue?.Failure, Is.EqualTo(TestFailure.CONTRACT_INVALID));
            Assert.That(result.Issue?.Diagnostic?.IssueKind, Is.EqualTo(StructuredResponseIssueKind.UNKNOWN_FIELD));
            Assert.That(result.Issue?.Diagnostic?.FieldName, Is.EqualTo("extra"));
        });
    }

    [Test]
    public void SemanticValidationGetsAContentFreeDiagnostic()
    {
        var result = StructuredResponseProcessor.Process<TestResponse, TestFailure>(
            """{"value": 7, "identifier": "e6763e02-6526-4b47-9e75-578d176b0a45"}""",
            SERIALIZER_OPTIONS,
            Classify,
            _ => new("The response violates the test contract.", TestFailure.SEMANTIC_INVALID));

        Assert.Multiple(() =>
        {
            Assert.That(result.Response, Is.Null);
            Assert.That(result.Issue?.Failure, Is.EqualTo(TestFailure.SEMANTIC_INVALID));
            Assert.That(result.Issue?.Diagnostic?.IssueKind, Is.EqualTo(StructuredResponseIssueKind.SEMANTIC_CONTRACT_INVALID));
            Assert.That(result.Issue?.Diagnostic?.JsonPath, Is.EqualTo("$"));
        });
    }

    [Test]
    public void TheGrammarComesFromTheSameClrContract()
    {
        var grammar = StructuredResponseProcessor.BuildContractGrammar<TestResponse>();

        Assert.Multiple(() =>
        {
            Assert.That(grammar, Does.Contain("\"value\": number"));
            Assert.That(grammar, Does.Contain("\"identifier\": string<uuid>"));
            Assert.That(grammar, Does.Contain("Every object may contain only the properties shown above."));
        });
    }

    [Test]
    public void TheVisualBriefingAdapterPreservesUnknownFieldFailures()
    {
        var result = VisualBriefingStructuredResponseProcessor.Process<TestResponse>(
            """{"value": 7, "identifier": "e6763e02-6526-4b47-9e75-578d176b0a45", "extra": true}""",
            _ => null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Issue?.Code, Is.EqualTo(VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID));
            Assert.That(result.Issue?.Rule, Is.EqualTo(VisualBriefingValidationRule.UNKNOWN_FIELD));
            Assert.That(result.Issue?.Diagnostic?.IssueKind, Is.EqualTo(StructuredResponseIssueKind.UNKNOWN_FIELD));
        });
    }

    [Test]
    public void TheVisualBriefingAdapterPreservesDeserializerFailures()
    {
        var result = VisualBriefingStructuredResponseProcessor.Process<TestResponse>(
            """{"value": 7, "identifier": "not-a-uuid"}""",
            _ => null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Issue?.Code, Is.EqualTo(VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID));
            Assert.That(result.Issue?.Rule, Is.EqualTo(VisualBriefingValidationRule.VALUE_TYPE_INVALID));
            Assert.That(result.Issue?.Diagnostic?.IssueKind, Is.EqualTo(StructuredResponseIssueKind.TYPE_MISMATCH));
        });
    }

    [Test]
    public void TheVisualBriefingAdapterPreservesSemanticFailures()
    {
        var result = VisualBriefingStructuredResponseProcessor.Process<TestResponse>(
            """{"value": 7, "identifier": "e6763e02-6526-4b47-9e75-578d176b0a45"}""",
            _ => new(
                VisualBriefingFailureCode.PRESENTATION_INVALID,
                "The layout violates the response contract.",
                VisualBriefingValidationRule.LAYOUT_INVALID));

        Assert.Multiple(() =>
        {
            Assert.That(result.Issue?.Code, Is.EqualTo(VisualBriefingFailureCode.PRESENTATION_INVALID));
            Assert.That(result.Issue?.Rule, Is.EqualTo(VisualBriefingValidationRule.LAYOUT_INVALID));
            Assert.That(result.Issue?.Diagnostic?.IssueKind, Is.EqualTo(StructuredResponseIssueKind.SEMANTIC_CONTRACT_INVALID));
        });
    }

    private static StructuredResponseResult<TestResponse, TestFailure> Process(string answer) =>
        StructuredResponseProcessor.Process<TestResponse, TestFailure>(
            answer,
            SERIALIZER_OPTIONS,
            Classify,
            _ => null);

    private static TestFailure Classify(
        StructuredResponseFailureKind failureKind,
        StructuredResponseIssueKind _) =>
        failureKind is StructuredResponseFailureKind.JSON_INVALID
            ? TestFailure.JSON_INVALID
            : TestFailure.CONTRACT_INVALID;

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class TestResponse
    {
        [JsonRequired]
        public int Value { get; init; }

        [JsonRequired]
        public Guid Identifier { get; init; }
    }

    private enum TestFailure
    {
        JSON_INVALID,
        CONTRACT_INVALID,
        SEMANTIC_INVALID,
    }
}