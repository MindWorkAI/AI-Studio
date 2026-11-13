using AIStudio.Settings;
using AIStudio.Tools.Media;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Media;

/// <summary>
/// Checks where data sources keep the transcripts of their audio and video files.
/// </summary>
[TestFixture]
public sealed class DataSourceTranscriptStoreTests
{
    private static readonly string DATA_SOURCE_ID = Guid.NewGuid().ToString();

    private string rootPath = string.Empty;

    private string dataDirectory = string.Empty;

    private string userFolder = string.Empty;

    [SetUp]
    public void CreateFolders()
    {
        this.rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-transcript-store-{Guid.NewGuid():N}");
        this.dataDirectory = Path.Combine(this.rootPath, "data");
        this.userFolder = Path.Combine(this.rootPath, "user");
        Directory.CreateDirectory(this.dataDirectory);
        Directory.CreateDirectory(this.userFolder);
    }

    [TearDown]
    public void DeleteFolders()
    {
        if (Directory.Exists(this.rootPath))
            Directory.Delete(this.rootPath, true);
    }

    [Test]
    public async Task TheTranscriptLiesBelowTheDataDirectoryOfItsDataSource()
    {
        var mediaPath = await this.CreateMediaAsync("recording.mp3", [1, 2, 3]);
        var slot = await this.Store().ResolveAsync(mediaPath, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceTranscriptStore.IsKey(slot.Key), Is.True);
            Assert.That(slot.Path, Is.EqualTo(Path.Join(this.dataDirectory, DataSourceTranscriptStore.FOLDER_NAME, DATA_SOURCE_ID, slot.Key + ".md")));
        }
    }

    [Test]
    public async Task TheSameContentFindsTheSameTranscriptWhereverTheFileLies()
    {
        var original = await this.CreateMediaAsync("recording.mp3", [1, 2, 3]);
        var copy = await this.CreateMediaAsync(Path.Combine("moved", "renamed.mp3"), [1, 2, 3]);
        File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddDays(-3));
        var other = await this.CreateMediaAsync("other.mp3", [1, 2, 3, 4]);

        var store = this.Store();
        var originalSlot = await store.ResolveAsync(original, CancellationToken.None);
        var copySlot = await store.ResolveAsync(copy, CancellationToken.None);
        var otherSlot = await store.ResolveAsync(other, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copySlot, Is.EqualTo(originalSlot));
            Assert.That(otherSlot.Key, Is.Not.EqualTo(originalSlot.Key));
        }
    }

    [Test]
    public async Task AKnownHashSparesReadingTheRecordingAgain()
    {
        var mediaPath = await this.CreateMediaAsync("recording.mp3", [1, 2, 3]);
        var store = this.Store();
        var knownHash = (await store.ResolveAsync(mediaPath, CancellationToken.None)).Key;

        // A hash the content does not have shows whether the store hashed the file once more:
        var unrelatedHash = new string('a', knownHash.Length);
        var slotWhileUnchanged = await store.WithKnownContentHash(unrelatedHash, _ => true).ResolveAsync(mediaPath, CancellationToken.None);
        var slotOnceChanged = await store.WithKnownContentHash(unrelatedHash, _ => false).ResolveAsync(mediaPath, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(slotWhileUnchanged.Key, Is.EqualTo(unrelatedHash), "Hashing a video reads all of it, which the indexer did already.");
            Assert.That(slotWhileUnchanged.Path, Is.EqualTo(store.GetPath(unrelatedHash)));
            Assert.That(slotOnceChanged.Key, Is.EqualTo(knownHash), "A file which changed since it was hashed must not get the transcript of its content before.");
        }
    }

    [Test]
    public async Task ARenamedRecordingIsPaidForOnlyOnce()
    {
        var mediaPath = await this.CreateMediaAsync("recording.mp3", [1, 2, 3]);
        var provider = new TranscriptionProvider { Id = "test-transcription-provider" };
        var calls = 0;
        Task<MediaTranscriptionResult> Transcribe(string path, TranscriptionProvider _, CancellationToken __)
        {
            calls++;
            return Task.FromResult(MediaTranscriptionResult.Succeeded("Transcript"));
        }

        var first = await PersistentMediaTranscript.ReadOrCreateAsync(this.Store(), mediaPath, provider, Transcribe, NullLogger.Instance, CancellationToken.None);

        var renamedPath = Path.Combine(this.userFolder, "renamed.mp3");
        File.Move(mediaPath, renamedPath);
        File.SetLastWriteTimeUtc(renamedPath, DateTime.UtcNow.AddMinutes(5));
        var second = await PersistentMediaTranscript.ReadOrCreateAsync(this.Store(), renamedPath, provider, Transcribe, NullLogger.Instance, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
            Assert.That(second.Status, Is.EqualTo(PersistentMediaTranscriptStatus.READY));
            Assert.That(second.Reused, Is.True);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(Directory.GetFiles(this.userFolder), Has.Length.EqualTo(1), "The data source wrote into the folder of the user.");
        }
    }

    [Test]
    public async Task OnlyTranscriptsAreListedAndDeleted()
    {
        var store = this.Store();
        var key = new string('a', 64);
        var transcriptPath = store.GetPath(key);
        var folder = Path.GetDirectoryName(transcriptPath)!;
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(transcriptPath, "Transcript");
        await File.WriteAllTextAsync($"{transcriptPath}.{Guid.NewGuid():N}.tmp", "Interrupted");
        await File.WriteAllTextAsync(Path.Combine(folder, "notes.md"), "Not ours");

        Assert.That(store.ListKeys(), Is.EqualTo(new[] { key }));

        store.DeleteAll();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(File.Exists(transcriptPath), Is.False);
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
            Assert.That(File.Exists(Path.Combine(folder, "notes.md")), Is.True, "A file which the store did not write was deleted.");
        }
    }

    [Test]
    public async Task DeletingAllTranscriptsRemovesTheFolderOfTheDataSource()
    {
        var store = this.Store();
        var transcriptPath = store.GetPath(new string('b', 64));
        Directory.CreateDirectory(Path.GetDirectoryName(transcriptPath)!);
        await File.WriteAllTextAsync(transcriptPath, "Transcript");

        store.DeleteAll();

        Assert.That(Directory.Exists(Path.GetDirectoryName(transcriptPath)), Is.False);
    }

    [Test]
    public void DeletingADataSourceWithoutTranscriptsWorks()
    {
        Assert.DoesNotThrow(() => this.Store().DeleteAll());
    }

    [Test]
    public async Task ALinkInTheFolderIsNeitherFollowedNorDeleted()
    {
        var store = this.Store();
        var key = new string('c', 64);
        var linkPath = store.GetPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        var targetPath = Path.Combine(this.userFolder, "precious.md");
        await File.WriteAllTextAsync(targetPath, "Precious");

        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            Assert.Ignore("Creating symbolic links needs a privilege this system does not grant.");
        }

        var deleted = store.Delete(key);
        store.DeleteAll();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(deleted, Is.False);
            Assert.That(File.ReadAllText(targetPath), Is.EqualTo("Precious"));
        }
    }

    [Test]
    public async Task ABackupCopiesOnlyTheTranscriptsOfAllDataSources()
    {
        var secondId = Guid.NewGuid().ToString();
        var firstPath = this.Store().GetPath(new string('d', 64));
        var secondPath = new DataSourceTranscriptStore(this.dataDirectory, secondId).GetPath(new string('e', 64));
        foreach (var path in new[] { firstPath, secondPath })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "Transcript");
        }

        await File.WriteAllTextAsync($"{firstPath}.{Guid.NewGuid():N}.tmp", "Interrupted");
        var targetDirectory = Path.Combine(this.rootPath, "copies");

        var copied = await DataSourceTranscriptStore.CopyAllAsync(this.dataDirectory, targetDirectory, CancellationToken.None);

        var copiedFiles = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(targetDirectory, path).Replace('\\', '/'));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(copied, Is.EqualTo(2));
            Assert.That(copiedFiles, Is.EquivalentTo(new[]
            {
                $"{DataSourceTranscriptStore.FOLDER_NAME}/{DATA_SOURCE_ID}/{new string('d', 64)}.md",
                $"{DataSourceTranscriptStore.FOLDER_NAME}/{secondId}/{new string('e', 64)}.md",
            }));
        }
    }

    [Test]
    public async Task ABackupWithoutTranscriptsCopiesNothing()
    {
        Assert.That(await DataSourceTranscriptStore.CopyAllAsync(this.dataDirectory, Path.Combine(this.rootPath, "copies"), CancellationToken.None), Is.Zero);
    }

    [TestCase("")]
    [TestCase("ABCDEF")]
    [TestCase("../outside")]
    public void ADataSourceIdWhichIsNoGuidIsRejected(string dataSourceId)
    {
        Assert.Throws<ArgumentException>(() => _ = new DataSourceTranscriptStore(this.dataDirectory, dataSourceId));
    }

    [TestCase("")]
    [TestCase("../../settings")]
    [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void AKeyWhichIsNoHashIsRejected(string key)
    {
        Assert.Throws<ArgumentException>(() => this.Store().GetPath(key));
    }

    private DataSourceTranscriptStore Store() => new(this.dataDirectory, DATA_SOURCE_ID);

    private async Task<string> CreateMediaAsync(string relativePath, byte[] content)
    {
        var path = Path.Combine(this.userFolder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content);
        return path;
    }
}