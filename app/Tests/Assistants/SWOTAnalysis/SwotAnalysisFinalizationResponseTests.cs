using System.Text.Json;

using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tests.Assistants.SWOTAnalysis;

[TestFixture]
public sealed class SwotAnalysisFinalizationResponseTests
{
    [Test]
    public void TheFinalizationAcceptsExistingFactorIdsWithoutCaseSensitivity()
    {
        var categories = CreateCategories();
        var finalization = CreateFinalization();
        finalization.PrioritizedActions.Items.Single().AddressedFactorIds = ["s1", "o1"];
        var response = JsonSerializer.Serialize(finalization);

        var parsed = SwotAnalysisFinalizationResponseParser.TryParse(response, categories, out var result);
        var assembled = SwotAnalysisFinalization.Assemble(categories, result);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result.PrioritizedActions.Items.Single().AddressedFactorIds, Is.EqualTo(new[] { "s1", "o1" }));
            Assert.That(assembled.PrioritizedActions.Items.Single().AddressedFactors, Is.EqualTo(new[] { "Experienced team", "Growing market" }));
        });
    }

    [Test]
    public void AnInventedFactorIdIsRejected()
    {
        var categories = CreateCategories();
        var finalization = CreateFinalization();
        finalization.PrioritizedActions.Items.Single().AddressedFactorIds = ["X1"];

        var parsed = SwotAnalysisFinalizationResponseParser.TryParse(
            JsonSerializer.Serialize(finalization),
            categories,
            out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void AnActionWithoutAFactorIdIsRejected()
    {
        var categories = CreateCategories();
        var finalization = CreateFinalization();
        finalization.PrioritizedActions.Items.Single().AddressedFactorIds.Clear();

        var parsed = SwotAnalysisFinalizationResponseParser.TryParse(
            JsonSerializer.Serialize(finalization),
            categories,
            out _);

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void AssemblyPreservesEveryValidatedCategoryFinding()
    {
        var categories = CreateCategories();
        var finalization = CreateFinalization();
        finalization.StrengthsLabel = "Stärken";

        var result = SwotAnalysisFinalization.Assemble(categories, finalization);

        Assert.Multiple(() =>
        {
            Assert.That(result.Strengths.Label, Is.EqualTo("Stärken"));
            Assert.That(result.Strengths.Findings.Single().Summary, Is.EqualTo("Experienced team"));
            Assert.That(result.Weaknesses.Findings.Single().Explanation, Is.EqualTo("The documents name an unsupported application."));
            Assert.That(result.Opportunities.Findings.Single().Summary, Is.EqualTo("Growing market"));
            Assert.That(result.Threats.Findings.Single().Summary, Is.EqualTo("New competitor"));
            Assert.That(result.PrioritizedActions.Items.Single().AddressedFactors, Is.EqualTo(new[] { "Experienced team", "Growing market" }));
        });
    }

    [Test]
    public void LongFindingSummariesAreRestoredFromShortFactorIds()
    {
        var categories = CreateCategories();
        var longSummary = "Gestiegene finanzwirtschaftliche Risiken und ein negatives Rating-Ausblicksignal — mit weiteren Details und Satzzeichen.";
        categories[SwotAnalysisCategory.STRENGTHS].Findings.Single().Summary = longSummary;
        var response = JsonSerializer.Serialize(CreateFinalization());

        var parsed = SwotAnalysisFinalizationResponseParser.TryParse(response, categories, out var finalization);
        var result = SwotAnalysisFinalization.Assemble(categories, finalization);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(result.PrioritizedActions.Items.Single().AddressedFactors, Does.Contain(longSummary));
        });
    }

    [Test]
    public void CategoryProgressSurvivesAnAssistantSessionSnapshot()
    {
        var key = new AssistantSessionStateKey<Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress>>("categoryAnalysisProgress");
        var writer = new AssistantSessionStateWriter();
        writer.SetDictionary(key, new Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress>
        {
            [SwotAnalysisCategory.STRENGTHS] = new(SwotCategoryAnalysisStatus.COMPLETED, CreateCategories()[SwotAnalysisCategory.STRENGTHS]),
            [SwotAnalysisCategory.WEAKNESSES] = new(SwotCategoryAnalysisStatus.RUNNING),
        });
        Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisProgress> restored = [];

        var reader = new AssistantSessionStateReader(writer.ToDictionary(), "SWOT Analysis");
        reader.RestoreDictionary(key, restored);

        Assert.Multiple(() =>
        {
            Assert.That(restored[SwotAnalysisCategory.STRENGTHS].Status, Is.EqualTo(SwotCategoryAnalysisStatus.COMPLETED));
            Assert.That(restored[SwotAnalysisCategory.STRENGTHS].Result!.Findings.Single().Summary, Is.EqualTo("Experienced team"));
            Assert.That(restored[SwotAnalysisCategory.WEAKNESSES].Status, Is.EqualTo(SwotCategoryAnalysisStatus.RUNNING));
        });
    }

    [Test]
    public void TheFinalizationRequestContainsAllFourNamedCategories()
    {
        var request = SwotAnalysisFinalization.BuildUserRequest(CreateCategories());

        Assert.Multiple(() =>
        {
            Assert.That(request, Does.Contain("\"strengths\""));
            Assert.That(request, Does.Contain("\"weaknesses\""));
            Assert.That(request, Does.Contain("\"opportunities\""));
            Assert.That(request, Does.Contain("\"threats\""));
            Assert.That(request, Does.Contain("\"factor_id\": \"S1\""));
            Assert.That(request, Does.Contain("\"factor_id\": \"W1\""));
            Assert.That(request, Does.Contain("\"factor_id\": \"O1\""));
            Assert.That(request, Does.Contain("\"factor_id\": \"T1\""));
            Assert.That(request, Does.Contain("Experienced team"));
        });
    }

    [Test]
    public void TheFinalizationPromptRequestsFactorIdsInsteadOfFindingSummaries()
    {
        var prompt = SwotAnalysisFinalization.BuildSystemPrompt("Answer in German.");

        Assert.Multiple(() =>
        {
            Assert.That(prompt, Does.Contain("addressed_factor_ids"));
            Assert.That(prompt, Does.Contain("exact factor_id values"));
            Assert.That(prompt, Does.Contain("strengths_label"));
            Assert.That(prompt, Does.EndWith("Answer in German."));
        });
    }

    private static Dictionary<SwotAnalysisCategory, SwotCategoryAnalysisResult> CreateCategories() => new()
    {
        [SwotAnalysisCategory.STRENGTHS] = Category("Experienced team", "The documents describe long employee tenure."),
        [SwotAnalysisCategory.WEAKNESSES] = Category("Legacy system", "The documents name an unsupported application."),
        [SwotAnalysisCategory.OPPORTUNITIES] = Category("Growing market", "The supplied forecast shows increasing demand."),
        [SwotAnalysisCategory.THREATS] = Category("New competitor", "The material announces a funded market entrant."),
    };

    private static SwotCategoryAnalysisResult Category(string summary, string explanation) => new()
    {
        Findings =
        [
            new()
            {
                Summary = summary,
                Explanation = explanation,
            },
        ],
    };

    private static SwotAnalysisFinalizationResult CreateFinalization() => new()
    {
        MatrixHeading = "SWOT Matrix",
        InternalLabel = "Internal",
        ExternalLabel = "External",
        PositiveLabel = "Positive",
        NegativeLabel = "Negative",
        StrengthsLabel = "Strengths",
        WeaknessesLabel = "Weaknesses",
        OpportunitiesLabel = "Opportunities",
        ThreatsLabel = "Threats",
        PrioritizedActions = new SwotAnalysisFinalizationActions
        {
            Label = "Prioritized Actions",
            Items =
            [
                new SwotAnalysisFinalizationAction
                {
                    Action = "Prepare expansion",
                    Rationale = "Use the team's experience to address demand.",
                    AddressedFactorIds = ["S1", "O1"],
                },
            ],
        },
    };
}