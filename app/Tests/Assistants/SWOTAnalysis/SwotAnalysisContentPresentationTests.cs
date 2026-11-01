using System.Text.Json;

using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Chat;
using AIStudio.Tools;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotAnalysisContentPresentationTests
{
    [Test]
    public void PresentationSurvivesCloningAndWorkspaceSerialization()
    {
        var content = new ContentText
        {
            Text = "Complete SWOT analysis in Markdown",
            Presentation = SwotAnalysisContentPresentation.Create(CreateResult()),
        };
        var clone = (ContentText)content.DeepClone();
        var thread = new ChatThread
        {
            Blocks =
            [
                new ContentBlock
                {
                    ContentType = ContentType.TEXT,
                    Role = ChatRole.AI,
                    Content = clone,
                },
            ],
        };

        var json = JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS);
        var restoredThread = JsonSerializer.Deserialize<ChatThread>(json, WorkspaceBehaviour.JSON_OPTIONS);
        var restoredContent = (ContentText)restoredThread!.Blocks.Single().Content!;
        var presentationWasRead = SwotAnalysisContentPresentation.TryRead(restoredContent.Presentation, out var restoredResult);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clone.Presentation, Is.Not.SameAs(content.Presentation));
            Assert.That(clone.Text, Is.EqualTo(content.Text));
            Assert.That(restoredContent.Presentation?.Kind, Is.EqualTo(ContentPresentationKind.SWOT_ANALYSIS));
            Assert.That(presentationWasRead, Is.True);
            Assert.That(restoredResult.MatrixHeading, Is.EqualTo("SWOT Matrix"));
            Assert.That(restoredResult.Strengths.Findings.Single().Summary, Is.EqualTo("Experienced team"));
            Assert.That(restoredResult.PrioritizedActions.Items.Single().Action, Is.EqualTo("Prepare expansion"));
        }
    }

    [Test]
    public void InvalidPresentationDataFallsBackToMarkdown()
    {
        var content = new ContentText
        {
            Text = "Fallback Markdown",
            Presentation = ContentPresentation.Create(ContentPresentationKind.SWOT_ANALYSIS, "not a SWOT result"),
        };

        var presentationWasRead = SwotAnalysisContentPresentation.TryRead(content.Presentation, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(presentationWasRead, Is.False);
            Assert.That(content.Text, Is.EqualTo("Fallback Markdown"));
        }
    }

    [Test]
    public void TextWithoutPresentationUsesMarkdown()
    {
        var content = new ContentText { Text = "Ordinary Markdown" };

        Assert.That(SwotAnalysisContentPresentation.TryRead(content.Presentation, out _), Is.False);
    }

    [Test]
    public void AnAnalysisWithoutFindingsAndActionsIsStillComplete()
    {
        var result = CreateResult();
        foreach (var category in result.Categories)
        {
            category.Findings.Clear();
            category.EmptyMessage = $"No evidence for {category.Label}.";
        }

        result.PrioritizedActions.Items.Clear();
        result.PrioritizedActions.EmptyMessage = "No supported actions.";
        var content = new ContentText { Presentation = SwotAnalysisContentPresentation.Create(result) };

        var presentationWasRead = SwotAnalysisContentPresentation.TryRead(content.Presentation, out var readResult);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(presentationWasRead, Is.True);
            Assert.That(readResult.Categories.All(category => category.Findings.Count == 0), Is.True);
            Assert.That(readResult.PrioritizedActions.Items, Is.Empty);
        }
    }

    [Test]
    public void AStoredFindingWithoutAnExplanationFallsBackToMarkdown()
    {
        var result = CreateResult();
        result.Strengths.Findings[0].Explanation = string.Empty;
        var content = new ContentText { Presentation = SwotAnalysisContentPresentation.Create(result) };

        Assert.That(SwotAnalysisContentPresentation.TryRead(content.Presentation, out _), Is.False);
    }

    [Test]
    public void StoredDataWithANullFallsBackToMarkdown()
    {
        var data = JsonSerializer.SerializeToNode(CreateResult())!;
        data["strengths"]!["findings"] = null;
        var content = new ContentText
        {
            Presentation = new ContentPresentation
            {
                Kind = ContentPresentationKind.SWOT_ANALYSIS,
                Data = JsonSerializer.SerializeToElement(data),
            },
        };

        Assert.That(SwotAnalysisContentPresentation.TryRead(content.Presentation, out _), Is.False);
    }

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
        Findings =
        [
            new()
            {
                Summary = summary,
                Explanation = explanation,
            },
        ],
    };
}