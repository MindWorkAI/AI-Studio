using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that determining a confidence threshold survives a list with nothing in it.
/// </summary>
/// <remarks>
/// GetConfidenceThreshold reaches for Min and Max, both of which throw on an empty sequence. Until
/// v26.9.1 the only thing standing between them and that exception was a count check at each of the
/// two call sites. The RAG process already lost such a guard once -- a log line was written before
/// the check that was meant to protect it, which is how every new chat produced an
/// InvalidOperationException nobody noticed, because the RAG process catches and logs everything.
/// The threshold is now asked to defend itself, and these tests hold it to that.
/// </remarks>
[TestFixture]
public sealed class ConfidenceThresholdTests
{
    private readonly record struct Decision(float Confidence) : IConfidence;

    [Test]
    public void AnEmptyListYieldsAThresholdInsteadOfThrowing()
    {
        IReadOnlyList<Decision> nothingWasDecided = [];
        var targetWindow = new TargetWindow(1, 2, 3, 0f);

        Assert.That(nothingWasDecided.GetConfidenceThreshold(targetWindow), Is.EqualTo(0f), "A threshold of zero keeps everything that follows, which is the harmless answer for a list that holds nothing to filter.");
    }

    [Test]
    public void AnEmptyListIsAnsweredEvenWhenTheWindowDemandsItems()
    {
        //
        // The window asks for between five and ten items while not a single one exists. The guard
        // has to hold regardless of what the window wants:
        //
        IReadOnlyList<Decision> nothingWasDecided = [];
        var demandingWindow = new TargetWindow(4, 5, 10, 0.5f);

        Assert.That(nothingWasDecided.GetConfidenceThreshold(demandingWindow), Is.EqualTo(0f), "The number of items the window asks for cannot conjure items to measure.");
    }

    [Test]
    public void ASpreadOfDecisionsIsNarrowedToTheTargetWindow()
    {
        //
        // Guarding the empty case must not change what the threshold does with actual items. Two
        // weak decisions and three strong ones, with a window asking for two to three:
        //
        IReadOnlyList<Decision> decisions = [new(0.1f), new(0.2f), new(0.9f), new(0.95f), new(1.0f)];
        var targetWindow = new TargetWindow(1, 2, 3, 0f);

        var threshold = decisions.GetConfidenceThreshold(targetWindow);
        var survivors = decisions.Count(decision => decision.Confidence >= threshold);

        Assert.That(survivors, Is.InRange(targetWindow.TargetWindowMin, targetWindow.TargetWindowMax), "The threshold exists to cut a list down to the size the window asks for, and the three strong decisions are what should be left.");
    }
}