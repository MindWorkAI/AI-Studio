using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.Databases;
using AIStudio.Tools;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
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
        this.service = new DataSourceEmbeddingService(this.settings, null!, this.databases, guard, null!, NullLogger<DataSourceEmbeddingService>.Instance);
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

    private async Task SeedLegacyIndexAsync(bool singleFile)
    {
        var root = Path.Combine(this.testRoot, "original");
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        var id = Guid.NewGuid().ToString();
        var folder = new DataSourceLocalDirectory
        {
            Id = id, Name = "Existing documents", Type = DataSourceType.LOCAL_DIRECTORY, Path = root,
            EmbeddingId = EMBEDDING_ID, ConfidenceLevel = ConfidenceLevel.NONE, MaxChunkTokenLength = 512, ChunkOverlapTokenLength = 100,
            FilePatterns = "*.md", ExcludePatterns = string.Empty,
        };
        this.source = singleFile ? new DataSourceLocalFile
        {
            Id = id, Name = folder.Name, Type = DataSourceType.LOCAL_FILE, FilePath = Path.Combine(root, "notes.md"),
            EmbeddingId = EMBEDDING_ID, ConfidenceLevel = ConfidenceLevel.NONE, MaxChunkTokenLength = 512, ChunkOverlapTokenLength = 100,
        } : folder;
        this.settings.ConfigurationData.DataSources.Add(this.source);
        await this.store.Client.UpsertDataSourceAsync(id, this.source.Type.ToString(), EMBEDDING_ID, LEGACY_SIGNATURE, "legacy-source-hash", 3, CancellationToken.None);

        var paths = singleFile ? new[] { "notes.md" } : new[] { "notes.md", Path.Combine("nested", "report.md") };
        foreach (var relative in paths)
        {
            var path = Path.Combine(root, relative);
            await File.WriteAllTextAsync(path, "Already indexed report");
            var file = new FileInfo(path);
            var fingerprint = LegacyFingerprint(file);
            var parentId = IndexedDocumentIds.CreateParentId(id, path);
            var chunkId = IndexedDocumentIds.CreateChunkId(id, fingerprint, 0);
            var embeddedAt = DateTimeOffset.UtcNow.AddDays(-10);
            await this.store.Client.UpsertFileAsync(id, new EmbeddingStateFile(parentId, path, file.Name, relative, "md", fingerprint, file.Length,
                new DateTimeOffset(file.CreationTimeUtc), new DateTimeOffset(file.LastWriteTimeUtc), embeddedAt, 1), CancellationToken.None);
            await this.store.Client.UpsertChunksAsync(id, [new EmbeddingStateChunk(chunkId, parentId, 1, 0, "Already indexed report", embeddedAt)], CancellationToken.None);
            this.vectors.Locations.Add(parentId, path);
            this.chunkIds.Add(chunkId);
        }
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
            Assert.That(edit.Warning.Parameters!.Get<string>(nameof(ConfirmDialog.MarkdownBody)), Does.Contain("from the index").And.Contain("files on disk are not deleted").And.Contain("may need indexing").And.Contain("Unchanged files keep"));
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
        await using var context = this.store.CreateContext();
        var chunks = await context.EmbeddingChunks.AsNoTracking().ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(chunks.Select(chunk => chunk.ChunkId), Is.EquivalentTo(this.chunkIds));
            Assert.That(chunks.All(chunk => chunk.ChunkText == "Already indexed report"), Is.True);
            Assert.That(chunks.All(chunk => chunk.EmbeddedAtUtc < DateTimeOffset.UtcNow.AddDays(-9)), Is.True, "Updating the app or its paths must not recreate existing chunks.");
        });
    }

    private static string LegacyFingerprint(FileInfo file)
    {
        var parts = new[] { "file", file.FullName, file.Name, file.LastWriteTimeUtc.Ticks.ToString(), file.Length.ToString() };
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
