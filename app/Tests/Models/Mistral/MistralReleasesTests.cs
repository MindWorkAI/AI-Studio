using AIStudio.Models;
using AIStudio.Models.Matching;
using AIStudio.Models.Mistral;
using AIStudio.Models.Registry;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Mistral;

/// <summary>
/// Checks how a Mistral release is read out of a model name.
/// </summary>
/// <remarks>
/// This is the one place in the rebuilt rules where a capability is calculated rather than stated.
/// Mistral names its models after the month they came out, and the same family name stands for
/// models which can and cannot see -- so nothing a pattern could match on tells them apart.
///
/// What makes it worth its own tests is that model names are full of four-digit numbers which are
/// not dates: parameter counts, context sizes, versions. Reading one of those as a release would
/// silently promise image input for a model which has none.
/// </remarks>
[TestFixture]
public sealed class MistralReleasesTests
{
    /// <summary>
    /// A release far enough in the future that no name in these tests reaches it by accident.
    /// </summary>
    private const int SOME_LATEST_RELEASE = 2604;

    [TestCase("mistral-large-2512", ExpectedResult = 2512, TestName = "The release is read from the end of the name")]
    [TestCase("ministral-14b-2512", ExpectedResult = 2512, TestName = "The size of a model is not its release")]
    [TestCase("ministral-8b-2410", ExpectedResult = 2410, TestName = "A one digit size next to the release is not part of it")]
    [TestCase("something-25120", ExpectedResult = MistralReleases.UNKNOWN, TestName = "A five digit block is not a release")]
    [TestCase("something-125120", ExpectedResult = MistralReleases.UNKNOWN, TestName = "A six digit block is not a release either")]
    [TestCase("something-1912", ExpectedResult = MistralReleases.UNKNOWN, TestName = "A year before Mistral named models after dates is not a release")]
    [TestCase("something-2513", ExpectedResult = MistralReleases.UNKNOWN, TestName = "A thirteenth month is not a release")]
    [TestCase("something-2500", ExpectedResult = MistralReleases.UNKNOWN, TestName = "A zeroth month is not a release")]
    [TestCase("open-mistral-nemo", ExpectedResult = MistralReleases.UNKNOWN, TestName = "A name without any number carries no release")]
    public int TheReleaseIsReadOnlyWhereThereIsOne(string modelId) => MistralReleases.Of(new ModelId(modelId), SOME_LATEST_RELEASE);

    [Test]
    public void TheLatestAliasBecomesWhateverItsFamilyPointsAt()
    {
        var release = MistralReleases.Of(new ModelId("mistral-large-latest"), SOME_LATEST_RELEASE);

        Assert.That(release, Is.EqualTo(SOME_LATEST_RELEASE));
    }

    [Test]
    public void AMarketingVersionBecomesTheReleaseItStandsFor()
    {
        //
        // And the more specific one has to win: read as plain text rather than as patterns, a rule
        // for "mistral-medium-3" would otherwise answer for "mistral-medium-3.5" as well and place
        // it eleven months too early, before the release which gave it reasoning.
        //
        Assert.Multiple(() =>
        {
            Assert.That(MistralReleases.Of(new ModelId("mistral-medium-3"), SOME_LATEST_RELEASE), Is.EqualTo(2505));
            Assert.That(MistralReleases.Of(new ModelId("mistral-medium-3.5"), SOME_LATEST_RELEASE), Is.EqualTo(2604));
            Assert.That(MistralReleases.Of(new ModelId("mistral-medium-3-5"), SOME_LATEST_RELEASE), Is.EqualTo(2604), "Mistral writes the version separator both ways for the same model.");
        });
    }

    [Test]
    public void OneFamilyAnswersDifferentlyForTwoOfItsOwnReleases()
    {
        //
        // The point of the whole calculation, in one assertion: both names select the same family
        // and the same rule, and the model differs.
        //
        var beforeItCouldSee = ModelRegistry.Shared.Profile(LLMProviders.MISTRAL, "mistral-large-2411");
        var afterwards = ModelRegistry.Shared.Profile(LLMProviders.MISTRAL, "mistral-large-2512");

        Assert.Multiple(() =>
        {
            Assert.That(beforeItCouldSee.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.False);
            Assert.That(beforeItCouldSee.Reasoning, Is.EqualTo(ReasoningSupport.NONE));

            Assert.That(afterwards.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True);
            Assert.That(afterwards.Reasoning, Is.EqualTo(ReasoningSupport.OPTIONAL));
        });
    }

    [Test]
    public void AReleaseWhichCannotBeReadGrantsNothing()
    {
        //
        // The safe direction: offering an ability the model does not have makes the request fail,
        // while a missing one can be handed back by a person through the expert settings.
        //
        var profile = ModelRegistry.Shared.Profile(LLMProviders.MISTRAL, "mistral-large-whenever");

        Assert.Multiple(() =>
        {
            Assert.That(profile.Has(Capability.FUNCTION_CALLING), Is.True, "What the family could always do is still stated.");
            Assert.That(profile.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.False);
            Assert.That(profile.Reasoning, Is.EqualTo(ReasoningSupport.NONE));
        });
    }

    [Test]
    public void AFamilyWhichNeverReasonedDoesNotStartWithItsNewestRelease()
    {
        var newest = ModelRegistry.Shared.Profile(LLMProviders.MISTRAL, "ministral-3b-latest");

        Assert.Multiple(() =>
        {
            Assert.That(newest.Has(Capability.MULTIPLE_IMAGE_INPUT), Is.True, "Ministral 3 reads images.");
            Assert.That(newest.Reasoning, Is.EqualTo(ReasoningSupport.NONE), "No Ministral reasons, whatever its release.");
        });
    }
}