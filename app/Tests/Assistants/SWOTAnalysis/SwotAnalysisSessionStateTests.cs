using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotAnalysisSessionStateTests
{
    [Test]
    public void TheStructuredResultSurvivesAnAssistantSessionSnapshot()
    {
        var key = new AssistantSessionStateKey<SwotAnalysisResult?>("analysisResult");
        var writer = new AssistantSessionStateWriter();
        writer.Set(key, new SwotAnalysisResult
        {
            MatrixHeading = "SWOT Matrix",
            Strengths = new() { Findings = [Finding()] },
        });
        SwotAnalysisResult? restored = null;

        var reader = new AssistantSessionStateReader(writer.ToDictionary(), "SWOT Analysis");
        reader.Restore(key, value => restored = value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.MatrixHeading, Is.EqualTo("SWOT Matrix"));
            Assert.That(restored.Strengths.Findings.Single().Summary, Is.EqualTo("Experienced team"));
        }
    }

    [Test]
    public void CategoryProgressSurvivesAnAssistantSessionSnapshot()
    {
        var key = new AssistantSessionStateKey<Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress>>("categoryAnalysisProgress");
        var writer = new AssistantSessionStateWriter();
        writer.SetDictionary(key, new Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress>
        {
            [SwotAnalysisCategory.STRENGTHS] = new(SwotCategoryAnalysisStatus.COMPLETED, new SwotCategoryAnalysisResult { Findings = [Finding()] }),
            [SwotAnalysisCategory.WEAKNESSES] = new(SwotCategoryAnalysisStatus.RUNNING),
        });
        Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress> restored = [];

        var reader = new AssistantSessionStateReader(writer.ToDictionary(), "SWOT Analysis");
        reader.RestoreDictionary(key, restored);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored[SwotAnalysisCategory.STRENGTHS].Status, Is.EqualTo(SwotCategoryAnalysisStatus.COMPLETED));
            Assert.That(restored[SwotAnalysisCategory.STRENGTHS].Result!.Findings.Single().Summary, Is.EqualTo("Experienced team"));
            Assert.That(restored[SwotAnalysisCategory.WEAKNESSES].Status, Is.EqualTo(SwotCategoryAnalysisStatus.RUNNING));
        }
    }

    private static SwotFinding Finding() => new()
    {
        Summary = "Experienced team",
        Explanation = "The documents describe long employee tenure.",
    };
}