using AIStudio.Tools.Media;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Media;

/// <summary>
/// Checks that the transcript of a recording leaves the data source together with the recording.
/// </summary>
/// <remarks>
/// A transcript holds what was said. Once its recording is gone from the data source, keeping it
/// below the data directory would keep that text, unseen by the user.
/// </remarks>
[TestFixture]
public sealed class TranscriptCleanupTests
{
    private static readonly string KEY_A = new('a', 64);

    private static readonly string KEY_B = new('b', 64);

    private static readonly string KEY_C = new('c', 64);

    private string dataDirectory = string.Empty;

    private DataSourceTranscriptStore store = null!;

    [SetUp]
    public async Task CreateTranscripts()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-transcript-cleanup-{Guid.NewGuid():N}");
        this.store = new DataSourceTranscriptStore(this.dataDirectory, Guid.NewGuid().ToString());
        foreach (var key in new[] { KEY_A, KEY_B, KEY_C })
        {
            var path = this.store.GetPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, $"Transcript {key[0]}");
        }
    }

    [TearDown]
    public void DeleteTranscripts()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public void TheTranscriptOfARemovedRecordingIsDeleted()
    {
        var manifest = Manifest(("/data/kept.mp3", KEY_A));

        FileSourceIndexer.ForgetTranscript(this.store, manifest, KEY_B, NullLogger.Instance);

        Assert.That(this.store.ListKeys(), Is.EquivalentTo(new[] { KEY_A, KEY_C }));
    }

    [Test]
    public void ATranscriptWhichAnotherRecordingSharesIsKept()
    {
        var manifest = Manifest(("/data/copy-of-the-removed-recording.mp3", KEY_B));

        FileSourceIndexer.ForgetTranscript(this.store, manifest, KEY_B, NullLogger.Instance);

        Assert.That(this.store.ListKeys(), Does.Contain(KEY_B), "Two recordings with the same content share one transcript.");
    }

    [Test]
    public void AFileWithoutTranscriptDeletesNothing()
    {
        FileSourceIndexer.ForgetTranscript(this.store, new DataSourceEmbeddingManifest(), null, NullLogger.Instance);
        Assert.That(this.store.ListKeys(), Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ACompleteRunDeletesWhatNoFileUses()
    {
        var temporaryPath = $"{this.store.GetPath(KEY_A)}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, "Interrupted");
        var manifest = Manifest(("/data/meeting.mp3", KEY_A), ("/data/report.pdf", null));

        FileSourceIndexer.DeleteUnusedTranscripts(this.store, manifest, NullLogger.Instance);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(this.store.ListKeys(), Is.EqualTo(new[] { KEY_A }));
            Assert.That(File.Exists(temporaryPath), Is.False, "A temporary file of an interrupted write is no transcript of anything.");
        }
    }

    [Test]
    public void WithoutAStoreNothingHappens()
    {
        Assert.DoesNotThrow(() =>
        {
            FileSourceIndexer.DeleteUnusedTranscripts(null, new DataSourceEmbeddingManifest(), NullLogger.Instance);
            FileSourceIndexer.ForgetTranscript(null, new DataSourceEmbeddingManifest(), KEY_A, NullLogger.Instance);
        });
    }

    private static DataSourceEmbeddingManifest Manifest(params (string Path, string? TranscriptKey)[] files)
    {
        var manifest = new DataSourceEmbeddingManifest();
        foreach (var (path, transcriptKey) in files)
            manifest.Files[path] = new EmbeddedFileRecord("fingerprint", 16, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, transcriptKey);

        return manifest;
    }
}