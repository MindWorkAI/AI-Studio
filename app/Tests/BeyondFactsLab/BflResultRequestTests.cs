using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the requests which let the model write the results of the BeyondFacts Lab, and how the lab reads what the
/// model wrote.
/// </summary>
[TestFixture]
public sealed class BflResultRequestTests
{
    private static readonly DateTimeOffset NOW = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private static IEnumerable<BflResultKind> WrittenByModel => BflResultKind.All.Where(kind => kind.IsWrittenByModel);

    [Test]
    public async Task TheRequestOfEveryResultNamesTheStepsWithoutAConfirmedWorkState()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        workStates[BflStep.FRAMEWORK] = new BflWorkState();
        workStates[BflStep.FRAMEWORK].Confirm(new Dictionary<string, string> { ["FORMAT"] = "Podium" }, new Dictionary<BflStep, int>(), string.Empty, NOW);

        foreach (var kind in WrittenByModel)
        {
            var request = BflPromptComposer.ComposeResultRequest(await BflPromptLibrary.ReadAsync(BflPromptLibrary.ResultPath(kind)), workStates);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(request, Does.Not.Contain("{{"), kind.ToString());
                Assert.That(request, Does.Not.Contain($"„{BflStep.FRAMEWORK.Title}“"), kind.ToString());
                foreach (var step in BflStep.All.Where(step => step is not BflStep.FRAMEWORK))
                    Assert.That(request, Does.Contain($"„{step.Title}“"), $"{kind}: {step}");
            }
        }
    }

    [Test]
    public void ARequestSaysWhenEveryStepIsConfirmed()
    {
        var workStates = new Dictionary<BflStep, BflWorkState>();
        foreach (var step in BflStep.All)
        {
            workStates[step] = new BflWorkState();
            workStates[step].Confirm(new Dictionary<string, string> { ["OPEN_POINTS"] = "Punkt" }, new Dictionary<BflStep, int>(), string.Empty, NOW);
        }

        Assert.That(BflPromptComposer.ComposeResultRequest("{{MISSING_WORK_STATES}}", workStates), Is.EqualTo("keine; alle Schritte sind bestätigt."));
    }

    [Test]
    public void ADraftOfAWorkStateCountsAsMissing()
    {
        var workStates = new Dictionary<BflStep, BflWorkState> { [BflStep.STRATEGY] = new() { Draft = new(StringComparer.Ordinal) { ["OPEN_POINTS"] = "Entwurf" } } };

        Assert.That(BflPromptComposer.ComposeResultRequest("{{MISSING_WORK_STATES}}", workStates), Does.Contain($"„{BflStep.STRATEGY.Title}“"));
    }

    [Test]
    public void EveryResultOfAModelHasARequestTheTestFolderMayReplace()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var kind in WrittenByModel)
                Assert.That(BflPromptLibrary.AllPaths, Does.Contain(BflPromptLibrary.ResultPath(kind)), kind.ToString());

            Assert.That(WrittenByModel.Select(BflPromptLibrary.ResultPath), Is.Unique);
            Assert.Throws<ArgumentOutOfRangeException>(() => BflPromptLibrary.ResultPath(BflResultKind.REFLECTION_FOLDER));
        }
    }

    [TestCase("# Positionsblatt\n\nText", "# Positionsblatt\n\nText", TestName = "AResultInPlainMarkdownStaysAsItIs")]
    [TestCase("```markdown\n# Positionsblatt\n\nText\n```", "# Positionsblatt\n\nText", TestName = "ACodeBlockAroundTheWholeResultIsTakenOff")]
    [TestCase("~~~\n# Leitfaden\n~~~", "# Leitfaden", TestName = "ACodeBlockWithTildesAroundTheWholeResultIsTakenOff")]
    [TestCase("```\n# Fragen\n```\n\nText\n\n```\nCode\n```", "```\n# Fragen\n```\n\nText\n\n```\nCode\n```", TestName = "SeveralCodeBlocksStayAsTheyAre")]
    [TestCase("  # Liste\r\n\r\n- [ ] Punkt  \r\n", "# Liste\n\n- [ ] Punkt", TestName = "LineEndingsAndSpacesAroundTheResultAreTidied")]
    [TestCase(" \n ", "", TestName = "AnAnswerWithoutTextGivesNoResult")]
    public void TheLabReadsTheResultFromTheAnswer(string answer, string expected)
    {
        Assert.That(BflResultText.Read(answer), Is.EqualTo(expected));
    }
}