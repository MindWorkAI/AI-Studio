using System.Text.Json;

using AIStudio.Assistants.SWOTAnalysis;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotAnalysisModerationTests
{
    [Test]
    public void AValidAnswerIsAssembledIntoTheResult()
    {
        var parsed = SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(CreateModeration()), out var moderation, out var issue);
        var result = SwotAnalysisModeration.Assemble(moderation);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(issue, Is.Empty);
            Assert.That(result.Strengths.Label, Is.EqualTo("Stärken"));
            Assert.That(result.Strengths.Findings.Single().Summary, Is.EqualTo("Experienced team"));
            Assert.That(result.Threats.Findings.Single().Explanation, Is.EqualTo("The material announces a funded market entrant."));
            Assert.That(result.OverallAssessment.Paragraphs, Is.EqualTo(new[] { "The position is solid.", "The competitor is the main risk." }));
            Assert.That(result.ModerationNotes.Items.Single().Change, Is.EqualTo("Merged two strengths about the team."));
            Assert.That(result.PrioritizedActions.Items.Single().AddressedFactors, Is.EqualTo(new[] { "Experienced team", "Growing market" }));
        });
    }

    [Test]
    public void FactorIdsAreMatchedWithoutCaseSensitivity()
    {
        var moderation = CreateModeration();
        moderation.PrioritizedActions.Items.Single().AddressedFactorIds = ["s1", "o1"];

        var parsed = SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(moderation), out var parsedModeration, out _);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(SwotAnalysisModeration.Assemble(parsedModeration).PrioritizedActions.Items.Single().AddressedFactors, Is.EqualTo(new[] { "Experienced team", "Growing market" }));
        });
    }

    [Test]
    public void AFactorIdOfAnotherCategoryIsRejected()
    {
        var moderation = CreateModeration();
        moderation.Strengths.Findings.Single().FactorId = "W2";

        Assert.Multiple(() =>
        {
            Assert.That(SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(moderation), out _, out var issue), Is.False);
            Assert.That(issue, Is.EqualTo("Finding 1 of strengths has the factor_id \"W2\", which does not start with \"S\"."));
        });
    }

    [Test]
    public void AFactorIdUsedTwiceIsRejected()
    {
        var moderation = CreateModeration();
        moderation.Strengths.Findings.Add(Finding("s1", "Loyal customers", "The survey shows long customer relationships."));

        Assert.Multiple(() =>
        {
            Assert.That(SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(moderation), out _, out var issue), Is.False);
            Assert.That(issue, Does.Contain("\"s1\" is used more than once"));
        });
    }

    [Test]
    public void AnActionForAnUnknownFactorIsRejected()
    {
        var moderation = CreateModeration();
        moderation.PrioritizedActions.Items.Single().AddressedFactorIds = ["S1", "X9"];

        Assert.Multiple(() =>
        {
            Assert.That(SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(moderation), out _, out var issue), Is.False);
            Assert.That(issue, Does.Contain("\"X9\""));
        });
    }

    [Test]
    public void AnEmptyOverallAssessmentIsRejected()
    {
        var moderation = CreateModeration();
        moderation.OverallAssessment.Text = "  ";

        Assert.Multiple(() =>
        {
            Assert.That(SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(moderation), out _, out var issue), Is.False);
            Assert.That(issue, Is.EqualTo("The field overall_assessment.text is empty."));
        });
    }

    [Test]
    public void NotesWithoutChangesExplainWhy()
    {
        var withoutReason = CreateModeration();
        withoutReason.ModerationNotes.Items.Clear();
        var withReason = CreateModeration();
        withReason.ModerationNotes.Items.Clear();
        withReason.ModerationNotes.EmptyMessage = "The four categories needed no changes.";

        Assert.Multiple(() =>
        {
            Assert.That(SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(withoutReason), out _, out var issue), Is.False);
            Assert.That(issue, Does.StartWith("moderation_notes has no items"));
            Assert.That(SwotAnalysisModerationResponseParser.TryParse(JsonSerializer.Serialize(withReason), out _, out _), Is.True);
        });
    }

    [Test]
    public void AnAnswerWithoutJsonNamesTheProblemForTheModel()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SwotAnalysisModerationResponseParser.TryParse("Here is my assessment.", out _, out var issue), Is.False);
            Assert.That(issue, Does.Contain("no complete JSON object"));
        });
    }

    [Test]
    public void TheRequestHoldsTheFourCategoriesTheGoalAndTheResearch()
    {
        var request = SwotAnalysisModeration.BuildUserRequest(CreateCategories(), "Support the investment decision", string.Empty, "A market report documents increasing demand.");

        Assert.Multiple(() =>
        {
            foreach (var category in new[] { "strengths", "weaknesses", "opportunities", "threats" })
                Assert.That(request, Does.Contain($"\"{category}\""));

            Assert.That(request, Does.Contain("\"factor_id\": \"S1\""));
            Assert.That(request, Does.Contain("\"factor_id\": \"O1\""));
            Assert.That(request, Does.Contain("The material names no threats."));
            Assert.That(request, Does.Match("(?s)<analysis-goal>\\s*Support the investment decision\\s*</analysis-goal>"));
            Assert.That(request, Does.Match("(?s)<analysis-focus>\\s*No additional analysis focus was provided\\.\\s*</analysis-focus>"));
            Assert.That(request, Does.Match("(?s)<shared-research>\\s*A market report documents increasing demand\\.\\s*</shared-research>"));
            Assert.That(request, Does.Not.Contain("<analysis-subject>"), "The moderator works on the results, not on the material.");
        });
    }

    [Test]
    public void TheRequestOmitsEmptyResearch() =>
        Assert.That(SwotAnalysisModeration.BuildUserRequest(CreateCategories(), "Support the investment decision", string.Empty), Does.Not.Contain("<shared-research>"));

    [Test]
    public void ThePromptAsksForTheWholeModeratorSchema()
    {
        var prompt = SwotAnalysisModeration.BuildSystemPrompt("Answer in German.");

        Assert.Multiple(() =>
        {
            foreach (var key in new[] { "\"moderation_notes\"", "\"overall_assessment\"", "\"prioritized_actions\"", "\"addressed_factor_ids\"", "\"factor_id\"" })
                Assert.That(prompt, Does.Contain(key));

            Assert.That(prompt, Does.EndWith("Answer in German."));
        });
    }

    private static Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult> CreateCategories() => new()
    {
        [SwotAnalysisCategory.STRENGTHS] = new() { Findings = [new() { Summary = "Experienced team", Explanation = "The documents describe long employee tenure." }] },
        [SwotAnalysisCategory.WEAKNESSES] = new() { Findings = [new() { Summary = "Legacy system", Explanation = "The documents name an unsupported application." }] },
        [SwotAnalysisCategory.OPPORTUNITIES] = new() { Findings = [new() { Summary = "Growing market", Explanation = "The supplied forecast shows increasing demand." }] },
        [SwotAnalysisCategory.THREATS] = new() { EmptyMessage = "The material names no threats." },
    };

    private static SwotModerationResult CreateModeration() => new()
    {
        MatrixHeading = "SWOT-Matrix",
        InternalLabel = "Intern",
        ExternalLabel = "Extern",
        PositiveLabel = "Positiv",
        NegativeLabel = "Negativ",
        Strengths = Category("Stärken", Finding("S1", "Experienced team", "The documents describe long employee tenure.")),
        Weaknesses = Category("Schwächen", Finding("W1", "Legacy system", "The documents name an unsupported application.")),
        Opportunities = Category("Chancen", Finding("O1", "Growing market", "The supplied forecast shows increasing demand.")),
        Threats = Category("Risiken", Finding("T1", "New competitor", "The material announces a funded market entrant.")),
        ModerationNotes = new()
        {
            Label = "Abgleich der Quadranten",
            Items = [new() { Change = "Merged two strengths about the team.", Reason = "Both described the same tenure." }],
        },
        OverallAssessment = new()
        {
            Label = "Gesamtbewertung",
            Text = "The position is solid.\n\nThe competitor is the main risk.",
        },
        PrioritizedActions = new()
        {
            Label = "Maßnahmen",
            Items = [new() { Action = "Prepare expansion", Rationale = "Use the team's experience to address demand.", AddressedFactorIds = ["S1", "O1"] }],
        },
    };

    private static SwotModeratedCategory Category(string label, SwotModeratedFinding finding) => new()
    {
        Label = label,
        Findings = [finding],
    };

    private static SwotModeratedFinding Finding(string factorId, string summary, string explanation) => new()
    {
        FactorId = factorId,
        Summary = summary,
        Explanation = explanation,
    };
}