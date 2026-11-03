using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Media;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks combinations of media, transcripts, and ordinary Markdown against the current folder
/// selection rules. Transcript reuse exercises the shared implementation used by RAG and batch.
/// </summary>
public sealed class MediaFileSelectionTests
{
    private const string TRANSCRIPT_TEXT = "# Transcript\nSpoken content from the recording.";

    private string rootPath = string.Empty;
    private string userFolder = string.Empty;

    [SetUp]
    public void CreateDirectory()
    {
        this.rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-media-selection-{Guid.NewGuid():N}");
        this.userFolder = Path.Combine(this.rootPath, "user");
        Directory.CreateDirectory(this.userFolder);
    }

    [TearDown]
    public void DeleteDirectory() => Directory.Delete(this.rootPath, true);

    [TestCase("*.wav", "*.md")]
    [TestCase("*.wav;*.md", "*.md")]
    [TestCase("*", "*.md")]
    public async Task ExcludingMarkdownStillSelectsTheMedia(string includes, string excludes)
    {
        var mediaPath = await this.CreateMediaAndDocumentsAsync();

        Assert.That(this.Discover(includes, excludes), Is.EqualTo(new[] { mediaPath }));
    }

    [TestCase("*.md")]
    [TestCase("*.wav;*.md")]
    [TestCase("*")]
    public async Task ExcludingMediaKeepsOrdinaryMarkdownAndTheIndependentTranscript(string includes)
    {
        var mediaPath = await this.CreateMediaAndDocumentsAsync();

        Assert.That(this.Discover(includes, "*.wav"), Is.EquivalentTo(new[]
        {
            MediaSidecarTranscripts.GetFilePath(mediaPath),
            Path.Combine(this.userFolder, "notes.md"),
            Path.Combine(this.userFolder, "readme.md"),
        }));
    }

    [Test]
    public async Task SelectingMediaAndMarkdownDoesNotProcessTheTranscriptTwice()
    {
        var mediaPath = await this.CreateMediaAndDocumentsAsync();

        Assert.That(this.Discover("*.wav;*.md", string.Empty), Is.EquivalentTo(new[]
        {
            mediaPath,
            Path.Combine(this.userFolder, "notes.md"),
            Path.Combine(this.userFolder, "readme.md"),
        }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExistingTranscriptsAreReusedWithoutAProviderOrARewrite(bool dataSourceTranscript)
    {
        var mediaPath = await this.CreateMediaAndDocumentsAsync();
        IMediaTranscriptLocation location = dataSourceTranscript
            ? this.TranscriptStore()
            : MediaSidecarTranscripts.INSTANCE;
        var slot = await location.ResolveAsync(mediaPath, CancellationToken.None);
        Directory.CreateDirectory(Path.GetDirectoryName(slot.Path)!);
        await File.WriteAllTextAsync(slot.Path, TRANSCRIPT_TEXT);
        var originalWriteTime = File.GetLastWriteTimeUtc(slot.Path);
        var calls = 0;
        Task<MediaTranscriptionResult> Transcribe(string _, TranscriptionProvider __, CancellationToken ___)
        {
            calls++;
            throw new InvalidOperationException("An existing transcript must not ask a provider.");
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = await PersistentMediaTranscript.ReadOrCreateAsync(
                location, mediaPath, null, Transcribe, NullLogger.Instance, CancellationToken.None);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
                Assert.That(result.Reused, Is.True);
                Assert.That(File.ReadAllText(result.TranscriptPath), Is.EqualTo(TRANSCRIPT_TEXT));
                Assert.That(File.GetLastWriteTimeUtc(slot.Path), Is.EqualTo(originalWriteTime));
                Assert.That(calls, Is.Zero);
            }
        }
    }

    [Test]
    public async Task ChangedMediaDoesNotReuseAnOldDataSourceTranscriptEvenWithTheSameSizeAndWriteTime()
    {
        var mediaPath = await this.CreateMediaAndDocumentsAsync();
        var store = this.TranscriptStore();
        var originalSlot = await store.ResolveAsync(mediaPath, CancellationToken.None);
        Directory.CreateDirectory(Path.GetDirectoryName(originalSlot.Path)!);
        await File.WriteAllTextAsync(originalSlot.Path, TRANSCRIPT_TEXT);
        var originalWriteTime = File.GetLastWriteTimeUtc(mediaPath);

        await File.WriteAllBytesAsync(mediaPath, [4, 5, 6]);
        File.SetLastWriteTimeUtc(mediaPath, originalWriteTime);
        var changedSlot = await store.ResolveAsync(mediaPath, CancellationToken.None);
        var result = await PersistentMediaTranscript.ReadOrCreateAsync(
            store, mediaPath, null,
            (_, _, _) => throw new InvalidOperationException("No provider was configured."),
            NullLogger.Instance, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changedSlot.Key, Is.Not.EqualTo(originalSlot.Key));
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.NO_PROVIDER));
            Assert.That(File.ReadAllText(originalSlot.Path), Is.EqualTo(TRANSCRIPT_TEXT));
        }
    }

    [TestCase("*.wav", "*.md", false, false)]
    [TestCase("*.md", "*.wav", true, true)]
    [TestCase("*.wav;*.md", "", true, false)]
    public async Task WatcherUsesTheSelectionOfTheMediaWhenDecidingAboutItsSidecar(
        string includes, string excludes, bool markdownChangesMatter, bool transcriptChangesMatter)
    {
        var mediaPath = await this.CreateMediaAndDocumentsAsync();
        var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
            this.userFolder,
            false,
            FilePatterns: FileNamePatterns.Parse(includes),
            ExcludePatterns: FileExclusions.Parse(excludes));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                FileSourceIndexer.IsRelevantWatcherPath(configuration, Path.Combine(this.userFolder, "notes.md"), WatcherChangeTypes.Changed),
                Is.EqualTo(markdownChangesMatter));
            Assert.That(
                FileSourceIndexer.IsRelevantWatcherPath(configuration, MediaSidecarTranscripts.GetFilePath(mediaPath), WatcherChangeTypes.Changed),
                Is.EqualTo(transcriptChangesMatter));
        }
    }

    private async Task<string> CreateMediaAndDocumentsAsync()
    {
        var mediaPath = Path.Combine(this.userFolder, "test123.wav");
        // These bytes cannot be decoded: reuse must find the transcript before asking a provider.
        await File.WriteAllBytesAsync(mediaPath, [1, 2, 3]);
        await File.WriteAllTextAsync(MediaSidecarTranscripts.GetFilePath(mediaPath), TRANSCRIPT_TEXT);
        await File.WriteAllTextAsync(Path.Combine(this.userFolder, "notes.md"), "# Ordinary notes");
        await File.WriteAllTextAsync(Path.Combine(this.userFolder, "readme.md"), "# Ordinary readme");
        return mediaPath;
    }

    private IReadOnlyList<string> Discover(string includes, string excludes)
    {
        using var indexer = new FileSourceIndexer(null!, null!, null!, null!, NullLogger.Instance);
        var result = indexer.GetInputFiles(new DataSourceLocalDirectory
        {
            Type = DataSourceType.LOCAL_DIRECTORY,
            Path = this.userFolder,
            FilePatterns = includes,
            ExcludePatterns = excludes,
            IncludeSubdirectories = false,
        }, new DataSourceEmbeddingManifest(),
            MediaTranscriptionAvailability.Allowed(new TranscriptionProvider { Id = "test-transcription-provider" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failures, Is.Empty);
            Assert.That(result.IgnoredFiles, Is.Empty);
        }

        return result.Files.Select(file => file.FullName).ToArray();
    }

    private DataSourceTranscriptStore TranscriptStore() => new(
        Path.Combine(this.rootPath, "data"), "d401ce54-9a42-4e78-85de-2402e07f7f7e");
}
