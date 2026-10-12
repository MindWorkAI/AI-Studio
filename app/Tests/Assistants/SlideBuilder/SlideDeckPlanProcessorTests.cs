using AIStudio.Assistants.SlideBuilder;
using AIStudio.Tools;

namespace AIStudio.Tests.Assistants.SlideBuilder;

/// <summary>
/// Verifies the strict model contract used before a PowerPoint file can be exported.
/// </summary>
[TestFixture]
public sealed class SlideDeckPlanProcessorTests
{
    [Test]
    public void AValidPlanUsesTheRequestedTotalIncludingTheTitleSlide()
    {
        var result = SlideDeckPlanProcessor.Process(
            """
            {
              "contractVersion": 1,
              "title": "Quarterly review",
              "slides": [
                { "title": "Results", "bulletPoints": ["Revenue increased"] },
                { "title": "Next steps", "bulletPoints": ["Launch the pilot"] }
              ]
            }
            """,
            requiredTotalSlides: 3,
            requiredBulletPoints: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.Plan?.Title, Is.EqualTo("Quarterly review"));
            Assert.That(result.Plan?.Slides, Has.Count.EqualTo(2));
            Assert.That(result.Issue, Is.Empty);
        });
    }

    [Test]
    public void TheRequestedTotalCountsTheTitleSlide()
    {
        var result = SlideDeckPlanProcessor.Process(
            """
            {
              "contractVersion": 1,
              "title": "Quarterly review",
              "slides": [
                { "title": "Results", "bulletPoints": ["Revenue increased"] },
                { "title": "Next steps", "bulletPoints": ["Launch the pilot"] }
              ]
            }
            """,
            requiredTotalSlides: 2,
            requiredBulletPoints: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Diagnostic?.IssueKind, Is.EqualTo(StructuredResponseIssueKind.SEMANTIC_CONTRACT_INVALID));
            Assert.That(result.Diagnostic?.JsonPath, Is.EqualTo("$.slides"));
        });
    }

    [Test]
    public void UnknownFieldsAreRejected()
    {
        var result = SlideDeckPlanProcessor.Process(
            """
            {
              "contractVersion": 1,
              "title": "Quarterly review",
              "slides": [],
              "speakerNotes": "Do not accept this field"
            }
            """,
            requiredTotalSlides: 1,
            requiredBulletPoints: 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Diagnostic?.IssueKind, Is.EqualTo(StructuredResponseIssueKind.UNKNOWN_FIELD));
            Assert.That(result.Diagnostic?.FieldName, Is.EqualTo("speakerNotes"));
        });
    }

    [Test]
    public void AutomaticExtentStaysWithinTheSupportedRange()
    {
        var result = SlideDeckPlanProcessor.Process(
            """
            {
              "contractVersion": 1,
              "title": "Title only",
              "slides": []
            }
            """,
            requiredTotalSlides: 0,
            requiredBulletPoints: 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Diagnostic?.JsonPath, Is.EqualTo("$.slides"));
        });
    }

    [Test]
    public void MarkdownPreviewContainsOnlyTheValidatedPlan()
    {
        var plan = new SlideDeckPlan
        {
            ContractVersion = 1,
            Title = "Quarterly review",
            Slides =
            [
                new()
                {
                    Title = "Results",
                    BulletPoints = ["Revenue increased", "Costs decreased"],
                },
            ],
        };

        var markdown = SlideDeckPlanProcessor.ToMarkdown(plan);

        Assert.That(
            markdown,
            Is.EqualTo(
                """
                # Quarterly review

                ## Results
                - Revenue increased
                - Costs decreased
                """));
    }
}
