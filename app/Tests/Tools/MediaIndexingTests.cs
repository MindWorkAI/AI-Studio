using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks which audio and video files a local data source transcribes, and how it treats those it cannot.
/// </summary>
/// <remarks>
/// Transcribing costs money with every cloud provider. A media file is therefore only sent when a
/// provider exists which the data source allows, and a file which cannot be transcribed for a
/// reason of its own is not sent again until it changes.
/// </remarks>
public sealed class MediaIndexingTests
{
    private string testRoot = string.Empty;

    [SetUp]
    public void CreateTestRoot()
    {
        this.testRoot = Path.Combine(Path.GetTempPath(), $"ai-studio-media-indexing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testRoot);
    }

    [TearDown]
    public void DeleteTestRoot()
    {
        if (Directory.Exists(this.testRoot))
            Directory.Delete(this.testRoot, true);
    }

    [Test]
    public void AnAllowedProviderTranscribesASelectedRecording()
    {
        var recording = this.CreateFile("meeting.mp3", 16);

        var result = CreateIndexer().GetInputFiles(this.Folder("*"), new DataSourceEmbeddingManifest(), Allowed());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Files.Select(file => file.FullName), Is.EqualTo(new[] { recording }));
            Assert.That(result.IgnoredFiles, Is.Empty);
        }
    }

    [Test]
    public void WithoutAProviderANewRecordingWaitsForOne()
    {
        var recording = this.CreateFile("meeting.mp3", 16);

        var result = CreateIndexer().GetInputFiles(this.Folder("*"), new DataSourceEmbeddingManifest(), MediaTranscriptionAvailability.MISSING);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Files, Is.Empty);
            Assert.That(result.IgnoredFiles, Is.EqualTo(new[] { new DataSourceEmbeddingIgnoredFile(recording, DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_MISSING) }));
        }
    }

    [Test]
    public void AProviderBelowTheConfidenceLevelHearsNothing()
    {
        var recording = this.CreateFile("meeting.mp4", 16);

        var result = CreateIndexer().GetInputFiles(this.Folder("*"), new DataSourceEmbeddingManifest(), MediaTranscriptionAvailability.NOT_ALLOWED);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Files, Is.Empty, "The recording would have gone to a provider the data source does not allow.");
            Assert.That(result.IgnoredFiles, Is.EqualTo(new[] { new DataSourceEmbeddingIgnoredFile(recording, DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_NOT_ALLOWED) }));
        }
    }

    [Test]
    public void AnIndexedRecordingStaysWhenTheProviderIsGone()
    {
        var recording = this.CreateFile("meeting.mp3", 16);
        var manifest = new DataSourceEmbeddingManifest();
        manifest.Files[recording] = new EmbeddedFileRecord(FileSourceIndexer.BuildFileMetadataHash(new FileInfo(recording)), 16, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 3, new string('a', 64));

        var result = CreateIndexer().GetInputFiles(this.Folder("*"), manifest, MediaTranscriptionAvailability.MISSING);

        Assert.That(result.Files.Select(file => file.FullName), Is.EqualTo(new[] { recording }), "An unchanged recording is not read, so it needs no provider, and leaving it out would remove it from the index.");
    }

    [Test]
    public void ARecordingWhichFailedForGoodStaysWithItsReasonWhenTheProviderIsGone()
    {
        var recording = this.CreateFile("silence.wav", 16);
        var manifest = new DataSourceEmbeddingManifest();
        manifest.PermanentFailures[recording] = new PermanentIndexingFailureRecord(FileSourceIndexer.BuildFileMetadataHash(new FileInfo(recording)), FileExtractionErrorCode.NO_CONTENT, "No content", DateTimeOffset.UtcNow);

        var result = CreateIndexer().GetInputFiles(this.Folder("*"), manifest, MediaTranscriptionAvailability.NOT_ALLOWED);

        Assert.That(result.Files.Select(file => file.FullName), Is.EqualTo(new[] { recording }), "The run lists it with its stored reason instead of reading it.");
    }

    [Test]
    public void AChangedRecordingNeedsAProviderAgain()
    {
        var recording = this.CreateFile("meeting.mp3", 16);
        var manifest = new DataSourceEmbeddingManifest();
        manifest.Files[recording] = new EmbeddedFileRecord("THE-FINGERPRINT-BEFORE-THE-CHANGE", 8, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 3, new string('a', 64));

        var result = CreateIndexer().GetInputFiles(this.Folder("*"), manifest, MediaTranscriptionAvailability.MISSING);

        Assert.That(result.IgnoredFiles, Is.EqualTo(new[] { new DataSourceEmbeddingIgnoredFile(recording, DataSourceEmbeddingIgnoreReason.TRANSCRIPTION_PROVIDER_MISSING) }));
    }

    [Test]
    public void ARecordingAboveTheLimitIsNeverTranscribed()
    {
        var recording = this.CreateFile("conference.mkv", FolderFileSelection.MegabytesToBytes(1) + 1);
        var dataSource = this.Folder("*") with { MaxFileSizeMegabytes = 1 };

        var result = CreateIndexer().GetInputFiles(dataSource, new DataSourceEmbeddingManifest(), Allowed());

        Assert.That(result.IgnoredFiles, Is.EqualTo(new[] { new DataSourceEmbeddingIgnoredFile(recording, DataSourceEmbeddingIgnoreReason.TOO_LARGE) }));
    }

    [Test]
    public void ImagesAreStillLeftOut()
    {
        var image = this.CreateFile("photo.png", 16);

        var result = CreateIndexer().GetInputFiles(this.Folder("*"), new DataSourceEmbeddingManifest(), Allowed());

        Assert.That(result.IgnoredFiles, Is.EqualTo(new[] { new DataSourceEmbeddingIgnoredFile(image, DataSourceEmbeddingIgnoreReason.IMAGE_NOT_SUPPORTED) }));
    }

    [TestCase("meeting.mp3")]
    [TestCase("meeting.mp4")]
    [TestCase("meeting.wav")]
    [TestCase("meeting.webm")]
    public void TheDefaultPatternsSelectNoRecording(string fileName)
    {
        var recording = this.CreateFile(fileName, 16);

        var result = CreateIndexer().GetInputFiles(this.Folder(DataSourceLocalDirectory.DEFAULT_FILE_PATTERNS), new DataSourceEmbeddingManifest(), Allowed());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Files, Is.Empty, "Existing data sources must not start transcribing, and paying for it, after an update.");
            Assert.That(FileNamePatterns.Matches(recording, FileNamePatterns.Parse(":text:")), Is.False);
        }
    }

    [TestCase("*")]
    [TestCase("*.mp3")]
    [TestCase("*.pdf;*.mp3")]
    public void APatternWhichNamesRecordingsSelectsThem(string patterns)
    {
        var recording = this.CreateFile("meeting.mp3", 16);

        var result = CreateIndexer().GetInputFiles(this.Folder(patterns), new DataSourceEmbeddingManifest(), Allowed());

        Assert.That(result.Files.Select(file => file.FullName), Is.EqualTo(new[] { recording }));
    }

    [Test]
    public void TheBatchTranscriptOfASelectedRecordingIsNoDocumentOfItsOwn()
    {
        var recording = this.CreateFile("meeting.mp3", 16);
        var transcript = this.CreateFile(Path.GetFileName(MediaSidecarTranscripts.GetFilePath(recording)), 16);

        var everything = CreateIndexer().GetInputFiles(this.Folder("*"), new DataSourceEmbeddingManifest(), Allowed());
        var markdownOnly = CreateIndexer().GetInputFiles(this.Folder("*.md"), new DataSourceEmbeddingManifest(), Allowed());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(everything.Files.Select(file => file.FullName), Is.EqualTo(new[] { recording }), "The transcript would duplicate the text of the recording.");
            Assert.That(markdownOnly.Files.Select(file => file.FullName), Is.EqualTo(new[] { transcript }), "Without the recording, the transcript is a document like every other.");
        }
    }

    [Test]
    public void ASingleRecordingCanBeADataSource()
    {
        var recording = this.CreateFile("interview.m4a", 16);
        var dataSource = new DataSourceLocalFile { Type = DataSourceType.LOCAL_FILE, FilePath = recording };

        var result = CreateIndexer().GetInputFiles(dataSource, new DataSourceEmbeddingManifest(), Allowed());

        Assert.That(result.Files.Select(file => file.FullName), Is.EqualTo(new[] { recording }));
    }

    [Test]
    public void AReadyTranscriptIsRead()
    {
        var transcript = PersistentMediaTranscriptResult.Ready(new MediaTranscriptSlot("/data/mediaTranscripts/source/key.md", "key"), reused: true);
        Assert.That(FileSourceIndexer.GetTranscriptOrThrow(transcript, CancellationToken.None), Is.EqualTo(new MediaTranscriptSlot("/data/mediaTranscripts/source/key.md", "key")));
    }

    [TestCase(PersistentMediaTranscriptStatus.EMPTY)]
    [TestCase(PersistentMediaTranscriptStatus.NO_AUDIBLE_SIGNAL)]
    public void ARecordingWithoutTextIsRecordedForGood(PersistentMediaTranscriptStatus status)
    {
        var transcript = PersistentMediaTranscriptResult.Unavailable(status, null, "Nothing to read.");

        var exception = Assert.Throws<FileExtractionException>(() => FileSourceIndexer.GetTranscriptOrThrow(transcript, CancellationToken.None));
        Assert.That(exception!.Code, Is.EqualTo(FileExtractionErrorCode.NO_CONTENT), "Asked again with every run, the provider would be paid again with every run.");
    }

    [Test]
    public void ARecordingTheMediaPipelineCannotReadIsRecordedForGood()
    {
        var transcript = PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.TRANSCRIPTION_FAILED, null, "The container is damaged.", MediaJobErrorCode.DAMAGED_CONTAINER);

        var exception = Assert.Throws<FileExtractionException>(() => FileSourceIndexer.GetTranscriptOrThrow(transcript, CancellationToken.None));
        Assert.That(exception!.Code, Is.EqualTo(FileExtractionErrorCode.MEDIA_PROCESSING_FAILED));
    }

    [Test]
    public void ARecordingTheProviderRefusedIsRecordedForGood()
    {
        var transcript = PersistentMediaTranscriptResult.Unavailable(PersistentMediaTranscriptStatus.REJECTED, null, "The provider 'Cloud' reported an error: Audio file might be corrupted or unsupported");

        var exception = Assert.Throws<FileExtractionException>(() => FileSourceIndexer.GetTranscriptOrThrow(transcript, CancellationToken.None));
        Assert.That(exception!.Code, Is.EqualTo(FileExtractionErrorCode.MEDIA_PROCESSING_FAILED), "Tried again with every run, the same file would be uploaded again with every run.");
    }

    [TestCase(PersistentMediaTranscriptStatus.TRANSCRIPTION_FAILED)]
    [TestCase(PersistentMediaTranscriptStatus.STORE_FAILED)]
    [TestCase(PersistentMediaTranscriptStatus.SOURCE_CHANGED)]
    [TestCase(PersistentMediaTranscriptStatus.NO_PROVIDER)]
    public void AFailureWhichMayPassIsTriedAgain(PersistentMediaTranscriptStatus status)
    {
        var transcript = PersistentMediaTranscriptResult.Unavailable(status, null, "Try again later.");
        Assert.Throws<IOException>(() => FileSourceIndexer.GetTranscriptOrThrow(transcript, CancellationToken.None));
    }

    private DataSourceLocalDirectory Folder(string patterns) => new()
    {
        Type = DataSourceType.LOCAL_DIRECTORY,
        Path = this.testRoot,
        FilePatterns = patterns,
    };

    private string CreateFile(string fileName, long length)
    {
        var filePath = Path.Combine(this.testRoot, fileName);
        using var stream = File.Create(filePath);
        stream.SetLength(length);
        return filePath;
    }

    private static MediaTranscriptionAvailability Allowed() => MediaTranscriptionAvailability.Allowed(new TranscriptionProvider { Id = "test-transcription-provider" });

    // Finding the files reads none of them, so it needs neither the runtime nor a transcription provider:
    private static FileSourceIndexer CreateIndexer() => new(null!, null!, null!, null!, NullLogger.Instance);
}