using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks that AI Studio comes with every prompt of the BeyondFacts Lab, and that they stay small enough.
/// </summary>
/// <remarks>
/// The tests run a development build, which reads the prompts from its output folder: the project file copies every
/// embedded file there as well.
/// </remarks>
[TestFixture]
public sealed class BflPromptLibraryTests
{
    /// <summary>
    /// How many characters the fixed part of the longest system prompt may have: the core, the guide, the longest step,
    /// and the form of address.
    /// </summary>
    /// <remarks>
    /// German takes about four characters per token, so this is about 22,000 tokens. The lab accepts models with a
    /// context of 32,000 tokens; the rest is left for the details of the person and the conversation. Raising this
    /// limit means raising that minimum, too.
    /// </remarks>
    private const int MAX_FIXED_PROMPT_CHARACTERS = 90_000;

    /// <summary>
    /// How many characters the fixed part of the prompt of the onboarding may have: the prompt and the longest form of
    /// address.
    /// </summary>
    /// <remarks>
    /// About 4,000 tokens. The onboarding asks for no minimum context window, unlike the steps, so it has to leave room
    /// for a conversation in small models as well.
    /// </remarks>
    private const int MAX_ONBOARDING_PROMPT_CHARACTERS = 16_000;

    [Test]
    public async Task EveryPromptComesWithTheApp()
    {
        foreach (var path in BflPromptLibrary.AllPaths)
            Assert.That(await BflPromptLibrary.ReadAsync(path), Is.Not.Empty, path);
    }

    [Test]
    public void EveryStepHasAPromptOfItsOwn()
    {
        var paths = BflStep.All.Select(BflPromptLibrary.StepPath).ToList();

        Assert.That(paths, Is.Unique);
    }

    [Test]
    public void BothFormsOfAddressHaveAnInstructionOfTheirOwn()
    {
        Assert.That(BflPromptLibrary.AddressPath(AddressForm.INFORMAL), Is.Not.EqualTo(BflPromptLibrary.AddressPath(AddressForm.FORMAL)));
    }

    [Test]
    public async Task EveryPromptUsesOnlyThePlaceholdersTheLabFillsInIt()
    {
        foreach (var path in BflPromptLibrary.AllPaths)
            Assert.That(BflPromptTemplate.NamesIn(await BflPromptLibrary.ReadBuiltInAsync(path)), Is.SubsetOf(BflPromptLibrary.PlaceholdersOf(path)), path);
    }

    [Test]
    public void TheComposerFillsEveryPlaceholderAPromptMayContain()
    {
        var core = string.Join("\n", BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.CORE_PATH).Select(name => $"{{{{{name}}}}}"));
        var onboarding = string.Join("\n", BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.ONBOARDING_PATH).Select(name => $"{{{{{name}}}}}"));
        var workState = string.Join("\n", BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.WORK_STATE_PATH).Select(name => $"{{{{{name}}}}}"));
        var result = string.Join("\n", BflPromptLibrary.PlaceholdersOf(BflPromptLibrary.ResultPath(BflResultKind.POSITION_PAPER)).Select(name => $"{{{{{name}}}}}"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflPromptComposer.Compose(new(core, "Leitfaden", "Schritt", "Anrede"), BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch"), Does.Not.Contain("{{"));
            Assert.That(BflPromptComposer.ComposeOnboarding(new(onboarding, "Anrede"), "Deutsch"), Does.Not.Contain("{{"));
            Assert.That(BflPromptComposer.ComposeWorkStateRequest(workState, BflStep.FRAMEWORK, new BflStepFormData()), Does.Not.Contain("{{"));
            Assert.That(BflPromptComposer.ComposeResultRequest(result, new Dictionary<BflStep, BflWorkState>()), Does.Not.Contain("{{"));
        }
    }

    [Test]
    public void EveryPromptLiesInTheFolderOfTheLab()
    {
        Assert.That(BflPromptLibrary.AllPaths.Select(BflPromptLibrary.RelativePath), Is.Unique.And.All.Not.StartWith("/"));
    }

    [Test]
    public async Task AMissingPromptIsAnError()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await BflPromptLibrary.ReadAsync("Assistants/BeyondFactsLab/Prompts/does-not-exist.md"));
    }

    [Test]
    public async Task TheFixedPartOfTheLongestPromptLeavesRoomForTheConversation()
    {
        var core = await BflPromptLibrary.ReadAsync(BflPromptLibrary.CORE_PATH);
        var knowledge = await BflPromptLibrary.ReadAsync(BflPromptLibrary.KNOWLEDGE_PATH);
        var longestStep = 0;
        foreach (var step in BflStep.All)
            longestStep = Math.Max(longestStep, (await BflPromptLibrary.ReadAsync(BflPromptLibrary.StepPath(step))).Length);

        var longestAddress = 0;
        foreach (var addressForm in Enum.GetValues<AddressForm>())
            longestAddress = Math.Max(longestAddress, (await BflPromptLibrary.ReadAsync(BflPromptLibrary.AddressPath(addressForm))).Length);

        Assert.That(core.Length + knowledge.Length + longestStep + longestAddress, Is.LessThanOrEqualTo(MAX_FIXED_PROMPT_CHARACTERS));
    }

    [Test]
    public async Task EveryOpeningRequestNamesASectionOfItsPrompt()
    {
        var core = (await BflPromptLibrary.ReadAsync(BflPromptLibrary.CORE_PATH)).ReplaceLineEndings("\n");
        var onboarding = (await BflPromptLibrary.ReadAsync(BflPromptLibrary.ONBOARDING_PATH)).ReplaceLineEndings("\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflConversationService.OpeningRequestFor(new BflStepThreadLocation(Guid.NewGuid(), BflStep.FRAMEWORK)), Does.Contain("„Dieser Schritt“"));
            Assert.That(core, Does.Contain("\n# Dieser Schritt\n"));
            Assert.That(BflConversationService.OpeningRequestFor(BflOnboardingThreadLocation.INSTANCE), Does.Contain("„Gesprächsführung“"));
            Assert.That(onboarding, Does.Contain("\n# Gesprächsführung\n"));
        }
    }

    [Test]
    public async Task ThePromptOfTheOnboardingStaysShort()
    {
        var onboarding = await BflPromptLibrary.ReadAsync(BflPromptLibrary.ONBOARDING_PATH);
        var longestAddress = 0;
        foreach (var addressForm in Enum.GetValues<AddressForm>())
            longestAddress = Math.Max(longestAddress, (await BflPromptLibrary.ReadAsync(BflPromptLibrary.AddressPath(addressForm))).Length);

        Assert.That(onboarding.Length + longestAddress, Is.LessThanOrEqualTo(MAX_ONBOARDING_PROMPT_CHARACTERS));
    }
}