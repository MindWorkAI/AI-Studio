using AIStudio.Tools.Media;

namespace AIStudio.Tests.Tools.Media;

/// <summary>
/// Checks the transcripts which the Batch Processing assistant keeps next to its media files.
/// </summary>
[TestFixture]
public sealed class MediaSidecarTranscriptsTests
{
    [TestCase("recording.mp3.transcript.md")]
    [TestCase("recording.mp3.transcript.md.0123456789abcdef.tmp")]
    [TestCase("RECORDING.MP3.TRANSCRIPT.MD")]
    public void GeneratedTranscriptFilesAreRecognized(string fileName)
    {
        Assert.That(MediaSidecarTranscripts.IsArtifactPath(fileName), Is.True);
    }

    [TestCase("notes.md")]
    [TestCase("recording.mp3")]
    [TestCase("recording.mp3.transcript.md.backup")]
    public void OtherFilesAreNoTranscripts(string fileName)
    {
        Assert.That(MediaSidecarTranscripts.IsArtifactPath(fileName), Is.False);
    }

    [Test]
    public async Task TheTranscriptLiesNextToItsMediaFile()
    {
        var mediaPath = Path.Combine(Path.GetTempPath(), "recording.mp3");
        var slot = await MediaSidecarTranscripts.INSTANCE.ResolveAsync(mediaPath, CancellationToken.None);
        Assert.That(slot.Path, Is.EqualTo(mediaPath + MediaSidecarTranscripts.FILE_SUFFIX));
    }

    [Test]
    public async Task OnlyATranscriptWithContentCanBeReused()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-sidecar-transcript-{Guid.NewGuid():N}");
        var mediaPath = Path.Combine(directoryPath, "recording.mp3");
        Directory.CreateDirectory(directoryPath);

        try
        {
            Assert.That(MediaSidecarTranscripts.HasReusableTranscript(mediaPath), Is.False, "A missing transcript counted as reusable.");

            await File.WriteAllTextAsync(MediaSidecarTranscripts.GetFilePath(mediaPath), string.Empty);
            Assert.That(MediaSidecarTranscripts.HasReusableTranscript(mediaPath), Is.False, "An empty transcript counted as reusable.");

            await File.WriteAllTextAsync(MediaSidecarTranscripts.GetFilePath(mediaPath), "Transcript");
            Assert.That(MediaSidecarTranscripts.HasReusableTranscript(mediaPath), Is.True);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }
}