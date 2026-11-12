using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Provider;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks what the research agents of the BeyondFacts Lab receive: the public details about the event, and nothing
/// else of a preparation.
/// </summary>
[TestFixture]
public sealed class BflResearchPromptTests
{
    [Test]
    public async Task OnlyThePublicDetailsReachTheResearch()
    {
        var forms = FormsWithAMarkerInEveryField();
        var details = BflPromptComposer.PublicDetailsOf(forms);

        foreach (var topic in BflResearchTopic.All)
        {
            var prompt = BflPromptComposer.ComposeResearch(await BflPromptLibrary.LoadResearchPartsAsync(topic), details, "Deutsch");
            using (Assert.EnterMultipleScope())
            {
                foreach (var field in TextFields())
                {
                    if (field.IsSharedWithResearch)
                        Assert.That(prompt, Does.Contain(Marker(field)), $"{topic}: {field.Id}");
                    else
                        Assert.That(prompt, Does.Not.Contain(Marker(field)), $"{topic}: {field.Id}");
                }
            }
        }
    }

    [Test]
    public void ThePublicDetailsAreTheFieldsMarkedForTheResearch()
    {
        var details = BflPromptComposer.PublicDetailsOf(FormsWithAMarkerInEveryField());
        var expected = BflFieldCatalog.FIELDS.Where(field => field.IsSharedWithResearch).Select(field => field.Id).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(details.Select(detail => detail.FieldId), Is.EqualTo(expected));
            Assert.That(details.Select(detail => detail.Value), Is.EqualTo(expected.Select(id => $"MARKER-{id}")));
        }
    }

    [Test]
    public void TheTopicOfTheContributionNeverReachesTheResearch()
    {
        var field = BflFieldCatalog.FIELDS.Single(field => field.Id == BflFieldCatalog.CONTRIBUTION_TOPIC);

        Assert.That(field.IsSharedWithResearch, Is.False, "The topic of the contribution may be unpublished.");
    }

    [Test]
    public void ADetailOfAFieldWhichIsNotPublicIsLeftOut()
    {
        BflResearchDetail[] details =
        [
            new() { FieldId = BflFieldCatalog.EVENT_TOPIC, Value = "Klimapolitik" },
            new() { FieldId = BflFieldCatalog.CONTRIBUTION_TOPIC, Value = "Unveröffentlichte Studie" },
            new() { FieldId = "unknown_field", Value = "Unbekannt" },
        ];

        var prompt = BflPromptComposer.ComposeResearch(new("{{PUBLIC_DETAILS}}", "Auftrag"), details, "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("Klimapolitik"));
            Assert.That(prompt, Does.Not.Contain("Unveröffentlichte Studie"));
            Assert.That(prompt, Does.Not.Contain("Unbekannt"));
        }
    }

    [Test]
    public void TheResearchHasNoPlaceForAnythingButThePublicDetails()
    {
        var placeholders = BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.RESEARCH_PATH);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(placeholders, Is.EquivalentTo(new[] { BflPromptTemplate.RESPONSE_LANGUAGE, BflPromptTemplate.TOPIC, BflPromptTemplate.TOOL_CALLS, BflPromptTemplate.PUBLIC_DETAILS }));
            foreach (var topic in BflResearchTopic.All)
                Assert.That(BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.ResearchTopicPath(topic)), Is.Empty, topic.ToString());
        }
    }

    [Test]
    public async Task EveryTopicHasATaskOfItsOwn()
    {
        var paths = BflResearchTopic.All.Select(BflPromptLibrary.ResearchTopicPath).ToList();
        var fileNames = BflResearchTopic.All.Select(topic => topic.FileName).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paths, Is.Unique);
            Assert.That(fileNames, Is.Unique);
            Assert.That(BflPromptLibrary.AllPaths, Is.SupersetOf(paths).And.Contains(BflPromptLibrary.RESEARCH_PATH));
            foreach (var path in paths)
                Assert.That(await BflPromptLibrary.ReadAsync(path), Is.Not.Empty, path);
        }
    }

    [Test]
    public async Task TheAgentKnowsItsBudgetOfToolCalls()
    {
        var prompt = BflPromptComposer.ComposeResearch(await BflPromptLibrary.LoadResearchPartsAsync(BflResearchTopic.PARTICIPANTS), [], "Deutsch");

        Assert.That(prompt, Does.Contain($"höchstens {ToolSelectionRules.MAX_TOOL_CALLS} Werkzeugaufrufe"));
    }

    [Test]
    public async Task TheRequestNamesSectionsOfThePrompt()
    {
        var research = (await BflPromptLibrary.ReadAsync(BflPromptLibrary.RESEARCH_PATH)).ReplaceLineEndings("\n");
        var request = BflResearchRunner.REQUEST;

        using (Assert.EnterMultipleScope())
        {
            foreach (var section in new[] { "Dein Auftrag", "Vorgehen", "Form" })
            {
                Assert.That(request, Does.Contain($"„{section}“"));
                Assert.That(research, Does.Contain($"\n# {section}\n"));
            }
        }
    }

    [Test]
    public async Task TheSamePublicDetailsGiveTheSamePrompt()
    {
        var parts = await BflPromptLibrary.LoadResearchPartsAsync(BflResearchTopic.EVENT);
        var first = BflPromptComposer.ComposeResearch(parts, BflPromptComposer.PublicDetailsOf(FormsWithAMarkerInEveryField()), "Deutsch");
        var second = BflPromptComposer.ComposeResearch(parts, BflPromptComposer.PublicDetailsOf(FormsWithAMarkerInEveryField()), "Deutsch");

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void AResearchAgentSearchesTheWebAndReadsPagesInAChatOfItsOwn()
    {
        var provider = new AIStudio.Settings.Provider(1, "provider-1", "Test", LLMProviders.SELF_HOSTED, new Model("model", null));
        var first = BflThreadFactory.CreateHidden("research-event");
        var second = BflThreadFactory.CreateHidden("research-event");

        BflThreadFactory.PrepareForRequest(first, provider, "Prompt", BflResearchRunner.TOOL_IDS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflResearchRunner.TOOL_IDS, Is.EquivalentTo(new[] { ToolSelectionRules.WEB_SEARCH_TOOL_ID, ToolSelectionRules.READ_WEB_PAGE_TOOL_ID }));
            Assert.That(first.RuntimeSelectedToolIds, Is.EquivalentTo(BflResearchRunner.TOOL_IDS));
            Assert.That(first.RuntimeToolsAreAssistantManaged, Is.True);
            Assert.That(first.RequiredOutboundDataRestriction.Restriction, Is.EqualTo(OutboundDataRestriction.UNRESTRICTED));
            Assert.That(first.DataSourceOptions.DisableDataSources, Is.True);
            Assert.That(first.Blocks, Is.Empty);
            Assert.That(first.ChatId, Is.Not.EqualTo(Guid.Empty).And.Not.EqualTo(second.ChatId));
        }
    }

    private static IEnumerable<BflFieldDefinition> TextFields() => BflFieldCatalog.FIELDS.Where(field => field.Kind is not (BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE));

    private static string Marker(BflFieldDefinition field) => $"MARKER-{field.Id}";

    private static Dictionary<BflStep, BflStepFormData> FormsWithAMarkerInEveryField()
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        foreach (var field in TextFields())
            forms[field.Step].Set(field.Id, Marker(field));

        foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.Kind is BflFieldKind.CHOICE or BflFieldKind.MULTI_CHOICE))
            forms[field.Step].SetChoices(field, [field.Options[0].Id]);

        return forms;
    }
}