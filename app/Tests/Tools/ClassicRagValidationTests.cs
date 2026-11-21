using AIStudio.AIOperations.RetrievalContextValidation;
using AIStudio.Tools.RAG;
using AIStudio.Tools.RAG.AugmentationProcesses;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which passages the classic RAG process keeps after a model checked which of them fit the user prompt.
/// </summary>
/// <remarks>
/// A passage whose check failed once counted as judged with a confidence of 1.0: it always stayed and moved
/// the threshold of all the passages which were judged. The check of a passage may fail for many reasons,
/// e.g., an answer with broken JSON, and none of them says anything about the passage.<br/><br/>
/// The decision of the model was once ignored as well: only the confidence counted, so a passage judged not to
/// fit with a confidence of 0.9 stayed, and took the room of a passage which fits.
/// </remarks>
[TestFixture]
public sealed class ClassicRagValidationTests
{
    [Test]
    public void NothingToKeepIsNoFailedCheck()
    {
        var validated = AugmentationOne.KeepFittingPassages([]);

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.Empty);
            Assert.That(validated.IsUnchecked, Is.False, "Without a passage, no check failed, and the user needs no warning.");
        });
    }

    [Test]
    public void WhenEveryCheckFailsEveryPassageStays()
    {
        var passages = Passages(4);
        var validated = AugmentationOne.KeepFittingPassages(passages.Select(Failed).ToList());

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.EqualTo(passages), "A failed check says nothing about its passage, so the passage stays.");
            Assert.That(validated.NumUnchecked, Is.EqualTo(4));
            Assert.That(validated.IsUnchecked, Is.True, "The user switched the check on and hears that it failed for every passage.");
        });
    }

    [Test]
    public void FailedChecksDoNotMoveTheThresholdOfTheJudgedPassages()
    {
        //
        // Eight passages fit with little confidence, and two checks failed. When the failed checks counted as
        // judged with 1.0, only those two passages were better than guessing and set the threshold to 0.5: all
        // eight judged passages fell out, and only the two which nobody judged were left.
        //
        var judged = Passages(8);
        var failed = Passages(2);
        var results = judged.Select(passage => Fits(passage, 0.3f)).Concat(failed.Select(Failed)).ToList();

        var validated = AugmentationOne.KeepFittingPassages(results);

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.EqualTo(judged.Concat(failed)), "The judged passages are all alike, so none of them is worse than another.");
            Assert.That(validated.NumJudged, Is.EqualTo(8));
            Assert.That(validated.NumUnchecked, Is.EqualTo(2));
            Assert.That(validated.IsUnchecked, Is.False);
        });
    }

    [Test]
    public void WeakPassagesFallBelowTheThresholdWhileUncheckedOnesStay()
    {
        var passages = Passages(6);
        List<RetrievalContextValidationResult> results =
        [
            Fits(passages[0], 0.2f),
            Fits(passages[1], 0.3f),
            Fits(passages[2], 0.9f),
            Failed(passages[3]),
            Fits(passages[4], 0.95f),
            Fits(passages[5], 1.0f),
        ];

        var validated = AugmentationOne.KeepFittingPassages(results);

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.EqualTo(new[] { passages[2], passages[3], passages[4], passages[5] }), "The three strong passages stay, and so does the unchecked one, in the order of the results.");
            Assert.That(validated.NumBelowThreshold, Is.EqualTo(2));
            Assert.That(validated.Threshold, Is.EqualTo(0.5f).Within(0.001f));
        });
    }

    [Test]
    public void APassageJudgedNotToFitIsLeftOutHoweverConfident()
    {
        var passages = Passages(3);
        var validated = AugmentationOne.KeepFittingPassages([NotFitting(passages[0], 0.95f), Fits(passages[1], 0.6f), Fits(passages[2], 0.7f)]);

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.EqualTo(new[] { passages[1], passages[2] }), "The confidence says how sure the model is of its decision, not how well the passage fits.");
            Assert.That(validated.NumRejected, Is.EqualTo(1));
        });
    }

    [Test]
    public void WhenEveryPassageIsJudgedNotToFitNoneStays()
    {
        var passages = Passages(10);
        var validated = AugmentationOne.KeepFittingPassages(passages.Select(passage => NotFitting(passage, 0.9f)).ToList());

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.Empty, "The model answers as when the search found nothing.");
            Assert.That(validated.NumRejected, Is.EqualTo(10));
            Assert.That(validated.IsUnchecked, Is.False, "Every check worked, so the user needs no warning.");
        });
    }

    [Test]
    public void PassagesJudgedNotToFitDoNotMoveTheThresholdOfTheFittingOnes()
    {
        //
        // Eight passages fit with little confidence, and two do not fit with much. When only the confidence
        // counted, the two were the only ones better than guessing and set the threshold to 0.5: the eight which
        // fit fell out, and only the two which do not fit were left.
        //
        var fitting = Passages(8);
        var notFitting = Passages(2);
        var results = fitting.Select(passage => Fits(passage, 0.3f)).Concat(notFitting.Select(passage => NotFitting(passage, 0.9f))).ToList();

        var validated = AugmentationOne.KeepFittingPassages(results);

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.EqualTo(fitting));
            Assert.That(validated.NumRejected, Is.EqualTo(2));
            Assert.That(validated.NumBelowThreshold, Is.Zero);
        });
    }

    [Test]
    public void AMixOfJudgmentsAndFailedChecksKeepsTheStrongFittingAndTheUncheckedPassages()
    {
        var passages = Passages(7);
        List<RetrievalContextValidationResult> results =
        [
            Fits(passages[0], 0.2f),
            NotFitting(passages[1], 0.9f),
            Fits(passages[2], 0.9f),
            Failed(passages[3]),
            Fits(passages[4], 0.95f),
            NotFitting(passages[5], 0.6f),
            Fits(passages[6], 1.0f),
        ];

        var validated = AugmentationOne.KeepFittingPassages(results);

        Assert.Multiple(() =>
        {
            Assert.That(validated.Contexts, Is.EqualTo(new[] { passages[2], passages[3], passages[4], passages[6] }));
            Assert.That(validated.NumJudged, Is.EqualTo(6));
            Assert.That(validated.NumUnchecked, Is.EqualTo(1));
            Assert.That(validated.NumRejected, Is.EqualTo(2));
            Assert.That(validated.NumBelowThreshold, Is.EqualTo(1));
            Assert.That(validated.IsUnchecked, Is.False);
        });
    }

    /// <summary>
    /// Passages which differ from each other, so that a test can tell which of them were left.
    /// </summary>
    private static List<PreparedRetrievalContext> Passages(int count) => Enumerable.Range(1, count).Select(number => new PreparedRetrievalContext(new RetrievalTextContext
    {
        DataSourceName = "Handbook",
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_DOCUMENT,
        Path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"handbook-{number}.pdf")),
        MatchedText = $"Passage {number} of the handbook.",
    }, $"body {number}", [], null)).ToList();

    private static RetrievalContextValidationResult Fits(PreparedRetrievalContext passage, float confidence) => new(passage, true, true, "It fits.", confidence);

    private static RetrievalContextValidationResult NotFitting(PreparedRetrievalContext passage, float confidence) => new(passage, true, false, "It is about something else.", confidence);

    private static RetrievalContextValidationResult Failed(PreparedRetrievalContext passage) => RetrievalContextValidationResult.NotAnswered(passage, "The AI did not return a response.");
}