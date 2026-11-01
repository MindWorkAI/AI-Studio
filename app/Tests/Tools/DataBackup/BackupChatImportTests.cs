using System.IO.Compression;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.ChatArchive;
using AIStudio.Tools.Databases.ProvenanceStore;
using AIStudio.Tools.DataBackup;

using Microsoft.EntityFrameworkCore;

namespace AIStudio.Tests.Tools.DataBackup;

/// <summary>
/// Checks adding the chats of a backup to the chats of another computer.
/// </summary>
/// <remarks>
/// A backup is written on one computer and read on another, whose data folder lies elsewhere. Its
/// chats have to arrive with their transcripts, which the chats name by a path relative to the data
/// folder, and a chat which exists already must not turn into a second chat with the same ID.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class BackupChatImportTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    private string SourceDataDirectory => Path.Combine(this.testDirectory, "source");

    private string TargetDataDirectory => Path.Combine(this.testDirectory, "target");

    private string ArchivePath => Path.Combine(this.testDirectory, "backup.mwbackup");

    [SetUp]
    public void RememberDataDirectory()
    {
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-backup-chat-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
    }

    [TearDown]
    public void RemoveDataDirectories()
    {
        SettingsManager.DataDirectory = this.previousDataDirectory;
        WorkspaceBehaviour.TestDataDirectory = null;

        // Whoever reads the cache next must not get the workspaces of this test:
        WorkspaceBehaviour.InvalidateWorkspaceTreeCache();

        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public async Task TheChatsOfABackupArriveWithTheirTranscripts()
    {
        await using var provenance = await TemporaryProvenanceStore.CreateAsync();
        var storedChat = await this.WriteBackupWithAChatAsync();
        await UseDataDirectoryAsync(this.TargetDataDirectory);

        var preview = await ChatArchiveImporter.ReadPreviewAsync(this.ArchivePath, CancellationToken.None);
        var result = await ChatArchiveImporter.ImportAsync(this.ArchivePath, provenance.Client, ChatArchiveCollisionBehavior.SKIP, null, CancellationToken.None);
        var chat = await WorkspaceBehaviour.LoadChatAsync(new(storedChat.WorkspaceId, storedChat.ChatId));
        var transcript = ((ContentText)chat!.Blocks.Single().Content!).FileAttachments.Single();
        var chatsWithEntries = await provenance.Client.GetChatIdsAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chatsWithEntries, Is.Empty, "A backup without a provenance store, e.g., of an older version, brings its chats all the same.");
            Assert.That(preview.Success, Is.True, preview.Issue);
            Assert.That(preview.Manifest.Workspaces.Select(workspace => workspace.Name), Is.EqualTo(new[] { "Projects" }));
            Assert.That(preview.Manifest.Workspaces.Single().Chats.Select(listedChat => listedChat.Name), Is.EqualTo(new[] { "Meeting notes" }));
            Assert.That(result.ImportedChats, Is.EqualTo(1));
            Assert.That(result.ImportedWorkspaces, Is.EqualTo(1));
            Assert.That(transcript.FilePath, Does.StartWith(WorkspaceBehaviour.GetChatDirectory(storedChat.WorkspaceId, storedChat.ChatId)),"The transcript belongs to the chat on this computer, not to the data folder of the other one.");
            Assert.That(File.ReadAllText(transcript.FilePath), Does.Contain("Hello and welcome."));
        }
    }

    [Test]
    public async Task AChatWhichExistsAlreadyIsKept()
    {
        await using var provenance = await TemporaryProvenanceStore.CreateAsync();
        await this.WriteBackupWithAChatAsync();
        await UseDataDirectoryAsync(this.TargetDataDirectory);

        await ChatArchiveImporter.ImportAsync(this.ArchivePath, provenance.Client, ChatArchiveCollisionBehavior.SKIP, null, CancellationToken.None);
        var secondResult = await ChatArchiveImporter.ImportAsync(this.ArchivePath, provenance.Client, ChatArchiveCollisionBehavior.SKIP, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondResult.ImportedChats, Is.Zero);
            Assert.That(secondResult.SkippedChats, Is.EqualTo(1), "Two chats with one ID would leave the app unable to tell them apart.");
        }
    }

    [Test]
    public async Task TheChatsOfABackupArriveWithWhatTheyReceived()
    {
        await using var sourceProvenance = await TemporaryProvenanceStore.CreateAsync();
        await using var provenance = await TemporaryProvenanceStore.CreateAsync();
        var storedChat = await this.WriteBackupWithAChatAsync(sourceProvenance);
        await UseDataDirectoryAsync(this.TargetDataDirectory);

        var result = await ChatArchiveImporter.ImportAsync(this.ArchivePath, provenance.Client, ChatArchiveCollisionBehavior.SKIP, null, CancellationToken.None);
        var entries = await provenance.Client.GetEntriesAsync([storedChat.ProvenanceId!.Value], CancellationToken.None);
        var chatsWithEntries = await provenance.Client.GetChatIdsAsync(CancellationToken.None);
        var stagingDirectory = Path.Combine(this.TargetDataDirectory, ".backup-staging");
        var stagingLeftovers = Directory.Exists(stagingDirectory) ? Directory.GetFileSystemEntries(stagingDirectory) : Array.Empty<string>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.ImportedChats, Is.EqualTo(1));
            Assert.That(entries[storedChat.ProvenanceId.Value].Content, Is.EqualTo("Hello and welcome."), "The chat refers to the entry by its id, which the import keeps.");
            Assert.That(chatsWithEntries, Is.EqualTo(new[] { storedChat.ChatId }), "Another chat's entries of the source computer stay behind.");
            Assert.That(stagingLeftovers, Is.Empty, "The unpacked store of the backup is removed afterward.");
        }
    }

    [Test]
    public async Task AChatAddedOnceMoreTakesItsEntriesAlong()
    {
        await using var sourceProvenance = await TemporaryProvenanceStore.CreateAsync();
        await using var provenance = await TemporaryProvenanceStore.CreateAsync();
        var storedChat = await this.WriteBackupWithAChatAsync(sourceProvenance);
        await UseDataDirectoryAsync(this.TargetDataDirectory);

        await ChatArchiveImporter.ImportAsync(this.ArchivePath, provenance.Client, ChatArchiveCollisionBehavior.IMPORT_AS_COPY, null, CancellationToken.None);
        var secondResult = await ChatArchiveImporter.ImportAsync(this.ArchivePath, provenance.Client, ChatArchiveCollisionBehavior.IMPORT_AS_COPY, null, CancellationToken.None);
        var chatsWithEntries = await provenance.Client.GetChatIdsAsync(CancellationToken.None);

        var entryKey = storedChat.ProvenanceId!.Value.ToString("D");
        await using var context = provenance.CreateContext();
        var copiesOfTheEntry = await context.Entries.CountAsync(entry => entry.Id == entryKey);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(secondResult.ImportedChats, Is.EqualTo(1));
            Assert.That(chatsWithEntries, Has.Count.EqualTo(2), "The copy under its new id gets entries of its own, so deleting one of the two chats leaves the other whole.");
            Assert.That(chatsWithEntries, Does.Contain(storedChat.ChatId));
            Assert.That(copiesOfTheEntry, Is.EqualTo(2), "Both chats refer to the entry by the same id.");
        }
    }

    [Test]
    public async Task ABackupOfANewerFormatIsRefused()
    {
        var manifest = new DataBackupManifest(DataBackupFormat.FORMAT_VERSION + 1, "99.0.0", "V9", DateTimeOffset.UtcNow, "/data", "/config", false, []);
        await using (var archiveStream = File.Create(this.ArchivePath))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
        {
            await using var manifestStream = archive.CreateEntry(DataBackupFormat.MANIFEST_ENTRY_NAME).Open();
            await JsonSerializer.SerializeAsync(manifestStream, manifest, DataBackupFormat.JSON_OPTIONS);
        }

        var preview = await ChatArchiveImporter.ReadPreviewAsync(this.ArchivePath, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Success, Is.False, "This version cannot know what a later format holds.");
            Assert.That(preview.Issue, Is.Not.Empty);
        }
    }

    [Test]
    public async Task AZipFileWithoutManifestIsNoBackup()
    {
        await using (var archiveStream = File.Create(this.ArchivePath))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
        {
            await using var entryStream = archive.CreateEntry("notes.txt").Open();
            await entryStream.WriteAsync(Encoding.UTF8.GetBytes("Not a backup."));
        }

        var preview = await ChatArchiveImporter.ReadPreviewAsync(this.ArchivePath, CancellationToken.None);

        Assert.That(preview.Success, Is.False);
    }

    /// <summary>
    /// Stores a chat with a transcript in a workspace of the source computer, and backs up its data folder.
    /// </summary>
    /// <param name="sourceProvenance">The provenance store of the source computer, which then holds what the chat received, or null when the backup holds no store.</param>
    private async Task<StoredChat> WriteBackupWithAChatAsync(TemporaryProvenanceStore? sourceProvenance = null)
    {
        await UseDataDirectoryAsync(this.SourceDataDirectory);

        var workspace = await WorkspaceBehaviour.TryCreateWorkspaceAsync("Projects");
        Assert.That(workspace.Success, Is.True, "The workspace could not be created.");

        var workspaceId = workspace.Workspace.WorkspaceId;
        var chatId = Guid.NewGuid();
        var transcriptDirectory = Path.Combine(WorkspaceBehaviour.GetChatDirectory(workspaceId, chatId), "attachments", "transcripts");
        Directory.CreateDirectory(transcriptDirectory);

        var transcriptPath = Path.Combine(transcriptDirectory, "meeting-transcript-0001.md");
        await File.WriteAllTextAsync(transcriptPath, "# Transcription: meeting.mp4\n\nHello and welcome.");

        var chat = new ChatThread
        {
            WorkspaceId = workspaceId,
            ChatId = chatId,
            Name = "Meeting notes",
            Blocks =
            [
                new ContentBlock
                {
                    Time = DateTimeOffset.UtcNow,
                    ContentType = ContentType.TEXT,
                    Content = new ContentText { Text = "Summarize the meeting", FileAttachments = [new ManagedTranscriptAttachment("meeting-transcript-0001.md", transcriptPath, new FileInfo(transcriptPath).Length, "meeting.mp4", false)] },
                    Role = ChatRole.USER,
                },
            ],
        };

        Assert.That(await WorkspaceBehaviour.StoreChatAsync(chat), Is.True, "The chat could not be stored.");

        //
        // What the chat received, next to an entry of a chat the backup does not hold, copied the way
        // a backup copies the store:
        //
        Guid? provenanceId = null;
        Func<CancellationToken, Task<string>>? copyProvenanceStoreAsync = null;
        if (sourceProvenance is not null)
        {
            provenanceId = await sourceProvenance.Client.StoreAsync(chatId, ProvenanceKind.ATTACHMENT, "Hello and welcome.", null, CancellationToken.None);
            await sourceProvenance.Client.StoreAsync(Guid.NewGuid(), ProvenanceKind.WEB_PAGE, "A page of another chat.", null, CancellationToken.None);

            var provenanceCopyDirectory = Path.Combine(this.testDirectory, "provenance-copy");
            copyProvenanceStoreAsync = async token =>
            {
                await sourceProvenance.Client.BackupToDirectoryAsync(Path.Combine(provenanceCopyDirectory, "databases", "provenance"), token);
                return provenanceCopyDirectory;
            };
        }

        var sources = new DataBackupSources(this.SourceDataDirectory, Path.Combine(this.testDirectory, "config"), "{}", string.Empty, [], copyProvenanceStoreAsync);
        var manifest = new DataBackupManifest(DataBackupFormat.FORMAT_VERSION, "26.10.1", "V6", DateTimeOffset.UtcNow, this.SourceDataDirectory, string.Empty, false, []);
        await using (var archiveStream = File.Create(this.ArchivePath))
            await DataBackupWriter.WriteAsync(archiveStream, sources, manifest, null, CancellationToken.None);

        return new(workspaceId, chatId, provenanceId);
    }

    /// <summary>
    /// Makes the given folder the data folder, as another computer would have it.
    /// </summary>
    /// <remarks>
    /// The data directory is static state of the whole application, which is why this fixture does
    /// not run alongside others. The workspace behaviour keeps the roots of the workspaces and of the
    /// temporary chats fixed, so it gets told about it separately, and its cache starts over.
    /// </remarks>
    private static async Task UseDataDirectoryAsync(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        SettingsManager.DataDirectory = dataDirectory;
        WorkspaceBehaviour.TestDataDirectory = dataDirectory;
        await WorkspaceBehaviour.ForceReloadWorkspaceTreeAsync();
    }

    /// <summary>
    /// A chat as the source computer stored it.
    /// </summary>
    /// <param name="WorkspaceId">The workspace of the chat.</param>
    /// <param name="ChatId">The chat.</param>
    /// <param name="ProvenanceId">The entry of what the chat received, or null when the backup holds no provenance store.</param>
    private sealed record StoredChat(Guid WorkspaceId, Guid ChatId, Guid? ProvenanceId = null);
}