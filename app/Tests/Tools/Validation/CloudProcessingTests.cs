using AIStudio.Tools;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks what the dialogs of local data sources tell about the cloud, and when they ask again.
/// </summary>
/// <remarks>
/// A data source can send its text to an embedding provider and its recordings to a transcription
/// provider. The user acknowledges what leaves the computer, and an acknowledgment for the one must
/// not count for the other.
/// </remarks>
[TestFixture]
public sealed class CloudProcessingTests
{
    [TestCase(CloudProcessing.EMBEDDING, CloudProcessing.EMBEDDING, true)]
    [TestCase(CloudProcessing.EMBEDDING, CloudProcessing.EMBEDDING | CloudProcessing.TRANSCRIPTION, false)]
    [TestCase(CloudProcessing.EMBEDDING, CloudProcessing.TRANSCRIPTION, false)]
    [TestCase(CloudProcessing.EMBEDDING | CloudProcessing.TRANSCRIPTION, CloudProcessing.TRANSCRIPTION, true)]
    [TestCase(CloudProcessing.NONE, CloudProcessing.EMBEDDING, false)]
    [TestCase(CloudProcessing.NONE, CloudProcessing.NONE, true)]
    public void AnAcknowledgmentCoversOnlyWhatItWasGivenFor(CloudProcessing acknowledged, CloudProcessing current, bool expected)
    {
        Assert.That(acknowledged.Covers(current), Is.EqualTo(expected));
    }

    [Test]
    public void ARecordingSentToTheCloudNeedsAnAcknowledgment()
    {
        var validation = new DataSourceValidation { GetSelectedCloudTranscription = () => true };

        Assert.Multiple(() =>
        {
            Assert.That(validation.ValidateUserAcknowledgedCloudEmbedding(false), Is.Not.Null);
            Assert.That(validation.ValidateUserAcknowledgedCloudEmbedding(true), Is.Null);
        });
    }

    [Test]
    public void NothingSentToTheCloudNeedsNoAcknowledgment()
    {
        Assert.That(new DataSourceValidation().ValidateUserAcknowledgedCloudEmbedding(false), Is.Null);
    }

    [TestCase("*")]
    [TestCase("*.mp3")]
    [TestCase("*.MP4")]
    [TestCase("meeting*")]
    [TestCase("*.*")]
    [TestCase("*.m?a")]
    [TestCase(":documents:;*.webm")]
    public void APatternWhichMaySelectRecordingsCounts(string patterns)
    {
        Assert.That(FileNamePatterns.MaySelectMedia(FileNamePatterns.Parse(patterns)), Is.True);
    }

    [TestCase(":documents:")]
    [TestCase(":text:")]
    [TestCase("*.pdf;*.docx")]
    [TestCase(".gitignore")]
    [TestCase("Makefile")]
    [TestCase("report_*.txt")]
    public void APatternWhichSelectsNoRecordingDoesNotCount(string patterns)
    {
        Assert.That(FileNamePatterns.MaySelectMedia(FileNamePatterns.Parse(patterns)), Is.False, "The dialog would warn about a transcription which never happens.");
    }
}