using System.Text.Json;
using System.Text.RegularExpressions;

using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotAnalysisResponseTests
{
    [TestCase(ResponseEnvelope.BARE_JSON)]
    [TestCase(ResponseEnvelope.JSON_CODE_FENCE)]
    [TestCase(ResponseEnvelope.SURROUNDING_TEXT)]
    public void TheStructuredResponseIsFoundInCommonModelEnvelopes(ResponseEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(CreateResult());
        var response = envelope switch
        {
            ResponseEnvelope.BARE_JSON => json,
            ResponseEnvelope.JSON_CODE_FENCE => $"```json\n{json}\n```",
            ResponseEnvelope.SURROUNDING_TEXT => $"Here is the result:\n{json}\nDone.",
            _ => throw new ArgumentOutOfRangeException(nameof(envelope), envelope, null),
        };

        var parsed = SwotAnalysisResponseParser.TryParse(response, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result.Strengths.Findings.Single().Summary, Is.EqualTo("Experienced team"));
            Assert.That(result.PrioritizedActions.Items.Single().AddressedFactors, Is.EqualTo(new[] { "Experienced team", "Growing market" }));
        });
    }

    [Test]
    public void UnsupportedCategoriesAndActionsRemainAValidAnalysis()
    {
        var result = CreateResult();
        foreach (var category in result.Categories)
        {
            category.Findings.Clear();
            category.EmptyMessage = $"No evidence for {category.Label}.";
        }

        result.PrioritizedActions.Items.Clear();
        result.PrioritizedActions.EmptyMessage = "No supported actions.";

        var parsed = SwotAnalysisResponseParser.TryParse(JsonSerializer.Serialize(result), out var parsedResult);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(parsedResult.Categories.All(category => category.Findings.Count == 0), Is.True);
            Assert.That(parsedResult.PrioritizedActions.Items, Is.Empty);
        });
    }

    [Test]
    public void AFindingWithoutAnExplanationIsRejected()
    {
        var result = CreateResult();
        result.Strengths.Findings[0].Explanation = string.Empty;

        var parsed = SwotAnalysisResponseParser.TryParse(JsonSerializer.Serialize(result), out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void EveryFindingAppearsInTheDetailAndInTheFinalMatrix()
    {
        var result = CreateResult();
        for (var index = 2; index <= 6; index++)
            result.Strengths.Findings.Add(new() { Summary = $"Strength {index}", Explanation = $"Evidence {index}" });

        var markdown = SwotAnalysisMarkdownFormatter.Format(result);

        Assert.Multiple(() =>
        {
            Assert.That(markdown, Does.Contain("| | Positive | Negative |"));
            Assert.That(Count(markdown, "Experienced team"), Is.GreaterThanOrEqualTo(2));
            Assert.That(Count(markdown, "Strength 6"), Is.EqualTo(2), "The matrix must not cap the number of findings.");
            Assert.That(Count(markdown, "Evidence 6"), Is.EqualTo(1), "Explanations belong to the detailed section, not the matrix.");
            Assert.That(markdown, Does.StartWith("## SWOT Matrix"));
            Assert.That(markdown.IndexOf("## SWOT Matrix", StringComparison.Ordinal), Is.LessThan(markdown.IndexOf("## Strengths", StringComparison.Ordinal)));
            Assert.That(markdown.IndexOf("## SWOT Matrix", StringComparison.Ordinal), Is.LessThan(markdown.IndexOf("## Prioritized Actions", StringComparison.Ordinal)));
            Assert.That(markdown, Does.Contain("| **External** | **Opportunities:** Growing market | **Threats:** New competitor |"));
        });
    }

    [Test]
    public void MatrixCellsEscapePipesFromModelText()
    {
        var result = CreateResult();
        result.Strengths.Findings[0].Summary = "Fast | reliable";

        var markdown = SwotAnalysisMarkdownFormatter.Format(result);

        Assert.That(markdown, Does.Contain("Fast \\| reliable"));
    }

    [Test]
    public void TheRequestAssignsEveryInputToItsOwnRole()
    {
        var request = SwotAnalysisRequestBuilder.Build(
            "A regional manufacturer",
            "Support the investment decision",
            "Pay particular attention to staffing",
            hasContextualKnowledge: true);

        Assert.Multiple(() =>
        {
            Assert.That(request, Does.Match("(?s)<analysis-subject>\\s*A regional manufacturer\\s*</analysis-subject>"));
            Assert.That(request, Does.Match("(?s)<analysis-goal>\\s*Support the investment decision\\s*</analysis-goal>"));
            Assert.That(request, Does.Match("(?s)<analysis-focus>\\s*Pay particular attention to staffing\\s*</analysis-focus>"));
            Assert.That(request, Does.Match("(?s)<contextual-knowledge>\\s*The files attached to this message are contextual knowledge for the analysis\\.\\s*</contextual-knowledge>"));
        });
    }

    [Test]
    public void TheRequestStatesWhenOptionalInputsAreMissing()
    {
        var request = SwotAnalysisRequestBuilder.Build(
            "A regional manufacturer",
            "Support the investment decision",
            string.Empty,
            hasContextualKnowledge: false);

        Assert.Multiple(() =>
        {
            Assert.That(request, Does.Contain("No additional analysis focus was provided."));
            Assert.That(request, Does.Contain("No additional contextual knowledge files were provided."));
        });
    }

    [Test]
    public void TheStructuredResultSurvivesAnAssistantSessionSnapshot()
    {
        var key = new AssistantSessionStateKey<SwotAnalysisResult?>("analysisResult");
        var writer = new AssistantSessionStateWriter();
        writer.Set(key, CreateResult());
        SwotAnalysisResult? restored = null;

        var reader = new AssistantSessionStateReader(writer.ToDictionary(), "SWOT Analysis");
        reader.Restore(key, value => restored = value);

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.MatrixHeading, Is.EqualTo("SWOT Matrix"));
            Assert.That(restored.Categories.SelectMany(category => category.Findings).Count(), Is.EqualTo(4));
        });
    }

    private static int Count(string value, string search) => Regex.Matches(value, Regex.Escape(search)).Count;

    private static SwotAnalysisResult CreateResult() => new()
    {
        MatrixHeading = "SWOT Matrix",
        InternalLabel = "Internal",
        ExternalLabel = "External",
        PositiveLabel = "Positive",
        NegativeLabel = "Negative",
        Strengths = Category("Strengths", "Experienced team", "The documents describe long employee tenure."),
        Weaknesses = Category("Weaknesses", "Legacy system", "The documents name an unsupported application."),
        Opportunities = Category("Opportunities", "Growing market", "The supplied forecast shows increasing demand."),
        Threats = Category("Threats", "New competitor", "The material announces a funded market entrant."),
        PrioritizedActions = new()
        {
            Label = "Prioritized Actions",
            EmptyMessage = string.Empty,
            Items =
            [
                new()
                {
                    Action = "Prepare expansion",
                    Rationale = "Use the team's experience to address demand.",
                    AddressedFactors = ["Experienced team", "Growing market"],
                },
            ],
        },
    };

    private static SwotCategory Category(string label, string summary, string explanation) => new()
    {
        Label = label,
        EmptyMessage = string.Empty,
        Findings = [new() { Summary = summary, Explanation = explanation }],
    };

    public enum ResponseEnvelope
    {
        BARE_JSON,
        JSON_CODE_FENCE,
        SURROUNDING_TEXT,
    }
}
