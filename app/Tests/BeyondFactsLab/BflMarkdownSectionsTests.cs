using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab reads a work state which a model wrote as Markdown.
/// </summary>
/// <remarks>
/// The keys of the sections are stored in every work state, so they are pinned here as well.
/// </remarks>
[TestFixture]
public sealed class BflMarkdownSectionsTests
{
    [Test]
    public void TheSectionsKeepTheirKeys()
    {
        Assert.That(BflWorkStateSections.SECTIONS.Select(section => $"{section.Step}/{section.Key}"), Is.EqualTo(new[]
        {
            "FRAMEWORK/FORMAT", "FRAMEWORK/AUDIENCE", "FRAMEWORK/SPEAKING_TIME", "FRAMEWORK/PARTICIPANTS", "FRAMEWORK/CONFLICTS", "FRAMEWORK/OPEN_POINTS",
            "KNOWLEDGE/ESTABLISHED", "KNOWLEDGE/UNCERTAINTIES", "KNOWLEDGE/WEAK_POINTS", "KNOWLEDGE/CONTROVERSIES", "KNOWLEDGE/EXPERTISE_LIMITS", "KNOWLEDGE/KEY_MESSAGES", "KNOWLEDGE/OPEN_POINTS",
            "GOALS_AND_ROLE/GOAL", "GOALS_AND_ROLE/ROLE", "GOALS_AND_ROLE/REASONS", "GOALS_AND_ROLE/CONSEQUENCES", "GOALS_AND_ROLE/OPEN_POINTS",
            "VALUES_AND_PERSPECTIVES/VALUES", "VALUES_AND_PERSPECTIVES/VALUE_CONFLICTS", "VALUES_AND_PERSPECTIVES/PERSONAL_INVOLVEMENT", "VALUES_AND_PERSPECTIVES/PERSPECTIVES", "VALUES_AND_PERSPECTIVES/CONSEQUENCES", "VALUES_AND_PERSPECTIVES/OPEN_POINTS",
            "STRATEGY/KEY_MESSAGES", "STRATEGY/STRUCTURE", "STRATEGY/LANGUAGE", "STRATEGY/UNCERTAINTY", "STRATEGY/FRAMING", "STRATEGY/COUNTERARGUMENTS", "STRATEGY/WORRIES", "STRATEGY/OPEN_POINTS",
            "TEST_SITUATION/STRENGTHS", "TEST_SITUATION/PRACTICE", "TEST_SITUATION/HARD_QUESTIONS", "TEST_SITUATION/POSITION", "TEST_SITUATION/OPEN_POINTS",
            "RESULTS/CHANGED", "RESULTS/KEPT", "RESULTS/LEFT_OPEN", "RESULTS/NEXT_STEPS",
        }));
    }

    [Test]
    public void EveryStepHasSectionsWithUniqueKeysAndTitles()
    {
        foreach (var step in BflStep.All)
        {
            var sections = BflWorkStateSections.ForStep(step);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(sections, Is.Not.Empty, step.ToString());
                Assert.That(sections.Select(section => section.Key), Is.Unique, step.ToString());
                Assert.That(sections.Select(section => section.Title), Is.Unique, step.ToString());
                Assert.That(sections.Select(section => section.Title), Has.None.EqualTo(string.Empty), step.ToString());
                Assert.That(sections.Select(section => section.Description), Has.All.Not.Empty, step.ToString());
            }
        }
    }

    [Test]
    public void OnlyThePersonalInvolvementIsPersonal()
    {
        Assert.That(BflWorkStateSections.SECTIONS.Where(section => section.IsPersonalDisclosure).Select(section => section.Key), Is.EqualTo(new[] { "PERSONAL_INVOLVEMENT" }));
    }

    [TestCase("## [GOAL] Ziel", TestName = "AHeadingOfTheSecondLevelIsASection")]
    [TestCase("### [GOAL] Ziel", TestName = "AHeadingOfAnotherLevelIsASection")]
    [TestCase("##[GOAL]", TestName = "AHeadingWithoutSpaceAndTitleIsASection")]
    [TestCase("## **[GOAL] Ziel**", TestName = "ABoldHeadingIsASection")]
    [TestCase("## [ goal ] Ziel", TestName = "TheKeyIgnoresCaseAndSpaces")]
    [TestCase("   ## [GOAL] Ziel", TestName = "AnIndentedHeadingIsASection")]
    public void TheHeadingOfASectionMayVary(string heading)
    {
        var answer = Read($"{heading}\nWirksam informieren.\n\n## [ROLE]\nEhrliche Maklerin\n## [REASONS]\n## [CONSEQUENCES]\nKurz fassen.\n## [OPEN_POINTS]\n");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.Problems, Is.Empty);
            Assert.That(answer.Sections["GOAL"], Is.EqualTo("Wirksam informieren."));
            Assert.That(answer.Sections["ROLE"], Is.EqualTo("Ehrliche Maklerin"));
            Assert.That(answer.Sections["REASONS"], Is.Empty);
            Assert.That(answer.Sections["OPEN_POINTS"], Is.Empty);
        }
    }

    [Test]
    public void TextBeforeTheFirstHeadingAndACodeBlockAroundTheAnswerAreLeftOut()
    {
        var answer = Read("Hier ist der Arbeitsstand:\n```markdown\n## [GOAL] Ziel\nInformieren\n- Punkt eins\n- Punkt zwei\n## [ROLE] Rolle\nMaklerin\n## [REASONS] Gründe\n## [CONSEQUENCES] Folgen\n## [OPEN_POINTS] Offen\nKeine\n```");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.Problems, Is.Empty);
            Assert.That(answer.Sections["GOAL"], Is.EqualTo("Informieren\n- Punkt eins\n- Punkt zwei"));
            Assert.That(answer.Sections["OPEN_POINTS"], Is.EqualTo("Keine"));
        }
    }

    [Test]
    public void AHeadingWithoutKeyBelongsToTheTextOfItsSection()
    {
        var answer = Read("## [GOAL] Ziel\n### Kurz gesagt\nInformieren\n## [ROLE]\n## [REASONS]\n## [CONSEQUENCES]\n## [OPEN_POINTS]");

        Assert.That(answer.Sections["GOAL"], Is.EqualTo("### Kurz gesagt\nInformieren"));
    }

    [Test]
    public void AMissingSectionIsAProblem()
    {
        var answer = Read("## [GOAL]\nInformieren\n## [ROLE]\n## [CONSEQUENCES]\n## [OPEN_POINTS]");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.Problems, Has.Count.EqualTo(1));
            Assert.That(answer.Problems[0], Does.Contain("[REASONS]"));
            Assert.That(answer.Sections.Keys, Is.EquivalentTo(new[] { "GOAL", "ROLE", "CONSEQUENCES", "OPEN_POINTS" }));
        }
    }

    [Test]
    public void ADuplicateSectionIsAProblemAndTheFirstOneCounts()
    {
        var answer = Read("## [GOAL]\nErstens\n## [GOAL]\nZweitens\n## [ROLE]\n## [REASONS]\n## [CONSEQUENCES]\n## [OPEN_POINTS]");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.Problems, Has.Count.EqualTo(1));
            Assert.That(answer.Problems[0], Does.Contain("[GOAL]"));
            Assert.That(answer.Sections["GOAL"], Is.EqualTo("Erstens"));
        }
    }

    [Test]
    public void AnUnknownSectionIsAProblem()
    {
        var answer = Read("## [GOAL]\n## [ROLE]\n## [REASONS]\n## [CONSEQUENCES]\n## [OPEN_POINTS]\n## [AUDIENCE] Publikum\nStudierende");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.Problems, Has.Count.EqualTo(1));
            Assert.That(answer.Problems[0], Does.Contain("[AUDIENCE]"));
            Assert.That(answer.Sections.ContainsKey("AUDIENCE"), Is.False);
        }
    }

    [Test]
    public void AnAnswerWithoutSectionsIsAProblem()
    {
        var answer = Read("Ich habe leider keine Angaben.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.Problems, Has.Count.EqualTo(1));
            Assert.That(answer.Sections, Is.Empty);
        }
    }

    [Test]
    public void ASuggestionForAnEmptyTextFieldIsRead()
    {
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.FORMAT, "Podium");

        var answer = BflMarkdownSections.Read(FrameworkWithSuggestions("## [FIELD:audience] Zielgruppe\nSchüler*innen\n## [field:EVENT_LINK]\nhttps://example.org/podium"), BflStep.FRAMEWORK, form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.Problems, Is.Empty);
            Assert.That(answer.FieldSuggestions, Is.EqualTo(new Dictionary<string, string>
            {
                [BflFieldCatalog.AUDIENCE] = "Schüler*innen",
                [BflFieldCatalog.EVENT_LINK] = "https://example.org/podium",
            }));
        }
    }

    [TestCase("## [FIELD:format]\nInterview", TestName = "AFieldWhichHoldsAValueGetsNoSuggestion")]
    [TestCase("## [FIELD:unknown_field]\nWert", TestName = "AnUnknownFieldGetsNoSuggestion")]
    [TestCase("## [FIELD:core_theses]\nThese", TestName = "AFieldOfAnotherStepGetsNoSuggestion")]
    [TestCase("## [FIELD:event_link]\nexample.org", TestName = "ALinkWithoutSchemeIsNoSuggestion")]
    [TestCase("## [FIELD:event_link]\njavascript:alert(1)", TestName = "ALinkOfAnotherSchemeIsNoSuggestion")]
    [TestCase("## [FIELD:audience]\nErste Zeile\nZweite Zeile", TestName = "ASingleLineFieldGetsNoSuggestionWithSeveralLines")]
    [TestCase("## [FIELD:audience]\n", TestName = "AnEmptySuggestionIsLeftOut")]
    public void ASuggestionWhichDoesNotFitIsLeftOutWithoutAProblem(string suggestion)
    {
        var form = new BflStepFormData();
        form.Set(BflFieldCatalog.FORMAT, "Podium");

        var answer = BflMarkdownSections.Read(FrameworkWithSuggestions(suggestion), BflStep.FRAMEWORK, form);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answer.FieldSuggestions, Is.Empty);
            Assert.That(answer.Problems, Is.Empty);
        }
    }

    [Test]
    public void ASuggestionLongerThanItsFieldIsLeftOut()
    {
        var answer = BflMarkdownSections.Read(FrameworkWithSuggestions($"## [FIELD:audience]\n{new string('x', BflFieldKind.SINGLE_LINE.MaxLength + 1)}"), BflStep.FRAMEWORK, new BflStepFormData());

        Assert.That(answer.FieldSuggestions, Is.Empty);
    }

    [Test]
    public void TheFirstSuggestionForAFieldCounts()
    {
        var answer = BflMarkdownSections.Read(FrameworkWithSuggestions("## [FIELD:audience]\nErste\n## [FIELD:audience]\nZweite"), BflStep.FRAMEWORK, new BflStepFormData());

        Assert.That(answer.FieldSuggestions[BflFieldCatalog.AUDIENCE], Is.EqualTo("Erste"));
    }

    [Test]
    public void OnlyEmptyTextFieldsWhichAreNotPersonalGetSuggestions()
    {
        var values = BflMarkdownSections.SuggestableFields(BflStep.VALUES_AND_PERSPECTIVES, new BflStepFormData()).Select(field => field.Id);
        var test = BflMarkdownSections.SuggestableFields(BflStep.TEST_SITUATION, new BflStepFormData()).Select(field => field.Id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(values, Is.EqualTo(new[] { BflFieldCatalog.VALUES_AND_CONCERNS, BflFieldCatalog.OTHER_PERSPECTIVES }));
            Assert.That(test, Is.EqualTo(new[] { BflFieldCatalog.TEST_NOTES }));
        }
    }

    private static BflWorkStateAnswer Read(string answer) => BflMarkdownSections.Read(answer, BflStep.GOALS_AND_ROLE, new BflStepFormData());

    private static string FrameworkWithSuggestions(string suggestions) => $"## [FORMAT]\n## [AUDIENCE]\n## [SPEAKING_TIME]\n## [PARTICIPANTS]\n## [CONFLICTS]\n## [OPEN_POINTS]\n{suggestions}";
}