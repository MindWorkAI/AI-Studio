using AIStudio.Tools;
using AIStudio.Tools.Media;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

public sealed class PersistentMediaTranscriptTests
{
    [Test]
    public async Task UnchangedIndexedMediaDoesNotNeedItsTranscriptOrAProviderAgain()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-indexed-media-{Guid.NewGuid():N}");
        var mediaPath = Path.Combine(directoryPath, "recording.mp3");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(mediaPath, [1, 2, 3]);
            var file = new FileInfo(mediaPath);
            var manifest = new DataSourceEmbeddingManifest();
            manifest.Files[file.FullName] = new EmbeddedFileRecord("CURRENT", file.Length, new DateTimeOffset(file.LastWriteTimeUtc), DateTimeOffset.UtcNow, 1);

            Assert.Multiple(() =>
            {
                Assert.That(FileSourceIndexer.CanIndexMediaWithoutTranscriptionProvider(file, "CURRENT", manifest), Is.True);
                Assert.That(FileSourceIndexer.CanIndexMediaWithoutTranscriptionProvider(file, "CHANGED", manifest), Is.False);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task CurrentTranscriptBecomesStaleWhenTheMediaChanges()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-media-transcript-{Guid.NewGuid():N}");
        var mediaPath = Path.Combine(directoryPath, "recording.mp3");
        var transcriptPath = PersistentMediaTranscript.GetFilePath(mediaPath);
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(mediaPath, [1, 2, 3]);
            await File.WriteAllTextAsync(transcriptPath, "Transcript");
            var sourceTime = DateTime.UtcNow.AddMinutes(-2);
            File.SetLastWriteTimeUtc(mediaPath, sourceTime);
            File.SetLastWriteTimeUtc(transcriptPath, sourceTime);

            Assert.That(PersistentMediaTranscript.IsReusable(mediaPath, PersistentMediaTranscript.Reuse.CURRENT_SOURCE), Is.True);

            File.SetLastWriteTimeUtc(transcriptPath, sourceTime.AddSeconds(1));
            Assert.That(PersistentMediaTranscript.IsReusable(mediaPath, PersistentMediaTranscript.Reuse.CURRENT_SOURCE), Is.False);
            File.SetLastWriteTimeUtc(transcriptPath, sourceTime);

            await File.WriteAllBytesAsync(mediaPath, [1, 2, 3, 4]);
            File.SetLastWriteTimeUtc(mediaPath, sourceTime.AddMinutes(1));

            Assert.Multiple(() =>
            {
                Assert.That(PersistentMediaTranscript.IsReusable(mediaPath, PersistentMediaTranscript.Reuse.CURRENT_SOURCE), Is.False);
                Assert.That(PersistentMediaTranscript.IsReusable(mediaPath, PersistentMediaTranscript.Reuse.ANY), Is.True);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [TestCase("recording.mp3.transcript.md")]
    [TestCase("recording.mp3.transcript.md.0123456789abcdef.tmp")]
    public void GeneratedTranscriptFilesAreRecognized(string fileName)
    {
        Assert.That(PersistentMediaTranscript.IsArtifactPath(fileName), Is.True);
    }

    [Test]
    public async Task TranscriptIsCoveredOnlyWhenItsMediaFileMatchesThePatterns()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-covered-transcript-{Guid.NewGuid():N}");
        var mediaPath = Path.Combine(directoryPath, "recording.mp3");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(mediaPath, [1, 2, 3]);
            var transcriptPath = PersistentMediaTranscript.GetFilePath(mediaPath);

            Assert.Multiple(() =>
            {
                Assert.That(PersistentMediaTranscript.IsCoveredArtifact(transcriptPath, FileNamePatterns.Parse("*")), Is.True);
                Assert.That(PersistentMediaTranscript.IsCoveredArtifact(transcriptPath, FileNamePatterns.Parse("*.mp3")), Is.True);
                Assert.That(PersistentMediaTranscript.IsCoveredArtifact(transcriptPath, FileNamePatterns.Parse("*.md")), Is.False);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }
}
