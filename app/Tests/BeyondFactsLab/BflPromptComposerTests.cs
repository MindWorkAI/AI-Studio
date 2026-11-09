using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab puts together the system prompt of a step.
/// </summary>
[TestFixture]
public sealed class BflPromptComposerTests
{
    private const string CORE = "Sprache: {{RESPONSE_LANGUAGE}}\n{{ADDRESS_FORM}}\n{{KNOWLEDGE}}\n{{STEP}}\n{{MATERIALS}}\n{{DETAILS}}\n{{MISSING_DETAILS}}";

    private static readonly BflPromptParts PARTS = new(CORE, "Leitfaden", "Schritt", "Anrede");

    [Test]
    public void APlaceholderWithoutAValueIsAnError()
    {
        var exception = Assert.Throws<FormatException>(() => BflPromptTemplate.Fill("Hallo {{UNKNOWN}}", new Dictionary<string, string>()));

        Assert.That(exception!.Message, Does.Contain("UNKNOWN"));
    }

    [Test]
    public void AMisspelledPlaceholderIsAnError()
    {
        Assert.Throws<FormatException>(() => BflPromptTemplate.Fill("Hallo {{ step }}", new Dictionary<string, string> { [BflPromptTemplate.STEP] = "Schritt" }));
    }

    [Test]
    public void AValueIsInsertedAsItIs()
    {
        var prompt = BflPromptTemplate.Fill("{{STEP}}", new Dictionary<string, string> { [BflPromptTemplate.STEP] = "Die Person schrieb {{KNOWLEDGE}}." });

        Assert.That(prompt, Is.EqualTo("Die Person schrieb {{KNOWLEDGE}}."), "What the person entered must never be read as a placeholder.");
    }

    [Test]
    public async Task ThePromptOfEveryStepFillsEveryPlaceholder()
    {
        foreach (var step in BflStep.All)
        {
            var parts = await BflPromptLibrary.LoadPartsAsync(step, AddressForm.FORMAL);
            var prompt = BflPromptComposer.Compose(parts, step, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(prompt, Does.Not.Contain("{{"), step.ToString());
                Assert.That(prompt, Does.Contain(parts.Step.Trim()), step.ToString());
                Assert.That(prompt, Does.Contain(parts.Knowledge.Trim()[..200]), step.ToString());
            }
        }
    }

    [Test]
    public async Task TheFormOfAddressDecidesTheInstruction()
    {
        var formal = BflPromptComposer.Compose(await BflPromptLibrary.LoadPartsAsync(BflStep.FRAMEWORK, AddressForm.FORMAL), BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch");
        var informal = BflPromptComposer.Compose(await BflPromptLibrary.LoadPartsAsync(BflStep.FRAMEWORK, AddressForm.INFORMAL), BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(formal, Does.Contain("mit „Sie“"));
            Assert.That(formal, Does.Not.Contain("mit „du“"));
            Assert.That(informal, Does.Contain("mit „du“"));
            Assert.That(informal, Does.Not.Contain("mit „Sie“"));
        }
    }

    [Test]
    public async Task TheOnboardingFillsEveryPlaceholderWithoutTheGuide()
    {
        var parts = await BflPromptLibrary.LoadOnboardingPartsAsync(AddressForm.FORMAL);
        var knowledge = await BflPromptLibrary.ReadAsync(BflPromptLibrary.KNOWLEDGE_PATH);

        var prompt = BflPromptComposer.ComposeOnboarding(parts, "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Not.Contain("{{"));
            Assert.That(prompt, Does.Contain("Antworte auf Deutsch."));
            Assert.That(prompt, Does.Not.Contain(knowledge.Trim()[..200]), "The onboarding stays short, so it leaves out the guide.");
        }
    }

    [Test]
    public async Task TheFormOfAddressDecidesTheInstructionOfTheOnboarding()
    {
        var formal = BflPromptComposer.ComposeOnboarding(await BflPromptLibrary.LoadOnboardingPartsAsync(AddressForm.FORMAL), "Deutsch");
        var informal = BflPromptComposer.ComposeOnboarding(await BflPromptLibrary.LoadOnboardingPartsAsync(AddressForm.INFORMAL), "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(formal, Does.Contain("mit „Sie“"));
            Assert.That(formal, Does.Not.Contain("mit „du“"));
            Assert.That(informal, Does.Contain("mit „du“"));
            Assert.That(informal, Does.Not.Contain("mit „Sie“"));
        }
    }

    [Test]
    public void TheOnboardingIsTheSameOnEverySystem()
    {
        var parts = new BflOnboardingPromptParts("Sprache: {{RESPONSE_LANGUAGE}}\r\n{{ADDRESS_FORM}}\r\nEnde", " Anrede\r\n");

        var prompt = BflPromptComposer.ComposeOnboarding(parts, "Deutsch");

        Assert.That(prompt, Is.EqualTo("Sprache: Deutsch\nAnrede\nEnde"));
    }

    [Test]
    public void TheOnboardingKnowsNoDetailsOfAPreparation()
    {
        var parts = new BflOnboardingPromptParts("{{DETAILS}}", "Anrede");

        Assert.Throws<FormatException>(() => BflPromptComposer.ComposeOnboarding(parts, "Deutsch"));
    }

    [Test]
    public void TheLanguageOfTheAnswersIsNamed()
    {
        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [], "English (United States)");

        Assert.That(prompt, Does.StartWith("Sprache: English (United States)\n"));
    }

    [Test]
    public void TheMissingKeyDetailsAreListed()
    {
        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch");

        foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.IsKeyDetail))
            Assert.That(prompt, Does.Contain(field.Label), field.Id);
    }

    [Test]
    public void NothingIsMissingWhenEveryKeyDetailIsThere()
    {
        var forms = BflStep.All.ToDictionary(step => step, _ => new BflStepFormData());
        foreach (var field in BflFieldCatalog.FIELDS.Where(field => field.IsKeyDetail))
            forms[field.Step].Set(field.Id, "vorhanden");

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, forms, [], "Deutsch");

        Assert.That(prompt, Does.EndWith("Es fehlen keine wichtigen Angaben."));
    }

    [Test]
    public void TheDetailsShowTheLabelsAndTheChosenOptions()
    {
        var framework = new BflStepFormData();
        framework.Set(BflFieldCatalog.FORMAT, "Podiumsdiskussion");
        var goals = new BflStepFormData();
        var directions = BflFieldCatalog.FIELDS.Single(field => field.Id == BflFieldCatalog.GOAL_DIRECTIONS);
        goals.SetChoices(directions, [BflFieldCatalog.GOAL_INFORM, BflFieldCatalog.GOAL_SHOW_OPTIONS]);
        var forms = new Dictionary<BflStep, BflStepFormData> { [BflStep.FRAMEWORK] = framework, [BflStep.GOALS_AND_ROLE] = goals };

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, forms, [], "Deutsch");
        var inform = directions.Options.Single(option => option.Id == BflFieldCatalog.GOAL_INFORM).Label;
        var showOptions = directions.Options.Single(option => option.Id == BflFieldCatalog.GOAL_SHOW_OPTIONS).Label;
        var format = BflFieldCatalog.FIELDS.Single(field => field.Id == BflFieldCatalog.FORMAT).Label;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain($"- {format}: Podiumsdiskussion"));
            Assert.That(prompt, Does.Contain($"- {directions.Label}: {inform}, {showOptions}"));
            Assert.That(prompt, Does.Contain($"## {BflStep.FRAMEWORK.Title} (dieser Schritt)"));
            Assert.That(prompt, Does.Not.Contain(BflFieldCatalog.GOAL_INFORM), "The model reads the labels, not the IDs.");
        }
    }

    [Test]
    public void ALongTextKeepsItsLines()
    {
        var knowledge = new BflStepFormData();
        knowledge.Set(BflFieldCatalog.CORE_THESES, "Erste These\r\nZweite These");

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.KNOWLEDGE, new Dictionary<BflStep, BflStepFormData> { [BflStep.KNOWLEDGE] = knowledge }, [], "Deutsch");

        Assert.That(prompt, Does.Contain(": Erste These\n  Zweite These"));
    }

    [Test]
    public void APersonalDetailReachesOnlyTheChatOfItsOwnStep()
    {
        var values = new BflStepFormData();
        values.Set(BflFieldCatalog.PERSONAL_CONCERN, "VERTRAULICH-4711");
        values.Set(BflFieldCatalog.VALUES_AND_CONCERNS, "Offenheit");
        var forms = new Dictionary<BflStep, BflStepFormData> { [BflStep.VALUES_AND_PERSPECTIVES] = values };

        foreach (var step in BflStep.All)
        {
            var prompt = BflPromptComposer.Compose(PARTS, step, forms, [], "Deutsch");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(prompt.Contains("VERTRAULICH-4711", StringComparison.Ordinal), Is.EqualTo(step is BflStep.VALUES_AND_PERSPECTIVES), step.ToString());
                Assert.That(prompt, Does.Contain("Offenheit"), "Only what the person confided stays in its step.");
            }
        }
    }

    [Test]
    public void TheSameFormsGiveTheSamePrompt()
    {
        var framework = new BflStepFormData();
        framework.Set(BflFieldCatalog.AUDIENCE, "Öffentlichkeit");
        framework.Set(BflFieldCatalog.FORMAT, "Interview");
        var strategy = new BflStepFormData();
        strategy.Set(BflFieldCatalog.COMMUNICATION_WORRIES, "Zeitdruck");

        var first = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, new Dictionary<BflStep, BflStepFormData> { [BflStep.FRAMEWORK] = framework, [BflStep.STRATEGY] = strategy }, [], "Deutsch");
        var second = BflPromptComposer.Compose(PARTS, BflStep.STRATEGY, new Dictionary<BflStep, BflStepFormData> { [BflStep.STRATEGY] = strategy, [BflStep.FRAMEWORK] = framework }, [], "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Not.Contain("\r"));
            Assert.That(first.IndexOf("Interview", StringComparison.Ordinal), Is.LessThan(first.IndexOf("Öffentlichkeit", StringComparison.Ordinal)), "The details follow the order of the forms.");
        }
    }

    [Test]
    public void APreparationWithoutMaterialSaysSo()
    {
        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [], "Deutsch");

        Assert.That(prompt, Does.Contain("Die Person hat kein Material hinzugefügt."));
    }

    [Test]
    public void TheMaterialComesWithItsNameAndText()
    {
        var invitation = Excerpt("Einladung.docx", "Podium am 10. Dezember\r\nim Audimax", 10_000);
        var paper = Excerpt("Paper.pdf", "Abstract", 10_000);

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [invitation, paper], "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("## Material 1: „Einladung.docx“\n\nPodium am 10. Dezember\nim Audimax"));
            Assert.That(prompt, Does.Contain("## Material 2: „Paper.pdf“\n\nAbstract"));
            Assert.That(prompt, Does.Not.Contain("Gekürzt"));
        }
    }

    [Test]
    public void AShortenedMaterialSaysHowMuchOfItIsThere()
    {
        var material = new BflMaterial { MaterialId = Guid.NewGuid(), Name = "Paper.pdf", TextLength = 1_000 };
        var shortened = new BflMaterialExcerpt(new(material, 400), "Der Anfang");
        var missing = new BflMaterialExcerpt(new(new BflMaterial { MaterialId = Guid.NewGuid(), Name = "Buch.pdf", TextLength = 1_000 }, 0), string.Empty);

        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [shortened, missing], "Deutsch");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt, Does.Contain("(Gekürzt: Hier steht nur der Anfang, etwa 40 % des Materials."));
            Assert.That(prompt, Does.Contain("## Material 2: „Buch.pdf“\n(Für dieses Material reicht der Platz im Kontext des Modells nicht. Du kennst nur seinen Namen.)"));
        }
    }

    [Test]
    public void APlaceholderInTheMaterialStaysText()
    {
        var prompt = BflPromptComposer.Compose(PARTS, BflStep.FRAMEWORK, new Dictionary<BflStep, BflStepFormData>(), [Excerpt("Notizen.md", "Siehe {{KNOWLEDGE}}", 100)], "Deutsch");

        Assert.That(prompt, Does.Contain("Siehe {{KNOWLEDGE}}"));
    }

    [Test]
    public async Task TheMaterialStandsBetweenTheStepAndTheDetails()
    {
        var parts = await BflPromptLibrary.LoadPartsAsync(BflStep.KNOWLEDGE, AddressForm.FORMAL);
        var knowledge = new BflStepFormData();
        knowledge.Set(BflFieldCatalog.CORE_THESES, "THESE-4711");

        var prompt = BflPromptComposer.Compose(parts, BflStep.KNOWLEDGE, new Dictionary<BflStep, BflStepFormData> { [BflStep.KNOWLEDGE] = knowledge }, [Excerpt("Paper.pdf", "MATERIAL-4711", 100)], "Deutsch");
        var material = prompt.IndexOf("MATERIAL-4711", StringComparison.Ordinal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prompt.IndexOf(parts.Step.Trim(), StringComparison.Ordinal), Is.LessThan(material), "The material changes less often than the details, so it comes first for the prompt caching.");
            Assert.That(material, Is.LessThan(prompt.IndexOf("THESE-4711", StringComparison.Ordinal)));
        }
    }

    [Test]
    public void TheOnboardingKnowsNoMaterial()
    {
        var parts = new BflOnboardingPromptParts("{{MATERIALS}}", "Anrede");

        Assert.Throws<FormatException>(() => BflPromptComposer.ComposeOnboarding(parts, "Deutsch"));
    }

    private static BflMaterialExcerpt Excerpt(string name, string text, int textLength) => new(new(new BflMaterial { MaterialId = Guid.NewGuid(), Name = name, TextLength = textLength }, textLength), text);
}