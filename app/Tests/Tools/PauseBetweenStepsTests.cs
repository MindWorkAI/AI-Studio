using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the pause between two indexing steps, which spares an embedding server that many people share.
/// </summary>
/// <remarks>
/// The pause is a gap after every step which worked on documents. It is drawn only once a step is
/// about to start, and the time which passed by then counts toward it.
/// </remarks>
[TestFixture]
public sealed class PauseBetweenStepsTests
{
    private static readonly DateTimeOffset STEP_ENDED = new(2026, 10, 27, 14, 0, 0, TimeSpan.Zero);

    private static readonly PauseRange THIRTY_TO_SIXTY_SECONDS = new(30, 60);

    [Test]
    public void BeforeTheFirstStepNothingWaits()
    {
        var random = new LargestRandom();
        var pause = new PauseBetweenSteps().GetPauseBeforeNextStep(STEP_ENDED, THIRTY_TO_SIXTY_SECONDS, random);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pause, Is.EqualTo(PauseBeforeStep.NONE));
            Assert.That(random.Draws, Is.Zero);
        }
    }

    [Test]
    public void TheNextStepWaitsForTheWholePauseRightAfterAStep()
    {
        var pauses = new PauseBetweenSteps();
        pauses.RecordStepEnded(STEP_ENDED);

        var pause = pauses.GetPauseBeforeNextStep(STEP_ENDED, THIRTY_TO_SIXTY_SECONDS, new LargestRandom());

        Assert.That(pause, Is.EqualTo(new PauseBeforeStep(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60))));
    }

    [Test]
    public void TheUpperBoundCanBeDrawn()
    {
        var pauses = new PauseBetweenSteps();
        pauses.RecordStepEnded(STEP_ENDED);

        var pause = pauses.GetPauseBeforeNextStep(STEP_ENDED, new PauseRange(45, 45), new LargestRandom());

        Assert.That(pause.Pause, Is.EqualTo(TimeSpan.FromSeconds(45)));
    }

    [Test]
    public void TimeWhichPassedSinceTheStepCountsTowardThePause()
    {
        var pauses = new PauseBetweenSteps();
        pauses.RecordStepEnded(STEP_ENDED);

        var pause = pauses.GetPauseBeforeNextStep(STEP_ENDED + TimeSpan.FromSeconds(25), THIRTY_TO_SIXTY_SECONDS, new LargestRandom());

        Assert.That(pause, Is.EqualTo(new PauseBeforeStep(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(35))));
    }

    [Test]
    public void AStepWhichStartsLaterAnywayDoesNotWait()
    {
        var pauses = new PauseBetweenSteps();
        pauses.RecordStepEnded(STEP_ENDED);

        var pause = pauses.GetPauseBeforeNextStep(STEP_ENDED + TimeSpan.FromSeconds(60), THIRTY_TO_SIXTY_SECONDS, new LargestRandom());

        Assert.That(pause, Is.EqualTo(PauseBeforeStep.NONE));
    }

    [Test]
    public void NothingIsDrawnWhileThereIsNoPause()
    {
        var pauses = new PauseBetweenSteps();
        pauses.RecordStepEnded(STEP_ENDED);
        var random = new LargestRandom();

        var withoutPause = pauses.GetPauseBeforeNextStep(STEP_ENDED, PauseRange.NONE, random);
        var withPause = pauses.GetPauseBeforeNextStep(STEP_ENDED + TimeSpan.FromSeconds(10), THIRTY_TO_SIXTY_SECONDS, random);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withoutPause, Is.EqualTo(PauseBeforeStep.NONE));
            Assert.That(withPause, Is.EqualTo(new PauseBeforeStep(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(50))));
            Assert.That(random.Draws, Is.EqualTo(1));
        }
    }

    [Test]
    public void AStepWhichStoppedWhileWaitingLeavesTheRestOfThePauseToTheNextOne()
    {
        var pauses = new PauseBetweenSteps();
        pauses.RecordStepEnded(STEP_ENDED);
        var random = new LargestRandom();

        pauses.GetPauseBeforeNextStep(STEP_ENDED, THIRTY_TO_SIXTY_SECONDS, random);
        var nextPause = pauses.GetPauseBeforeNextStep(STEP_ENDED + TimeSpan.FromSeconds(20), new PauseRange(1, 1), random);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nextPause, Is.EqualTo(new PauseBeforeStep(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(40))));
            Assert.That(random.Draws, Is.EqualTo(1));
        }
    }

    [Test]
    public void ANewStepOpensANewGap()
    {
        var pauses = new PauseBetweenSteps();
        pauses.RecordStepEnded(STEP_ENDED);
        var random = new LargestRandom();
        pauses.GetPauseBeforeNextStep(STEP_ENDED, THIRTY_TO_SIXTY_SECONDS, random);

        var nextStepEnded = STEP_ENDED + TimeSpan.FromMinutes(6);
        pauses.RecordStepEnded(nextStepEnded);
        var pause = pauses.GetPauseBeforeNextStep(nextStepEnded + TimeSpan.FromSeconds(5), new PauseRange(10, 10), random);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pause, Is.EqualTo(new PauseBeforeStep(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5))));
            Assert.That(random.Draws, Is.EqualTo(2));
        }
    }

    /// <summary>
    /// Always draws the largest value it may, and counts how often it was asked.
    /// </summary>
    private sealed class LargestRandom : Random
    {
        public int Draws { get; private set; }

        public override int Next(int minValue, int maxValue)
        {
            this.Draws++;
            return maxValue - 1;
        }
    }
}