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
    public void TheSectionsFollowTheOrderOfTheResultView()
    {
        var result = CreateResult();
        result.OverallAssessment.Text = "The position is solid.\n\nThe competitor is the main risk.";
        result.ModerationNotes = new()
        {
            Label = "Reconciliation of the Quadrants",
            Items = [new() { Change = "Moved the new competitor to the threats.", Reason = "It is external and negative." }],
        };

        var markdown = SwotAnalysisMarkdownFormatter.Format(result).ReplaceLineEndings("\n");
        var positions = new[] { "## SWOT Matrix", "## Overall Assessment", "## Prioritized Actions", "## Strengths", "## Threats", "## Reconciliation of the Quadrants" }
            .Select(heading => markdown.IndexOf(heading, StringComparison.Ordinal))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(positions, Is.All.GreaterThanOrEqualTo(0));
            Assert.That(positions, Is.Ordered);
            Assert.That(markdown, Does.Contain("The position is solid\\.\n\nThe competitor is the main risk\\."));
            Assert.That(markdown, Does.EndWith("- **Moved the new competitor to the threats\\.** — It is external and negative\\."));
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
        OverallAssessment = new()
        {
            Label = "Overall Assessment",
            Text = "The position is solid.",
        },
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
        ModerationNotes = new()
        {
            Label = "Reconciliation of the Quadrants",
            EmptyMessage = "The four categories needed no changes.",
        },
    };

    private static SwotCategory Category(string label, string summary, string explanation) => new()
    {
        Label = label,
        EmptyMessage = string.Empty,
        Findings = [new() { Summary = summary, Explanation = explanation }],
    };
}