using AIStudio.Components;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Media;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks what the icon of a chat in the workspace list shows for the states of its transcript and its answer.
/// </summary>
/// <remarks>
/// A state without an entry in the tables of <see cref="ChatIndicator"/> falls back to the plain chat
/// icon, and nobody would notice that the work going on in a chat no longer shows.
/// </remarks>
[TestFixture]
public sealed class ChatIndicatorTests
{
    [Test]
    public void EveryTranscriptStateHasAnEntry()
    {
        var missing = Enum.GetValues<MediaImportStatus>().Where(state => !ChatIndicator.TRANSCRIPT_STATES.ContainsKey(state));
        Assert.That(missing, Is.Empty, "Add an entry to ChatIndicator.TRANSCRIPT_STATES for every new state of a transcript.");
    }

    [Test]
    public void EveryAnswerStateHasAnEntry()
    {
        var missing = Enum.GetValues<AIJobStatus>().Where(state => !ChatIndicator.ANSWER_STATES.ContainsKey(state));
        Assert.That(missing, Is.Empty, "Add an entry to ChatIndicator.ANSWER_STATES for every new state of an answer.");
    }

    [Test]
    public void AnAnswerShowsOneIconInAllOfItsSteps()
    {
        var indicators = new[] { AIJobStatus.QUEUED, AIJobStatus.WAITING_FOR_REMOTE, AIJobStatus.RUNNING }
            .Select(state => ChatIndicator.Of(transcriptState: null, state))
            .Distinct()
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(indicators, Has.Count.EqualTo(1));
            Assert.That(indicators[0].IsSpinning, Is.True);
        });
    }

    [Test]
    public void AnAnswerBeingWrittenComesBeforeHowTheLastTranscriptEnded()
    {
        var indicator = ChatIndicator.Of(MediaImportStatus.SUCCEEDED, AIJobStatus.RUNNING);
        Assert.That(indicator.IsSpinning, Is.True);
    }

    [Test]
    public void HowATranscriptEndedStaysOnceTheAnswerIsDone()
    {
        var indicator = ChatIndicator.Of(MediaImportStatus.FAILED, AIJobStatus.COMPLETED);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.TRANSCRIPT_STATES[MediaImportStatus.FAILED]));
    }

    [Test]
    public void AChatWithoutAnyWorkShowsThePlainIcon()
    {
        var indicator = ChatIndicator.Of(transcriptState: null, answerState: null);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.PLAIN));
    }
}