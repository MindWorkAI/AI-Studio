using AIStudio.Settings;
using AIStudio.Tools.Media;
using AIStudio.Tools.Rust;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Media;

/// <summary>
/// Checks the rules which keep a paid transcription from being paid twice.
/// </summary>
[TestFixture]
public sealed class PersistentMediaTranscriptTests
{
    private TranscriptionProvider? provider;

    private string directoryPath = string.Empty;

    private string mediaPath = string.Empty;

    [SetUp]
    public async Task CreateMediaFile()
    {
        this.directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-persistent-transcript-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.directoryPath);
        this.mediaPath = Path.Combine(this.directoryPath, "recording.mp3");
        this.provider = new TranscriptionProvider { Id = "test-transcription-provider" };
        await File.WriteAllBytesAsync(this.mediaPath, [1, 2, 3]);
    }

    [TearDown]
    public void DeleteMediaFile()
    {
        if (Directory.Exists(this.directoryPath))
            Directory.Delete(this.directoryPath, true);
    }

    [Test]
    public async Task AnExistingTranscriptIsUsedWithoutAskingTheProvider()
    {
        await File.WriteAllTextAsync(MediaSidecarTranscripts.GetFilePath(this.mediaPath), "Transcript");
        var transcriber = new CountingTranscriber(() => MediaTranscriptionResult.Succeeded("New transcript"));

        var result = await this.ReadOrCreateAsync(transcriber, this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
            Assert.That(result.Reused, Is.True);
            Assert.That(transcriber.Calls, Is.Zero);
        });
    }

    [Test]
    public async Task AnExistingTranscriptIsUsedEvenWithoutAProvider()
    {
        await File.WriteAllTextAsync(MediaSidecarTranscripts.GetFilePath(this.mediaPath), "Transcript");
        var result = await this.ReadOrCreateAsync(new CountingTranscriber(() => MediaTranscriptionResult.Succeeded("New transcript")), null);
        Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
    }

    [Test]
    public async Task WithoutATranscriptAndAProviderNothingIsTranscribed()
    {
        var transcriber = new CountingTranscriber(() => MediaTranscriptionResult.Succeeded("Transcript"));

        var result = await this.ReadOrCreateAsync(transcriber, null);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.NO_PROVIDER));
            Assert.That(transcriber.Calls, Is.Zero);
        });
    }

    [Test]
    public async Task ANewTranscriptIsStoredAndPaidForOnlyOnce()
    {
        var transcriber = new CountingTranscriber(() => MediaTranscriptionResult.Succeeded("Transcript"));

        var first = await this.ReadOrCreateAsync(transcriber, this.provider);
        var second = await this.ReadOrCreateAsync(transcriber, this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(first.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
            Assert.That(first.Reused, Is.False);
            Assert.That(File.ReadAllText(first.TranscriptPath), Is.EqualTo("Transcript"));
            Assert.That(second.Reused, Is.True);
            Assert.That(transcriber.Calls, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(this.directoryPath, "*.tmp"), Is.Empty);
        });
    }

    [Test]
    public async Task AnEmptyTranscriptIsNeverStored()
    {
        var result = await this.ReadOrCreateAsync(new CountingTranscriber(() => MediaTranscriptionResult.Succeeded(" \n ")), this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.EMPTY));
            Assert.That(File.Exists(MediaSidecarTranscripts.GetFilePath(this.mediaPath)), Is.False);
        });
    }

    [Test]
    public async Task ATranscriptHoldingOnlyWhiteSpaceIsReplaced()
    {
        await File.WriteAllTextAsync(MediaSidecarTranscripts.GetFilePath(this.mediaPath), "  \n ");
        var transcriber = new CountingTranscriber(() => MediaTranscriptionResult.Succeeded("Transcript"));

        var result = await this.ReadOrCreateAsync(transcriber, this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
            Assert.That(transcriber.Calls, Is.EqualTo(1));
            Assert.That(File.ReadAllText(result.TranscriptPath), Is.EqualTo("Transcript"));
        });
    }

    [Test]
    public async Task AFileWhichChangesDuringItsTranscriptionKeepsNoTranscript()
    {
        var transcriber = new CountingTranscriber(() =>
        {
            File.WriteAllBytes(this.mediaPath, [1, 2, 3, 4]);
            return MediaTranscriptionResult.Succeeded("Transcript of the old file");
        });

        var result = await this.ReadOrCreateAsync(transcriber, this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.SOURCE_CHANGED));
            Assert.That(File.Exists(MediaSidecarTranscripts.GetFilePath(this.mediaPath)), Is.False);
        });
    }

    [Test]
    public async Task ATranscriptWhichArrivesAfterACancellationIsStillStored()
    {
        using var cancellation = new CancellationTokenSource();
        var transcriber = new CountingTranscriber(() =>
        {
            cancellation.Cancel();
            return MediaTranscriptionResult.Succeeded("Paid transcript");
        });

        var result = await PersistentMediaTranscript.ReadOrCreateAsync(MediaSidecarTranscripts.INSTANCE, this.mediaPath, this.provider, transcriber.TranscribeAsync, NullLogger.Instance, cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
            Assert.That(File.ReadAllText(MediaSidecarTranscripts.GetFilePath(this.mediaPath)), Is.EqualTo("Paid transcript"));
        });
    }

    [Test]
    public async Task ACancelledTranscriptionStoresNothing()
    {
        var result = await this.ReadOrCreateAsync(new CountingTranscriber(MediaTranscriptionResult.Cancelled), this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.CANCELLED));
            Assert.That(File.Exists(MediaSidecarTranscripts.GetFilePath(this.mediaPath)), Is.False);
        });
    }

    [Test]
    public async Task AFailedTranscriptionKeepsTheReasonOfTheMediaPipeline()
    {
        var result = await this.ReadOrCreateAsync(new CountingTranscriber(() => MediaTranscriptionResult.Failed("The container is damaged.", MediaJobErrorCode.DAMAGED_CONTAINER)), this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.TRANSCRIPTION_FAILED));
            Assert.That(result.ErrorCode, Is.EqualTo(MediaJobErrorCode.DAMAGED_CONTAINER));
            Assert.That(result.UserMessage, Is.EqualTo("The container is damaged."));
        });
    }

    [Test]
    public async Task ATranscriptWhichCannotBeStoredIsReportedAndLeavesNoTemporaryFile()
    {
        //
        // A file where the folder of the transcript should be: writing fails for every user, root
        // included, unlike a folder without write permission.
        //
        var blockingFilePath = Path.Combine(this.directoryPath, "not-a-folder");
        await File.WriteAllTextAsync(blockingFilePath, "occupied");
        var location = new FixedLocation(Path.Combine(blockingFilePath, "transcript.md"));

        var result = await PersistentMediaTranscript.ReadOrCreateAsync(location, this.mediaPath, this.provider, new CountingTranscriber(() => MediaTranscriptionResult.Succeeded("Transcript")).TranscribeAsync, NullLogger.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.STORE_FAILED));
            Assert.That(Directory.GetFiles(this.directoryPath, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        });
    }

    [Test]
    public async Task AMissingMediaFileIsReportedWithoutAskingTheProvider()
    {
        File.Delete(this.mediaPath);
        var transcriber = new CountingTranscriber(() => MediaTranscriptionResult.Succeeded("Transcript"));

        var result = await this.ReadOrCreateAsync(transcriber, this.provider);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(PersistentMediaTranscriptStatus.SOURCE_UNREADABLE));
            Assert.That(transcriber.Calls, Is.Zero);
        });
    }

    private Task<PersistentMediaTranscriptResult> ReadOrCreateAsync(CountingTranscriber transcriber, TranscriptionProvider? provider) => PersistentMediaTranscript.ReadOrCreateAsync(MediaSidecarTranscripts.INSTANCE, this.mediaPath, provider, transcriber.TranscribeAsync, NullLogger.Instance, CancellationToken.None);

    private sealed class CountingTranscriber(Func<MediaTranscriptionResult> answer)
    {
        public int Calls { get; private set; }

        public Task<MediaTranscriptionResult> TranscribeAsync(string mediaPath, TranscriptionProvider provider, CancellationToken token)
        {
            this.Calls++;
            return Task.FromResult(answer());
        }
    }

    private sealed class FixedLocation(string transcriptPath) : IMediaTranscriptLocation
    {
        public Task<MediaTranscriptSlot> ResolveAsync(string mediaPath, CancellationToken token) => Task.FromResult(new MediaTranscriptSlot(transcriptPath, "fixed"));
    }
}