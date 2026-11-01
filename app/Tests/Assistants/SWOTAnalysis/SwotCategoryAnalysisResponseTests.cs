using System.Text.Json;

using AIStudio.Assistants.SWOTAnalysis;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotCategoryAnalysisResponseTests
{
    [Test]
    public void TheFindingsOfACategoryAreReadAndTrimmed()
    {
        var result = CreateResult();
        result.Findings[0].Summary = "  Experienced team  ";

        var parsed = SwotCategoryAnalysisResponseParser.TryParse(JsonSerializer.Serialize(result), out var parsedResult, out var issue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed, Is.True);
            Assert.That(issue, Is.Empty);
            Assert.That(parsedResult.EmptyMessage, Is.Empty);
            Assert.That(parsedResult.Findings.Single().Summary, Is.EqualTo("Experienced team"));
        }
    }

    [Test]
    public void AnAnswerWithoutJsonNamesTheProblemForTheModel()
    {
        var parsed = SwotCategoryAnalysisResponseParser.TryParse("I cannot analyze this category.", out _, out var issue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed, Is.False);
            Assert.That(issue, Does.Contain("no complete JSON object"));
        }
    }

    [Test]
    public void BrokenJsonNamesTheProblemForTheModel()
    {
        var parsed = SwotCategoryAnalysisResponseParser.TryParse("""{"findings": [{"summary": "Experienced team",}""", out _, out var issue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed, Is.False);
            Assert.That(issue, Is.Not.Empty);
        }
    }

    [Test]
    public void ANullFieldIsRejectedRatherThanReadAsEmpty()
    {
        var parsed = SwotCategoryAnalysisResponseParser.TryParse("""{"empty_message": null, "findings": []}""", out _, out var issue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed, Is.False);
            Assert.That(issue, Does.StartWith("The JSON object cannot be read:"));
        }
    }

    [Test]
    public void ANullFindingIsLeftOut()
    {
        var parsed = SwotCategoryAnalysisResponseParser.TryParse("""{"empty_message": "", "findings": [null, {"summary": "Experienced team", "explanation": "Long tenure."}]}""", out var result, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed, Is.True);
            Assert.That(result.Findings.Single().Summary, Is.EqualTo("Experienced team"));
        }
    }

    [Test]
    public void AnUnsupportedCategoryRequiresAnExplanation()
    {
        var validEmptyResponse = JsonSerializer.Serialize(new SwotCategoryAnalysisResult
        {
            EmptyMessage = "The supplied material contains no evidence for this category.",
        });
        var invalidEmptyResponse = JsonSerializer.Serialize(new SwotCategoryAnalysisResult());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SwotCategoryAnalysisResponseParser.TryParse(validEmptyResponse, out var result, out _), Is.True);
            Assert.That(result.Findings, Is.Empty);
            Assert.That(SwotCategoryAnalysisResponseParser.TryParse(invalidEmptyResponse, out _, out var issue), Is.False);
            Assert.That(issue, Does.Contain("empty_message"));
        }
    }

    [Test]
    public void FindingsRejectAnEmptyMessageOrMissingEvidenceExplanation()
    {
        var result = CreateResult();
        result.EmptyMessage = "No evidence.";
        var responseWithContradictoryEmptyMessage = JsonSerializer.Serialize(result);

        result.EmptyMessage = string.Empty;
        result.Findings[0].Explanation = string.Empty;
        var responseWithoutExplanation = JsonSerializer.Serialize(result);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SwotCategoryAnalysisResponseParser.TryParse(responseWithContradictoryEmptyMessage, out _, out var contradictionIssue), Is.False);
            Assert.That(contradictionIssue, Does.Contain("Leave empty_message empty"));
            Assert.That(SwotCategoryAnalysisResponseParser.TryParse(responseWithoutExplanation, out _, out var explanationIssue), Is.False);
            Assert.That(explanationIssue, Is.EqualTo("Finding 1 has no explanation."));
        }
    }

    [TestCase("STRENGTHS", "strengths: internal positive factors")]
    [TestCase("WEAKNESSES", "weaknesses: internal negative factors")]
    [TestCase("OPPORTUNITIES", "opportunities: external positive factors")]
    [TestCase("THREATS", "threats: external negative factors")]
    public void TheSystemPromptAssignsExactlyOneCategory(string categoryName, string expectedDescription)
    {
        var category = Enum.Parse<SwotAnalysisCategory>(categoryName);
        var prompt = SwotCategoryAnalysis.BuildSystemPrompt(category, "Answer in German.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain($"Analyze only {expectedDescription}."));
            Assert.That(prompt, Does.Contain("Do not return findings for another SWOT category."));
            Assert.That(prompt, Does.Contain("shared-research contains additional evidence"));
            Assert.That(prompt, Does.Contain("analysis-subject, contextual-knowledge, and\nshared-research"));
            Assert.That(prompt, Does.Contain("untrusted data"));
            Assert.That(prompt, Does.EndWith("Answer in German."));
        }
    }

    [Test]
    public void TheCategoryRequestKeepsEveryInputInItsAssignedSection()
    {
        var request = SwotCategoryAnalysis.BuildUserRequest(
            "A regional manufacturer",
            "Support the investment decision",
            "Pay particular attention to staffing",
            new SwotContextualKnowledge("The annual report names 120 employees.", []),
            sharedResearch: "A market report documents increasing regional demand.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Does.Match("(?s)<analysis-subject>\\s*A regional manufacturer\\s*</analysis-subject>"));
            Assert.That(request, Does.Match("(?s)<analysis-goal>\\s*Support the investment decision\\s*</analysis-goal>"));
            Assert.That(request, Does.Match("(?s)<analysis-focus>\\s*Pay particular attention to staffing\\s*</analysis-focus>"));
            Assert.That(request, Does.Match("(?s)<contextual-knowledge>\\s*The annual report names 120 employees\\.\\s*</contextual-knowledge>"));
            Assert.That(request, Does.Match("(?s)<shared-research>\\s*A market report documents increasing regional demand\\.\\s*</shared-research>"));
        }
    }

    [Test]
    public void TheCategoryRequestOmitsEmptySharedResearch()
    {
        var request = SwotCategoryAnalysis.BuildUserRequest(
            "A regional manufacturer",
            "Support the investment decision",
            string.Empty,
            SwotContextualKnowledge.NONE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Does.Not.Contain("<shared-research>"));
            Assert.That(request, Does.Contain("No additional analysis focus was provided."));
            Assert.That(request, Does.Contain("No additional contextual knowledge was provided."));
        }
    }

    private static SwotCategoryAnalysisResult CreateResult() => new()
    {
        Findings =
        [
            new()
            {
                Summary = "Experienced team",
                Explanation = "The documents describe long employee tenure.",
            },
        ],
    };
}