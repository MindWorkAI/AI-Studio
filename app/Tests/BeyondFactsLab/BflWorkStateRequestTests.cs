using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the request which lets the model write the work state of a step in the BeyondFacts Lab.
/// </summary>
[TestFixture]
public sealed class BflWorkStateRequestTests
{
    [Test]
    public async Task TheRequestOfEveryStepNamesEachOfItsSectionsWithItsHeading()
    {
        var template = await BflPromptLibrary.ReadAsync(BflPromptLibrary.WORK_STATE_PATH);
        foreach (var step in BflStep.All)
        {
            var request = BflPromptComposer.ComposeWorkStateRequest(template, step, new BflStepFormData());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(request, Does.Not.Contain("{{"), step.ToString());
                foreach (var section in BflWorkStateSections.ForStep(step))
                    Assert.That(request, Does.Contain($"## [{section.Key}] {section.Title}"), $"{step}: {section.Key}");
            }
        }
    }

    [Test]
    public void TheRequestOffersSuggestionsOnlyForEmptyFields()
    {
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.FORMAT, "Podium");

        var request = BflPromptComposer.ComposeWorkStateRequest("{{SECTIONS}}\n{{FIELDS}}", BflStep.FRAMEWORK, form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Does.Contain($"[{BflMarkdownSections.FIELD_PREFIX}{BflFieldCatalog.AUDIENCE}]"));
            Assert.That(request, Does.Contain($"[{BflMarkdownSections.FIELD_PREFIX}{BflFieldCatalog.EVENT_LINK}]"));
            Assert.That(request, Does.Not.Contain($"[{BflMarkdownSections.FIELD_PREFIX}{BflFieldCatalog.FORMAT}]"));
        }
    }

    [Test]
    public void TheRequestNamesTheOptionsOfAnEmptyChoice()
    {
        var difficulty = BflFieldCatalog.FIELDS.Single(field => field.Id == BflFieldCatalog.DIFFICULTY);

        var request = BflPromptComposer.ComposeWorkStateRequest("{{FIELDS}}", BflStep.TEST_SITUATION, new BflStepFormData());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Does.Contain($"[{BflMarkdownSections.FIELD_PREFIX}{BflFieldCatalog.DIFFICULTY}]"));
            foreach (var option in difficulty.Options)
                Assert.That(request, Does.Contain($"„{option.Label}“"), option.Id);
        }
    }

    [Test]
    public void ARequestWithoutEmptyFieldsAsksForNoSuggestions()
    {
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.COMMUNICATION_WORRIES, "Sorge");

        var request = BflPromptComposer.ComposeWorkStateRequest("{{FIELDS}}", BflStep.STRATEGY, form);

        Assert.That(request, Does.Not.Contain(BflMarkdownSections.FIELD_PREFIX));
    }

    [Test]
    public void TheRequestToCorrectAWorkStateNamesEveryProblem()
    {
        var request = BflPromptComposer.ComposeWorkStateRepairRequest(["Der Abschnitt [GOAL] fehlt.", "Den Abschnitt [X] gibt es in diesem Schritt nicht."]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request, Does.Contain("- Der Abschnitt [GOAL] fehlt."));
            Assert.That(request, Does.Contain("- Den Abschnitt [X] gibt es in diesem Schritt nicht."));
        }
    }

    [Test]
    public void TheBuiltInRequestIsAPromptTheTestFolderMayReplace()
    {
        Assert.That(BflPromptLibrary.AllPaths, Does.Contain(BflPromptLibrary.WORK_STATE_PATH));
    }
}