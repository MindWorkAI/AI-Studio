using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using MudBlazor;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Runs the actual edit, queue, cancellation, manifest preparation, and file indexing code against
/// a migrated SQLite store. Only the vector store and dialog rendering are replaced.
/// </summary>
[NonParallelizable]
public sealed class DataSourceRelocationWorkflowTests
{
    private const string EMBEDDING_ID = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01";

    // Frozen from the format before relocation support. Do not generate this with the current
    // signature builder: doing that would hide an accidental rebuild of every existing index.
    private const string LEGACY_SIGNATURE = "2|b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01|OPEN_AI|text-embedding-3-small|NONE|http://localhost:1234|NONE||8192|512|100";

    private static readonly TimeSpan TEST_TIMEOUT = TimeSpan.FromSeconds(10);

    private string testRoot = string.Empty;
    private TemporaryIndexStore store = null!;
    private SettingsManager settings = null!;
    private DatabaseClientProvider databases = null!;
    private DataSourceEmbeddingService service = null!;
    private RelocationVectorStore vectors = null!;
    private IInternalDataSource source = null!;
    private readonly List<string> chunkIds = [];
    private readonly List<Task> runningTasks = [];
    private CancellationTokenSource lifetime = null!;

    private string OriginalFolder => Path.Combine(this.testRoot, "original");

    [SetUp]
    public async Task SetUp()
    {
        this.testRoot = Path.Combine(Path.GetTempPath(), $"ai-studio-relocation-workflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testRoot);
        this.store = await TemporaryIndexStore.CreateAsync();
        this.settings = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        this.settings.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_RAG_2024);
        this.settings.ConfigurationData.App.DataSourceIndexing.AutomaticRefresh = false;
        this.settings.ConfigurationData.EmbeddingProviders.Add(new EmbeddingProvider(1, EMBEDDING_ID, "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small")));
        this.vectors = new RelocationVectorStore();
        this.databases = new DatabaseClientProvider(null!, NullLoggerFactory.Instance);
        var clients = Field<Dictionary<DatabaseRole, DatabaseClient>>(this.databases, "clients");
        clients.Add(DatabaseRole.INDEX_STORE, this.store.Client);
        clients.Add(DatabaseRole.VECTOR_STORE, this.vectors);
        var guard = new PromptInjectionGuardService(null!, this.settings, NullLogger<PromptInjectionGuardService>.Instance, NullLoggerFactory.Instance);
        this.service = new DataSourceEmbeddingService(this.settings, null!, this.databases, guard, null!, new ThreadSafeRandom(), NullLogger<DataSourceEmbeddingService>.Instance);
        this.lifetime = new CancellationTokenSource();
        this.chunkIds.Clear();
        this.runningTasks.Clear();
    }

    [TearDown]
    public async Task TearDown()
    {
        this.lifetime.Cancel();
        try
        {
            await Task.WhenAll(this.runningTasks).WaitAsync(TEST_TIMEOUT);
        }
        catch (OperationCanceledException) when (this.lifetime.IsCancellationRequested)
        {
            // A failed test may leave its deliberately blocked run for teardown to stop.
        }
        finally
        {
            this.service.Dispose();
            this.databases.Dispose();
            await this.store.DisposeAsync();
            this.lifetime.Dispose();
            Directory.Delete(this.testRoot, true);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AnExistingIndexSurvivesTheUpgradeWithoutReembedding(bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.RelocatedPaths, Is.Empty, "An upgrade is not a relocation.");
            Assert.That(this.vectors.DeletedStores, Is.Zero);
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EditingAndCancelingDoNothingAndSavingOnlyRelocatesUnchangedFiles(bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        var newPath = this.MoveSource("moved");
        var original = this.source;
        var edit = this.OpenEdit(newPath);

        Assert.Multiple(() =>
        {
            Assert.That(this.settings.ConfigurationData.DataSources.Single(), Is.EqualTo(original));
            Assert.That(this.Queue.TryDequeue(out _), Is.False);
            Assert.That(this.vectors.RelocatedPaths, Is.Empty);
            Assert.That(edit.Warning.ShowCount, Is.Zero, "Typing a path must not open the save confirmation.");
        });

        edit.Dialog.Instance.Cancel();
        await edit.ParentTask.WaitAsync(TEST_TIMEOUT);
        Assert.That(this.Queue.TryDequeue(out _), Is.False);

        await this.SaveEditAsync(newPath);
        Assert.That(this.vectors.RelocatedPaths, Is.Empty, "Saving queues the operation; the dialog must not write to either index.");
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(this.chunkIds.Count));
            Assert.That(this.vectors.DeletedStores, Is.Zero);
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
        });
        var manifest = await this.store.Client.GetManifestAsync(this.source.Id, CancellationToken.None);
        Assert.That(manifest.Files.Keys, Is.EquivalentTo(this.vectors.Locations.Values));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SavingAnUnchangedPathDoesNotWarnOrRelocate(bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        var currentPath = this.source is DataSourceLocalDirectory folder ? folder.Path : ((DataSourceLocalFile)this.source).FilePath;
        var edit = this.OpenEdit(currentPath);
        await InvokeTask(edit.Component, "StoreValidatedDataSourceAsync").WaitAsync(TEST_TIMEOUT);
        await edit.ParentTask.WaitAsync(TEST_TIMEOUT);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        Assert.Multiple(() =>
        {
            Assert.That(edit.Warning.ShowCount, Is.Zero);
            Assert.That(this.vectors.RelocatedPaths, Is.Empty);
            Assert.That(this.vectors.DeletedStores, Is.Zero);
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SavingWaitsForThePathWarningBeforeChangingConfigurationOrQueueingWork(bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        var original = this.source;
        var edit = this.OpenEdit(this.MoveSource("moved"));
        var save = InvokeTask(edit.Component, "StoreValidatedDataSourceAsync");

        Assert.Multiple(() =>
        {
            Assert.That(edit.Warning.ShowCount, Is.EqualTo(1));
            Assert.That(save.IsCompleted, Is.False);
            Assert.That(edit.ParentTask.IsCompleted, Is.False);
            Assert.That(this.settings.ConfigurationData.DataSources.Single(), Is.EqualTo(original));
            Assert.That(this.Queue.TryDequeue(out _), Is.False);
            Assert.That(this.vectors.RelocatedPaths, Is.Empty);
        });

        edit.Warning.Complete(DialogResult.Ok(true));
        await save.WaitAsync(TEST_TIMEOUT);
        await edit.ParentTask.WaitAsync(TEST_TIMEOUT);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
        await this.AssertOriginalChunksAsync();
        Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancelingThePathWarningKeepsTheConfiguredPathAndQueuesNothing(bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        var original = this.source;
        var edit = this.OpenEdit(this.MoveSource("moved"));
        edit.Warning.Complete(DialogResult.Cancel());
        await InvokeTask(edit.Component, "StoreValidatedDataSourceAsync");

        Assert.Multiple(() =>
        {
            Assert.That(edit.Warning.ShowCount, Is.EqualTo(1));
            Assert.That(edit.ParentTask.IsCompleted, Is.False, "Rejecting the warning leaves the edit dialog open.");
            Assert.That(this.settings.ConfigurationData.DataSources.Single(), Is.EqualTo(original));
            Assert.That(this.Queue.TryDequeue(out _), Is.False);
            Assert.That(this.vectors.RelocatedPaths, Is.Empty);
        });
        edit.Dialog.Instance.Cancel();
        await edit.ParentTask.WaitAsync(TEST_TIMEOUT);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ANewLocationOfADataSourceWithoutAnIndexIsSavedWithoutAQuestion(bool singleFile)
    {
        await this.AddSourceAsync(singleFile);
        var newPath = this.MoveSource("moved");
        var edit = this.OpenEdit(newPath);
        await InvokeTask(edit.Component, "StoreValidatedDataSourceAsync").WaitAsync(TEST_TIMEOUT);
        await edit.ParentTask.WaitAsync(TEST_TIMEOUT);

        var saved = this.settings.ConfigurationData.DataSources.Single();
        Assert.Multiple(() =>
        {
            Assert.That(edit.Warning.ShowCount, Is.Zero, "Nothing was prepared for the data source, so nothing can be lost.");
            Assert.That(saved is DataSourceLocalDirectory folder ? folder.Path : ((DataSourceLocalFile)saved).FilePath, Is.EqualTo(newPath));
            Assert.That(saved.OutboundDataRestriction, Is.EqualTo(OutboundDataRestriction.UNRESTRICTED), "The dialog creates the data source anew, so a value it does not carry over falls back to its default.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ANewLocationTogetherWithANewEmbeddingAsksOnlyOnce(bool singleFile)
    {
        const string OTHER_EMBEDDING_ID = "c1b5d5e3-2a4f-4b1b-9d0e-6b7c8d9e0f12";
        this.settings.ConfigurationData.EmbeddingProviders.Add(new EmbeddingProvider(2, OTHER_EMBEDDING_ID, "Other embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-large", "text-embedding-3-large")));
        await this.SeedLegacyIndexAsync(singleFile);
        var edit = this.OpenEdit(this.MoveSource("moved"));
        SetField(edit.Component, "dataEmbeddingId", OTHER_EMBEDDING_ID);
        edit.Warning.Complete(DialogResult.Ok(true));
        await InvokeTask(edit.Component, "StoreValidatedDataSourceAsync").WaitAsync(TEST_TIMEOUT);
        await edit.ParentTask.WaitAsync(TEST_TIMEOUT);

        Assert.Multiple(() =>
        {
            Assert.That(edit.Warning.ShowCount, Is.EqualTo(1), "Everything is prepared again anyway, so the location needs no question of its own.");
            Assert.That(edit.Warning.Title, Is.EqualTo("Documents Will Be Prepared Again"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ASecondSavedPathCancelsTheFirstRelocationAndRunsWithTheLatestPath(bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        await this.SaveEditAsync(this.MoveSource("first"));
        this.vectors.BlockNextRelocation = true;
        var firstRun = this.RunNextAsync();
        await this.vectors.RelocationStarted.Task.WaitAsync(TEST_TIMEOUT);

        var secondPath = this.MoveSource("second");
        await this.SaveEditAsync(secondPath);
        await firstRun.WaitAsync(TEST_TIMEOUT);

        Assert.That(this.vectors.CanceledRelocations, Is.EqualTo(1), "The first run must be canceled without finishing its remaining documents.");
        Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(1));
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.RelocatedPaths.Skip(1).ToArray(), Has.Length.EqualTo(this.chunkIds.Count));
            Assert.That(this.vectors.Locations.Values.All(path => singleFile ? path == secondPath : path.StartsWith(secondPath + Path.DirectorySeparatorChar, StringComparison.Ordinal)), Is.True);
            Assert.That(this.vectors.DeletedStores, Is.Zero);
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.Queue.TryDequeue(out _), Is.False, "The obsolete destination must not leave another run behind.");
        });
        var manifest = await this.store.Client.GetManifestAsync(this.source.Id, CancellationToken.None);
        Assert.That(manifest.Files.Keys, Is.EquivalentTo(this.vectors.Locations.Values));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeletingTheSourceCancelsRelocationBeforeRemovingTheStoredIndex(bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        await this.SaveEditAsync(this.MoveSource("moved"));
        this.vectors.BlockNextRelocation = true;
        var run = this.RunNextAsync();
        await this.vectors.RelocationStarted.Task.WaitAsync(TEST_TIMEOUT);

        // A second save also leaves a pending run; deletion has to discard that one as well.
        await this.service.QueueDataSourceAsync(this.source);
        this.settings.ConfigurationData.DataSources.Clear();
        await this.service.RemoveDataSourceAsync(this.source).WaitAsync(TEST_TIMEOUT);
        await run.WaitAsync(TEST_TIMEOUT);

        var manifest = await this.store.Client.GetManifestAsync(this.source.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(this.vectors.CanceledRelocations, Is.EqualTo(1));
            Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(1));
            Assert.That(this.vectors.DeletedStores, Is.EqualTo(1));
            Assert.That(this.vectors.Locations, Is.Empty);
            Assert.That(manifest.Files, Is.Empty);
            Assert.That(this.service.GetStatuses(), Is.Empty);
            Assert.That(this.Queue.TryDequeue(out _), Is.False);
        });
    }

    private DataSourceRunQueue Queue => Field<DataSourceRunQueue>(this.service, "runQueue");

    [TestCase(@"E:\Documents", '\\', false)]
    [TestCase(@"\\server\share\Documents", '\\', false)]
    [TestCase("/home/alice/Documents", '/', false)]
    [TestCase(@"E:\Documents", '\\', true)]
    [TestCase(@"\\server\share\Documents", '\\', true)]
    [TestCase("/Volumes/External drive/Documents", '/', true)]
    public async Task ABackupCanReconnectFilesFromAnotherOSWithoutReembedding(string oldRoot, char separator, bool singleFile)
    {
        await this.SeedLegacyIndexAsync(singleFile);
        var originalFolder = Path.Combine(this.testRoot, "original");
        await this.StoreForeignPathsAsync(oldRoot, separator);
        var restored = await this.BackupAndRestoreIndexAsync();

        // Opening the restored index while the old drive or network share is unavailable must not
        // prune anything. No real external volume or network connection is needed by these tests.
        if (!RelocatedFilePaths.IsLocalAbsolute(oldRoot))
        {
            await this.service.QueueDataSourceAsync(this.source);
            await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            await this.AssertOriginalChunksAsync();
        }

        // User documents are not in an AI Studio backup. Copy them separately, preserving their
        // modification times, as when reconnecting an external drive after restoring a computer.
        var destination = Path.Combine(this.testRoot, "mounted volume", "Übertragene Dokumente");
        foreach (var original in Directory.EnumerateFiles(originalFolder, "*", SearchOption.AllDirectories))
        {
            var localPath = Path.Combine(destination, Path.GetRelativePath(originalFolder, original));
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            File.Copy(original, localPath);
            File.SetLastWriteTimeUtc(localPath, File.GetLastWriteTimeUtc(original));
        }

        var newPath = singleFile ? Path.Combine(destination, "notes.md") : destination;
        await this.SaveEditAsync(newPath);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        var manifest = await restored.GetManifestAsync(this.source.Id, CancellationToken.None);
        var hits = await restored.SearchChunksAsync(this.source.Id, "indexed", 10, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(this.chunkIds.Count));
            Assert.That(this.vectors.DeletedStores, Is.Zero);
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
            Assert.That(manifest.Files.Keys, Is.EquivalentTo(this.vectors.Locations.Values));
            Assert.That(hits.Select(hit => hit.AbsolutePath), Is.EquivalentTo(this.vectors.Locations.Values));
            Assert.That(hits.All(hit => PathTools.IsInsideDirectory(destination, hit.AbsolutePath)), Is.True);
        });

        // A restart/hash check after relocation must not relocate or prepare everything a second time.
        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
        Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(this.chunkIds.Count));
        await this.AssertOriginalChunksAsync();

        // A disconnected external drive is not an empty folder. Reconnecting it must also keep
        // the embeddings, rather than paying to prepare the same documents again.
        var disconnected = destination + "-disconnected";
        Directory.Move(destination, disconnected);
        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
        Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.FAILED));
        await this.AssertOriginalChunksAsync();
        Directory.Move(disconnected, destination);
        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
        await this.AssertOriginalChunksAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
            Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(this.chunkIds.Count));
        });
    }

    private async Task StoreForeignPathsAsync(string oldRoot, char separator)
    {
        await using var context = this.store.CreateContext();
        foreach (var file in await context.EmbeddedFiles.ToListAsync())
        {
            var relative = file.RelativePath.Replace(Path.DirectorySeparatorChar, separator);
            var oldPath = oldRoot + separator + relative;
            file.AbsolutePath = oldPath;
            file.RelativePath = relative;
            file.Fingerprint = LegacyFingerprint(oldPath, file.FileName, file.LastWriteUtc.UtcDateTime.Ticks, file.FileSize);
            this.vectors.Locations[file.ParentFileId] = oldPath;
        }
        await context.SaveChangesAsync();
        this.source = this.source switch
        {
            DataSourceLocalDirectory folder => folder with { Path = oldRoot },
            DataSourceLocalFile file => file with { FilePath = oldRoot + separator + "notes.md" },
            _ => throw new InvalidOperationException(),
        };
        this.settings.ConfigurationData.DataSources[0] = this.source;
    }

    private async Task<SqliteIndexStoreClientImplementation> BackupAndRestoreIndexAsync()
    {
        var dataDirectory = Path.Combine(this.testRoot, "source-data");
        var configDirectory = Path.Combine(this.testRoot, "source-config");
        var copies = Path.Combine(this.testRoot, "index-copies");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(configDirectory);
        await this.store.Client.BackupToDirectoryAsync(Path.Combine(copies, "databases", "sqlite"), CancellationToken.None);

        // This fixture's vector client stores metadata only. The runtime tests separately check
        // backing up, reopening, and relocating actual Qdrant vectors without changing their values.
        var vectorDirectory = Path.Combine(copies, "databases", "vector_database", "stores", "store_test");
        Directory.CreateDirectory(vectorDirectory);
        await File.WriteAllTextAsync(Path.Combine(vectorDirectory, "locations.json"), JsonSerializer.Serialize(this.vectors.Locations));
        var archivePath = Path.Combine(this.testRoot, "backup.zip");
        var settingsJson = JsonSerializer.Serialize(this.settings.ConfigurationData, SettingsManager.JSON_OPTIONS);
        await using (var output = File.Create(archivePath))
            await DataBackupWriter.WriteAsync(output, new DataBackupSources(dataDirectory, configDirectory, settingsJson, copies, []),
                new DataBackupManifest(DataBackupFormat.FORMAT_VERSION, "26.10.1", "V6", DateTimeOffset.UtcNow, dataDirectory, configDirectory, true, []), null, CancellationToken.None);

        var target = new DataRestoreTarget(Path.Combine(this.testRoot, "restored-data"), Path.Combine(this.testRoot, "restored-config"), "26.10.1", AIStudio.Settings.Version.V6, []);
        var result = await DataRestorePreparer.PrepareAsync(archivePath, target, null, CancellationToken.None);
        Assert.That(result.Success, Is.True);

        // The runtime applies this prepared directory at restart. Open the exact restored bytes
        // here, so this test exercises the real backup writer and restore preparer, not a rebuilt DB.
        var pendingData = Path.Combine(target.DataDirectory, DataRestoreFormat.PENDING_DIRECTORY);
        var restoredPath = Path.Combine(pendingData, "databases", "sqlite", "rag-index.sqlite3");
        var restored = new SqliteIndexStoreClientImplementation("Restored SQLite", restoredPath, pendingData, string.Empty);
        Field<Dictionary<DatabaseRole, DatabaseClient>>(this.databases, "clients")[DatabaseRole.INDEX_STORE] = restored;
        this.vectors.Locations.Clear();
        foreach (var (id, path) in JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(pendingData, "databases", "vector_database", "stores", "store_test", "locations.json")))!)
            this.vectors.Locations.Add(id, path);

        var restoredSettings = JsonSerializer.Deserialize<Data>(await File.ReadAllTextAsync(Path.Combine(target.ConfigDirectory, DataRestoreFormat.PENDING_DIRECTORY, "settings.json")), SettingsManager.JSON_OPTIONS)!;
        this.settings.ConfigurationData.DataSources.Clear();
        this.settings.ConfigurationData.DataSources.AddRange(restoredSettings.DataSources);
        this.source = (IInternalDataSource)this.settings.ConfigurationData.DataSources.Single();
        return restored;
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AnEmptyMountDirectoryKeepsTheIndexAndReconnectingReusesIt(bool relocated)
    {
        await this.SeedLegacyIndexAsync(false);
        var original = ((DataSourceLocalDirectory)this.source).Path;
        var disconnected = Path.Combine(this.testRoot, "disconnected-drive");
        Directory.Move(original, disconnected);
        var mount = relocated ? Path.Combine(this.testRoot, "new-mount") : original;
        Directory.CreateDirectory(mount);
        var storedHash = (await this.store.Client.GetDataSourceStateAsync(this.source.Id, CancellationToken.None))!.SourceHash;

        if (relocated)
            await this.SaveEditAsync(mount);
        else
            await this.service.QueueDataSourceAsync(this.source);

        // A second refresh must not accept the empty mount as a new baseline and prune the index.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
            await this.AssertOriginalChunksAsync();
            var state = await this.store.Client.GetDataSourceStateAsync(this.source.Id, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.FAILED));
                Assert.That(this.service.GetStatuses().Single().LastError, Does.Contain("appears empty"));
                Assert.That(state!.SourceHash, Is.EqualTo(storedHash));
                Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            });
            if (attempt == 0)
                await this.service.QueueDataSourceAsync(this.source);
        }

        Directory.Delete(mount);
        Directory.Move(disconnected, mount);
        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
        await this.AssertOriginalChunksAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
            Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(relocated ? this.chunkIds.Count : 0));
        });
    }

    [Test]
    public async Task RemovingSomeFilesStillCleansUpOnlyTheirIndexEntries()
    {
        await this.SeedLegacyIndexAsync(false);
        var removed = Path.Combine(((DataSourceLocalDirectory)this.source).Path, "notes.md");
        var removedChunkId = IndexedDocumentIds.CreateChunkId(this.source.Id, LegacyFingerprint(new FileInfo(removed)), 0);
        File.Delete(removed);

        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
        var chunks = await this.store.Client.SearchChunksAsync(this.source.Id, "indexed", 10, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(chunks.Select(chunk => chunk.ChunkId), Is.EquivalentTo(this.chunkIds.Except([removedChunkId])));
            Assert.That(this.vectors.DeletedDocuments, Is.EqualTo(1));
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
        });
    }

    [Test]
    public async Task ExplicitlyExcludingEveryFileStillRemovesTheirIndexEntries()
    {
        await this.SeedLegacyIndexAsync(false);
        this.source = ((DataSourceLocalDirectory)this.source) with { FilePatterns = "*.pdf" };
        this.settings.ConfigurationData.DataSources[0] = this.source;

        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);
        var manifest = await this.store.Client.GetManifestAsync(this.source.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(manifest.Files, Is.Empty);
            Assert.That(this.vectors.Locations, Is.Empty);
            Assert.That(this.vectors.DeletedDocuments, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task AFolderWhoseNameChangesOnlyInCaseKeepsItsEmbeddings()
    {
        await this.SeedLegacyIndexAsync(false);
        var renamed = Path.Combine(this.testRoot, "Original");

        // Two moves, since a volume which ignores case may refuse to rename a folder to its own name:
        var detour = Path.Combine(this.testRoot, "renaming");
        Directory.Move(((DataSourceLocalDirectory)this.source).Path, detour);
        Directory.Move(detour, renamed);

        await this.SaveEditAsync(renamed);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        var manifest = await this.store.Client.GetManifestAsync(this.source.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.RelocatedPaths, Has.Count.EqualTo(this.chunkIds.Count));
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.InsertedBatches, Is.Zero);
            Assert.That(manifest.Files.Keys.All(path => path.StartsWith(renamed + Path.DirectorySeparatorChar, StringComparison.Ordinal)), Is.True, "The index must store the new spelling of the folder.");
        });
    }

    /// <summary>
    /// A file saved again without edits, synchronized or copied looks changed by its fingerprint,
    /// but its content is the same. Its chunks and vectors stay, nothing is read or embedded, and
    /// only the new fingerprint is stored, so the next run finds the file unchanged.
    /// </summary>
    [Test]
    public async Task AFileSavedAgainWithoutChangesKeepsItsEmbeddings()
    {
        await this.SeedLegacyIndexAsync(false, withContentHash: true);
        var touched = new FileInfo(Path.Combine(this.OriginalFolder, "notes.md"));
        touched.LastWriteTimeUtc = touched.LastWriteTimeUtc.AddMinutes(5);

        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        var manifest = await this.store.Client.GetManifestAsync(this.source.Id, CancellationToken.None);
        touched.Refresh();
        Assert.Multiple(() =>
        {
            Assert.That(this.service.GetStatuses().Single().State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(this.vectors.InsertedBatches, Is.Zero, "Nothing is embedded.");
            Assert.That(this.vectors.DeletedDocuments, Is.Zero, "Nothing is read either: without a runtime here, reading the file would fail and remove it.");
            Assert.That(this.vectors.RelocatedPaths, Is.EqualTo(new[] { touched.FullName }), "The new fingerprint reaches the vectors as well, under the same path.");
            Assert.That(manifest.Files[touched.FullName].Fingerprint, Is.EqualTo(LegacyFingerprint(touched)), "The next run finds the file unchanged.");
        });
    }

    /// <remarks>
    /// This fixture has no runtime, so reading a file fails, and the failed attempt is discarded.
    /// That is how these tests see that a file was read. The file keeps the version it had.
    /// </remarks>
    [Test]
    public async Task AFileWhoseContentChangedIsReadAgain()
    {
        await this.SeedLegacyIndexAsync(false, withContentHash: true);
        var changed = Path.Combine(this.OriginalFolder, "notes.md");
        await File.WriteAllTextAsync(changed, "A changed report");

        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        var manifest = await this.store.Client.GetManifestAsync(this.source.Id, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(this.vectors.DiscardedAttempts, Is.EqualTo(1), "Only the changed file was read.");
            Assert.That(this.vectors.DeletedDocuments, Is.Zero, "A failed attempt keeps the version the file had.");
            Assert.That(this.vectors.RelocatedPaths, Is.Empty);
            Assert.That(manifest.Files.Keys, Does.Contain(changed));
        });
    }

    /// <remarks>
    /// A file indexed before its content hash was kept cannot be compared, so it is read once more
    /// the next time it looks changed, which stores its hash. Cf. AFileWhoseContentChangedIsReadAgain
    /// for how the test sees the read.
    /// </remarks>
    [Test]
    public async Task AFileWithoutAStoredContentHashIsReadAgainWhenItLooksChanged()
    {
        await this.SeedLegacyIndexAsync(false);
        var touched = new FileInfo(Path.Combine(this.OriginalFolder, "notes.md"));
        touched.LastWriteTimeUtc = touched.LastWriteTimeUtc.AddMinutes(5);

        await this.service.QueueDataSourceAsync(this.source);
        await this.RunNextAsync().WaitAsync(TEST_TIMEOUT);

        await this.AssertOriginalChunksAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.vectors.DiscardedAttempts, Is.EqualTo(1));
            Assert.That(this.vectors.DeletedDocuments, Is.Zero);
            Assert.That(this.vectors.RelocatedPaths, Is.Empty);
        });
    }

    /// <param name="singleFile">Whether the data source is a single file rather than a folder.</param>
    /// <param name="withContentHash">Whether the rows keep the hash of the content of their file, as rows indexed since v26.10.1 do.</param>
    private async Task SeedLegacyIndexAsync(bool singleFile, bool withContentHash = false)
    {
        var paths = await this.AddSourceAsync(singleFile);
        var id = this.source.Id;
        await this.store.Client.UpsertDataSourceAsync(id, this.source.Type.ToString(), EMBEDDING_ID, LEGACY_SIGNATURE, "legacy-source-hash", 3, CancellationToken.None);

        foreach (var relative in paths)
        {
            var path = Path.Combine(this.OriginalFolder, relative);
            var file = new FileInfo(path);
            var fingerprint = LegacyFingerprint(file);
            var parentId = IndexedDocumentIds.CreateParentId(id, path);
            var chunkId = IndexedDocumentIds.CreateChunkId(id, fingerprint, 0);
            var embeddedAt = DateTimeOffset.UtcNow.AddDays(-10);
            var contentHash = withContentHash ? await FileContentHash.ComputeAsync(path, CancellationToken.None) : null;
            await this.store.Client.UpsertFileAsync(id, new EmbeddingStateFile(parentId, path, file.Name, relative, "md", fingerprint, file.Length,
                new DateTimeOffset(file.CreationTimeUtc), new DateTimeOffset(file.LastWriteTimeUtc), embeddedAt, 1, ContentHash: contentHash), CancellationToken.None);
            await this.store.Client.UpsertChunksAsync(id, [new EmbeddingStateChunk(chunkId, parentId, 1, 1, 0, "Already indexed report", embeddedAt)], CancellationToken.None);
            this.vectors.Locations.Add(parentId, path);
            this.chunkIds.Add(chunkId);
        }
    }

    /// <summary>
    /// Writes the documents of a data source and configures it, without anything in the index.
    /// </summary>
    /// <returns>The paths of the documents, relative to the folder.</returns>
    private async Task<string[]> AddSourceAsync(bool singleFile)
    {
        var root = this.OriginalFolder;
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        var id = Guid.NewGuid().ToString();
        var folder = new DataSourceLocalDirectory
        {
            Id = id, Name = "Existing documents", Type = DataSourceType.LOCAL_DIRECTORY, Path = root,
            EmbeddingId = EMBEDDING_ID, ConfidenceLevel = ConfidenceLevel.NONE, MaxChunkTokenLength = 512, ChunkOverlapTokenLength = 100,
            FilePatterns = "*.md", ExcludePatterns = string.Empty, OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED,
        };
        this.source = singleFile ? new DataSourceLocalFile
        {
            Id = id, Name = folder.Name, Type = DataSourceType.LOCAL_FILE, FilePath = Path.Combine(root, "notes.md"),
            EmbeddingId = EMBEDDING_ID, ConfidenceLevel = ConfidenceLevel.NONE, MaxChunkTokenLength = 512, ChunkOverlapTokenLength = 100,
            OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED,
        } : folder;
        this.settings.ConfigurationData.DataSources.Add(this.source);

        var paths = singleFile ? new[] { "notes.md" } : new[] { "notes.md", Path.Combine("nested", "report.md") };
        foreach (var relative in paths)
            await File.WriteAllTextAsync(Path.Combine(root, relative), "Already indexed report");

        return paths;
    }

    private string MoveSource(string destinationName)
    {
        var current = (IInternalDataSource)this.settings.ConfigurationData.DataSources.Single();
        var destination = Path.Combine(this.testRoot, destinationName);
        if (current is DataSourceLocalDirectory folder)
            Directory.Move(folder.Path, destination);
        else
        {
            destination += ".md";
            File.Move(((DataSourceLocalFile)current).FilePath, destination);
        }
        return destination;
    }

    private async Task SaveEditAsync(string newPath)
    {
        var edit = this.OpenEdit(newPath);
        edit.Warning.Complete(DialogResult.Ok(true));
        await InvokeTask(edit.Component, "StoreValidatedDataSourceAsync").WaitAsync(TEST_TIMEOUT);
        await edit.ParentTask.WaitAsync(TEST_TIMEOUT);
        Assert.Multiple(() =>
        {
            Assert.That(edit.Warning.ShowCount, Is.EqualTo(1));
            Assert.That(edit.Warning.ComponentType, Is.EqualTo(typeof(ConfirmDialog)));
            Assert.That(edit.Warning.Title, Is.EqualTo("New Location for This Data Source"));
            Assert.That(edit.Warning.Parameters!.Get<string>(nameof(ConfirmDialog.MarkdownBody)), Does.Contain("unchanged at the new location").And.Contain("removed from the index").And.Contain("files on disk stay as they are").And.Contain("costs money"));
        });
        this.source = (IInternalDataSource)this.settings.ConfigurationData.DataSources.Single();
    }

    private OpenedEdit OpenEdit(string newPath)
    {
        var storedSource = (IInternalDataSource)this.settings.ConfigurationData.DataSources.Single();
        var dialog = new TestDataSourceDialog();
        var warning = new TestDataSourceDialog();
        var parent = new DataSourceManagement();
        Property(parent, "SettingsManager", this.settings);
        Property(parent, "MessageBus", MessageBus.INSTANCE);
        Property(parent, "DataSourceEmbeddingService", this.service);
        Property(parent, "DialogService", dialog.Service);
        var parentTask = InvokeTask(parent, "EditDataSource", storedSource);
        object component = storedSource is DataSourceLocalDirectory ? new DataSourceLocalDirectoryDialog() : new DataSourceLocalFileDialog();
        Property(component, "SettingsManager", this.settings);
        Property(component, "DialogService", warning.Service);
        Property(component, "DataSourceEmbeddingService", this.service);
        Property(component, "MudDialog", dialog.Instance);
        Property(component, "IsEditing", true);
        Property(component, "DataSource", storedSource);
        SetField(component, "dataId", storedSource.Id);
        SetField(component, "dataName", storedSource.Name);
        SetField(component, "dataEmbeddingId", storedSource.EmbeddingId);
        SetField(component, "dataConfidenceLevel", storedSource.ConfidenceLevel);
        SetField(component, "dataOutboundDataRestriction", storedSource.OutboundDataRestriction);
        SetField(component, "dataMaxChunkTokenLength", storedSource.MaxChunkTokenLength);
        SetField(component, "dataChunkOverlapTokenLength", storedSource.ChunkOverlapTokenLength);
        SetField(component, storedSource is DataSourceLocalDirectory ? "dataPath" : "dataFilePath", newPath);
        if (storedSource is DataSourceLocalDirectory folder)
        {
            SetField(component, "dataFilePatterns", folder.FilePatterns);
            SetField(component, "dataExcludePatterns", folder.ExcludePatterns);
        }
        return new(component, dialog, warning, parentTask);
    }

    private Task RunNextAsync()
    {
        Assert.That(this.Queue.TryDequeue(out var request), Is.True, "Saving must queue a run.");
        var task = InvokeTask(this.service, "RunDataSourceAsync", request!.DataSourceId, request.RefreshMode, this.lifetime.Token);
        this.runningTasks.Add(task);
        return task;
    }

    private async Task AssertOriginalChunksAsync()
    {
        var index = await this.databases.GetIndexStoreAsync();
        var chunks = await index.SearchChunksAsync(this.source.Id, "indexed", 100, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(chunks.Select(chunk => chunk.ChunkId), Is.EquivalentTo(this.chunkIds));
            Assert.That(chunks.All(chunk => chunk.ChunkText == "Already indexed report"), Is.True);
            Assert.That(chunks.All(chunk => chunk.EmbeddedAtUtc < DateTimeOffset.UtcNow.AddDays(-9)), Is.True, "Updating the app or its paths must not recreate existing chunks.");
        });
    }

    private static string LegacyFingerprint(FileInfo file)
    {
        return LegacyFingerprint(file.FullName, file.Name, file.LastWriteTimeUtc.Ticks, file.Length);
    }

    private static string LegacyFingerprint(string path, string name, long writeTicks, long size)
    {
        var parts = new[] { "file", path, name, writeTicks.ToString(), size.ToString() };
        var serialized = string.Concat(parts.Select(part => $"{part.Length}:{part}|"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }

    // Keep reflection at the fixture boundary: exercise the production queue worker without
    // starting the unrelated application startup loop, and install the isolated database clients.
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static void SetField(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static void Property(object instance, string name, object value)
    {
        for (var type = instance.GetType(); type is not null; type = type.BaseType)
            if (type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } property)
            {
                property.SetValue(instance, value);
                return;
            }
        throw new MissingMemberException(name);
    }

    private static Task InvokeTask(object instance, string name, params object[] arguments) =>
        (Task)instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, arguments)!;

    private sealed record OpenedEdit(object Component, TestDataSourceDialog Dialog, TestDataSourceDialog Warning, Task ParentTask);
}
