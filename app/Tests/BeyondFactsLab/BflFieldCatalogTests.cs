using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the steps and the fields of the forms "Your details" in the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// The forms store their values under the IDs of the fields and options, and the folders of the steps carry their
/// names, so all of them are pinned here: changing one would lose what people entered before.
/// </remarks>
[TestFixture]
public sealed class BflFieldCatalogTests
{
    [Test]
    public void TheStepsKeepTheirNamesAndOrder()
    {
        Assert.That(BflStep.All.Select(step => step.ToString()), Is.EqualTo(new[]
        {
            "FRAMEWORK", "KNOWLEDGE", "GOALS_AND_ROLE", "VALUES_AND_PERSPECTIVES", "STRATEGY", "TEST_SITUATION", "RESULTS",
        }));
    }

    [Test]
    public void TheFoldersOfTheStepsKeepTheirNames()
    {
        Assert.That(BflStep.All.Select(step => step.FolderName), Is.EqualTo(new[]
        {
            "01-framework", "02-knowledge", "03-goals", "04-values", "05-strategy", "06-test", "07-results",
        }));
    }

    [Test]
    public void TheFieldsKeepTheirIds()
    {
        Assert.That(BflFieldCatalog.FIELDS.Select(field => field.Id), Is.EqualTo(new[]
        {
            "format", "event_topic", "audience", "contribution_topic", "event_date", "event_location", "event_duration", "contribution_duration", "other_participants", "event_link",
            "contribution_summary", "core_theses", "uncertainties", "material_notes",
            "goals", "goal_directions",
            "values_and_concerns", "personal_concern", "other_perspectives",
            "communication_worries",
            "question_type", "difficulty", "test_notes",
            "position_changes",
        }));
    }

    [Test]
    public void TheOptionsKeepTheirIds()
    {
        Assert.That(BflFieldCatalog.FIELDS.SelectMany(field => field.Options).Select(option => option.Id), Is.EqualTo(new[]
        {
            "goal_inform", "goal_moderate", "goal_take_position", "goal_raise_awareness", "goal_show_options", "goal_influence_policy",
            "questions_mixed", "questions_scientific_criticism", "questions_emotional_criticism", "questions_manipulative", "questions_political_instrumentalization",
            "difficulty_moderate", "difficulty_demanding", "difficulty_very_demanding",
        }));
    }

    [Test]
    public void EveryIdIsUnique()
    {
        var ids = BflFieldCatalog.FIELDS.Select(field => field.Id).Concat(BflFieldCatalog.FIELDS.SelectMany(field => field.Options).Select(option => option.Id)).ToList();

        Assert.That(ids, Is.Unique);
    }

    [Test]
    public void TheFieldsOfAStepFollowEachOther()
    {
        var stepsInOrder = BflFieldCatalog.FIELDS.Select(field => field.Step).Distinct();

        Assert.That(stepsInOrder, Is.EqualTo(BflStep.All));
    }

    [Test]
    public void EveryStepHasAFormWithATitleAndADescription()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var step in BflStep.All)
            {
                Assert.That(BflFieldCatalog.ForStep(step), Is.Not.Empty, $"{step} has no fields.");
                Assert.That(step.Title, Is.Not.Empty, $"{step} has no title.");
                Assert.That(step.Description, Is.Not.Empty, $"{step} has no description.");
            }
        }
    }

    [Test]
    public void OnlyFieldsWithAChoiceHaveOptions()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var field in BflFieldCatalog.FIELDS)
            {
                if (field.Kind.HasOptions)
                    Assert.That(field.Options, Has.Count.GreaterThanOrEqualTo(2), $"{field.Id} offers too few options.");
                else
                    Assert.That(field.Options, Is.Empty, $"{field.Id} has options, but nothing shows them.");
            }
        }
    }

    [Test]
    public void EveryKindHasALimit()
    {
        Assert.That(Enum.GetValues<BflFieldKind>().Select(kind => kind.MaxLength), Has.All.GreaterThan(0));
    }

    [Test]
    public void EveryFieldAndOptionHasALabel()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var field in BflFieldCatalog.FIELDS)
            {
                Assert.That(field.Label, Is.Not.Empty, $"{field.Id} has no label.");
                foreach (var option in field.Options)
                    Assert.That(option.Label, Is.Not.Empty, $"{option.Id} has no label.");
            }
        }
    }

    [Test]
    public void EveryFieldAndOptionHasALabelOfItsOwn()
    {
        // Equal English texts share one key of the localization, so another language could not tell them apart:
        var labels = BflFieldCatalog.FIELDS.SelectMany(field => field.Options.Select(option => option.Label).Prepend(field.Label));

        Assert.That(labels, Is.Unique);
    }

    [Test]
    public void OnlyPublicDetailsAboutTheEventReachTheResearch()
    {
        var shared = BflFieldCatalog.FIELDS.Where(field => field.IsSharedWithResearch).Select(field => field.Id);

        Assert.That(shared, Is.EquivalentTo(new[]
        {
            BflFieldCatalog.FORMAT, BflFieldCatalog.EVENT_TOPIC, BflFieldCatalog.EVENT_DATE, BflFieldCatalog.EVENT_LOCATION, BflFieldCatalog.OTHER_PARTICIPANTS, BflFieldCatalog.EVENT_LINK,
        }), "The topic of a contribution may be unpublished, and nothing personal belongs on the web.");
    }

    [Test]
    public void OnlyThePersonalInvolvementIsPersonal()
    {
        var personal = BflFieldCatalog.FIELDS.Where(field => field.IsPersonalDisclosure).Select(field => field.Id);

        Assert.That(personal, Is.EquivalentTo(new[] { BflFieldCatalog.PERSONAL_CONCERN }));
    }

    [Test]
    public void OnlyTheOtherParticipantsHoldNames()
    {
        var withNames = BflFieldCatalog.FIELDS.Where(field => field.NameRole is not BflNameRole.NONE).Select(field => field.Id);

        Assert.That(withNames, Is.EquivalentTo(new[] { BflFieldCatalog.OTHER_PARTICIPANTS }));
    }

    [Test]
    public void TheKeyDetailsAreThoseOfTheEventAndTheContribution()
    {
        var keyDetails = BflFieldCatalog.FIELDS.Where(field => field.IsKeyDetail).Select(field => field.Id);

        Assert.That(keyDetails, Is.EquivalentTo(new[]
        {
            BflFieldCatalog.FORMAT, BflFieldCatalog.EVENT_TOPIC, BflFieldCatalog.AUDIENCE, BflFieldCatalog.CONTRIBUTION_TOPIC, BflFieldCatalog.CONTRIBUTION_SUMMARY,
        }));
    }
}