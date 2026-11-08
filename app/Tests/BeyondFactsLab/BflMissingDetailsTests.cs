using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks which details of a preparation the BeyondFacts Lab marks as missing, and how a form keeps its values.
/// </summary>
[TestFixture]
public sealed class BflMissingDetailsTests
{
    [Test]
    public void ANewPreparationLacksTheDetailsOfTheEventAndTheContribution()
    {
        var steps = BflMissingDetails.StepsOf(new Dictionary<BflStep, BflStepFormData>());

        Assert.That(steps, Is.EqualTo(new[] { BflStep.FRAMEWORK, BflStep.KNOWLEDGE }));
    }

    [Test]
    public void TheMissingDetailsFollowTheOrderOfTheForm()
    {
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.EVENT_TOPIC, "Water protection");

        var missing = BflMissingDetails.Of(BflStep.FRAMEWORK, form);

        Assert.That(missing.Select(field => field.Id), Is.EqualTo(new[] { BflFieldCatalog.FORMAT, BflFieldCatalog.AUDIENCE, BflFieldCatalog.CONTRIBUTION_TOPIC }));
    }

    [Test]
    public void BlanksCountAsMissing()
    {
        var form = new BflStepFormData { Values = { [BflFieldCatalog.CONTRIBUTION_SUMMARY] = "  \n " } };

        Assert.That(BflMissingDetails.Of(BflStep.KNOWLEDGE, form), Has.Count.EqualTo(1));
    }

    [Test]
    public void AFilledFormLacksNothing()
    {
        var framework = new BflStepFormData();
        foreach (var field in BflFieldCatalog.ForStep(BflStep.FRAMEWORK))
            framework.Set(field.Id, "given");

        var knowledge = new BflStepFormData();
        knowledge.Set(BflFieldCatalog.CONTRIBUTION_SUMMARY, "A few sentences.");

        var steps = BflMissingDetails.StepsOf(new Dictionary<BflStep, BflStepFormData> { [BflStep.FRAMEWORK] = framework, [BflStep.KNOWLEDGE] = knowledge });

        Assert.That(steps, Is.Empty);
    }

    [Test]
    public void AnEmptyValueRemovesTheField()
    {
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.GOALS, "Inform the audience");

        form.Set(BflFieldCatalog.GOALS, " ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(form.Values, Is.Empty);
            Assert.That(form.Get(BflFieldCatalog.GOALS), Is.Empty);
        }
    }

    [Test]
    public void ChoicesAreStoredInTheOrderOfTheOptions()
    {
        var field = BflFieldCatalog.FIELDS.Single(candidate => candidate.Id == BflFieldCatalog.GOAL_DIRECTIONS);
        var form = new BflStepFormData();

        form.SetChoices(field, [BflFieldCatalog.GOAL_INFLUENCE_POLICY, BflFieldCatalog.GOAL_INFORM, "not_an_option"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(form.Get(field.Id), Is.EqualTo($"{BflFieldCatalog.GOAL_INFORM},{BflFieldCatalog.GOAL_INFLUENCE_POLICY}"));
            Assert.That(form.GetChoices(field.Id), Is.EquivalentTo(new[] { BflFieldCatalog.GOAL_INFORM, BflFieldCatalog.GOAL_INFLUENCE_POLICY }));
        }
    }

    [Test]
    public void NoChoiceRemovesTheField()
    {
        var field = BflFieldCatalog.FIELDS.Single(candidate => candidate.Id == BflFieldCatalog.GOAL_DIRECTIONS);
        var form = new BflStepFormData();
        form.SetChoices(field, [BflFieldCatalog.GOAL_INFORM]);

        form.SetChoices(field, []);

        Assert.That(form.Values, Is.Empty);
    }
}