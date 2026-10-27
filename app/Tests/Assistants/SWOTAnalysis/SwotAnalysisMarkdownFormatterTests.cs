using System.Text.RegularExpressions;

using AIStudio.Assistants.SWOTAnalysis;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotAnalysisMarkdownFormatterTests
{
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
}