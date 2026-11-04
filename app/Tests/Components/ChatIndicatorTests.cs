using AIStudio.Components;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.Media;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks what the icon of a chat in the workspace list shows for the states of its transcript and its
/// answer, and for an answer whose outcome the user has not seen yet.
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
    public void EveryUnseenAnswerStateHasAnEntry()
    {
        var missing = Enum.GetValues<AIJobStatus>().Where(state => !ChatIndicator.UNSEEN_ANSWER_STATES.ContainsKey(state));
        Assert.That(missing, Is.Empty, "Add an entry to ChatIndicator.UNSEEN_ANSWER_STATES for every new state of an answer.");
    }

    [Test]
    public void AnAnswerShowsOneIconInAllOfItsSteps([Values] bool isAnswerOutcomeSeen)
    {
        var indicators = new[] { AIJobStatus.QUEUED, AIJobStatus.WAITING_FOR_REMOTE, AIJobStatus.RUNNING }
            .Select(state => ChatIndicator.Of(transcriptState: null, state, isAnswerOutcomeSeen))
            .Distinct()
            .ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(indicators, Has.Count.EqualTo(1));
            Assert.That(indicators[0].IsSpinning, Is.True);
        }
    }

    [Test]
    public void AnAnswerWrittenInTheBackgroundShowsAsUnread()
    {
        var indicator = ChatIndicator.Of(transcriptState: null, AIJobStatus.COMPLETED, isAnswerOutcomeSeen: false);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.ANSWER_UNREAD));
    }

    [Test]
    public void AnAnswerFailedInTheBackgroundShowsTheFailure()
    {
        var indicator = ChatIndicator.Of(transcriptState: null, AIJobStatus.FAILED, isAnswerOutcomeSeen: false);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.ANSWER_FAILED));
    }

    [Test]
    public void AnAnswerCanceledInTheBackgroundShowsThePlainIcon()
    {
        var indicator = ChatIndicator.Of(transcriptState: null, AIJobStatus.CANCELED, isAnswerOutcomeSeen: false);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.PLAIN));
    }

    [TestCase(AIJobStatus.COMPLETED)]
    [TestCase(AIJobStatus.CANCELED)]
    [TestCase(AIJobStatus.FAILED)]
    public void AnAnswerSeenOnceShowsThePlainIcon(AIJobStatus answerState)
    {
        var indicator = ChatIndicator.Of(transcriptState: null, answerState, isAnswerOutcomeSeen: true);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.PLAIN));
    }

    [Test]
    public void AnUnseenAnswerComesBeforeHowTheLastTranscriptEnded()
    {
        var indicator = ChatIndicator.Of(MediaImportStatus.SUCCEEDED, AIJobStatus.FAILED, isAnswerOutcomeSeen: false);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.ANSWER_FAILED));
    }

    [Test]
    public void ATranscriptBeingMadeComesBeforeAnUnseenAnswer()
    {
        var indicator = ChatIndicator.Of(MediaImportStatus.RUNNING, AIJobStatus.COMPLETED, isAnswerOutcomeSeen: false);
        Assert.That(indicator, Is.EqualTo(ChatIndicator.TRANSCRIPT_STATES[MediaImportStatus.RUNNING]));
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