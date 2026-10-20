using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Security;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that a local data source keeps its index while its file or one of its folders cannot be reached.
/// </summary>
/// <remarks>
/// A drive which is not connected, or a network share while the VPN is down, looks like an empty
/// folder to anyone who only lists files. Read that way, every embedding of the data source was
/// removed, and once the folder was back, all of it was embedded again, which takes hours for a
/// large folder. The vector store of these tests refuses every removal, so a run which removes
/// anything fails right away.
/// </remarks>
[TestFixture]
public sealed class UnreachableDataSourceTests
{
    private const string DATA_SOURCE_ID = "3c9e2f1a-7b4d-4e8a-9f6c-1d2e3f4a5b60";

    private const string EMBEDDING_ID = "8a7b6c5d-4e3f-4a1b-9c8d-7e6f5a4b3c21";

    private const string STORED_SOURCE_HASH = "STORED-SOURCE-HASH";

    private string testRoot = string.Empty;

    private readonly List<string> lockedDirectories = [];

    [SetUp]
    public void CreateTestRoot()
    {
        this.testRoot = Path.Combine(Path.GetTempPath(), $"ai-studio-unreachable-{Guid.NewGuid():N}");
        this.lockedDirectories.Clear();
    }

    [TearDown]
    public void DeleteTestRoot()
    {
        if (!OperatingSystem.IsWindows())
            foreach (var directory in this.lockedDirectories)
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        if (Directory.Exists(this.testRoot))
            Directory.Delete(this.testRoot, true);
    }

    [Test]
    public async Task AMissingFolderKeepsItsIndex()
    {
        var folder = Path.Combine(this.testRoot, "documents");
        var (outcome, statuses, manifest) = await RunAsync(Folder(folder), Path.Combine(folder, "report.pdf"), Path.Combine(folder, "notes.docx"));

        AssertTheIndexWasKept(outcome, statuses, manifest, 2, folder);
    }

    [Test]
    public async Task AMissingRelocationTargetKeepsTheOriginalIndex()
    {
        var folder = Path.Combine(this.testRoot, "moved-but-unavailable");
        var (outcome, statuses, manifest) = await RunAsync(Folder(folder), Path.Combine(this.testRoot, "original", "report.pdf"));

        AssertTheIndexWasKept(outcome, statuses, manifest, 1, folder);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AReadableEmptyMountDirectoryKeepsItsIndex(bool emptySubfolder)
    {
        var folder = Path.Combine(this.testRoot, "mounted-drive");
        Directory.CreateDirectory(emptySubfolder ? Path.Combine(folder, "documents") : folder);
        var unfinishedFile = Path.Combine(folder, "unfinished.pdf");

        var (outcome, statuses, manifest) = await RunAsync(Folder(folder), [Path.Combine(folder, "report.pdf")], [unfinishedFile]);

        AssertTheIndexWasKept(outcome, statuses, manifest, 1, folder);
        Assert.Multiple(() =>
        {
            Assert.That(manifest.UnfinishedFiles, Does.Contain(unfinishedFile), "An unmounted drive must not discard interrupted work either.");
            Assert.That(statuses[^1].LastError, Does.Contain("appears empty").And.Contain("remove this data source"));
        });
    }

    [Test]
    public async Task ANewEmptyFolderCanFinishWithoutAnIndex()
    {
        var folder = Path.Combine(this.testRoot, "new-empty-folder");
        Directory.CreateDirectory(folder);

        var (outcome, statuses, manifest) = await RunAsync(Folder(folder));

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(IndexedRunOutcome.DONE));
            Assert.That(statuses[^1].State, Is.EqualTo(DataSourceEmbeddingState.COMPLETED));
            Assert.That(manifest.Files, Is.Empty);
        });
    }

    [Test]
    public async Task AMissingFileKeepsItsIndex()
    {
        var file = Path.Combine(this.testRoot, "report.pdf");
        var dataSource = new DataSourceLocalFile
        {
            Num = 1,
            Id = DATA_SOURCE_ID,
            Name = "Test file",
            Type = DataSourceType.LOCAL_FILE,
            EmbeddingId = EMBEDDING_ID,
            ConfidenceLevel = ConfidenceLevel.LOW,
            FilePath = file,
        };

        var (outcome, statuses, manifest) = await RunAsync(dataSource, file);

        AssertTheIndexWasKept(outcome, statuses, manifest, 1, file);
    }

    [Test]
    public async Task AFolderWhichCannotBeOpenedKeepsItsIndex()
    {
        var folder = Path.Combine(this.testRoot, "documents");
        Directory.CreateDirectory(folder);
        this.TakeAwayTheRightToRead(folder);

        var (outcome, statuses, manifest) = await RunAsync(Folder(folder), Path.Combine(folder, "report.pdf"));

        AssertTheIndexWasKept(outcome, statuses, manifest, 1, folder);
    }

    [Test]
    public async Task TheFilesOfASubfolderWhichCannotBeOpenedKeepTheirIndex()
    {
        var folder = Path.Combine(this.testRoot, "documents");
        var subfolder = Path.Combine(folder, "contracts");
        var fileInSubfolder = Path.Combine(subfolder, "contract.pdf");
        var unfinishedFileInSubfolder = Path.Combine(subfolder, "offer.pdf");
        Directory.CreateDirectory(subfolder);
        this.TakeAwayTheRightToRead(subfolder);

        var (outcome, statuses, manifest) = await RunAsync(Folder(folder), [fileInSubfolder], [unfinishedFileInSubfolder]);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(IndexedRunOutcome.DONE));
            Assert.That(manifest.Files.Keys, Is.EquivalentTo([fileInSubfolder]), "The file is out of reach, not gone. Once the subfolder can be read again, it is still indexed.");
            Assert.That(manifest.UnfinishedFiles, Is.EquivalentTo([unfinishedFileInSubfolder]), "A file whose indexing never came to an end is out of reach as well. Once the subfolder can be read again, indexing it replaces its leftovers.");
            Assert.That(statuses[^1].Failures.Select(failure => failure.DocumentKey), Has.Member(subfolder), "The user has to learn which subfolder could not be read.");
        });
    }

    [Test]
    public void OnlyFilesWhichAreGoneAreRemoved()
    {
        var folder = Path.Combine(this.testRoot, "documents");
        var found = Path.Combine(folder, "found.pdf");
        var deleted = Path.Combine(folder, "deleted.pdf");
        var subfolder = Path.Combine(folder, "contracts");
        var inSubfolder = Path.Combine(subfolder, "2026", "contract.pdf");
        var besideSubfolder = Path.Combine(folder, "contracts-old", "contract.pdf");

        var goneFiles = FileSourceIndexer.GetGoneFiles([found, deleted, inSubfolder, besideSubfolder], [new FileInfo(found)], [subfolder]);

        Assert.That(goneFiles, Is.EquivalentTo([deleted, besideSubfolder]), "A folder whose name only begins like the one which could not be read is no part of it.");
    }

    [Test]
    public void AnUnreadableDriveRootKeepsItsIndexedFiles()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;
        var file = Path.Combine(root, "documents", "notes.md");

        Assert.That(FileSourceIndexer.GetGoneFiles([file], [], [root]), Is.Empty, "A drive root already ends in its separator; adding another would fail to protect its files.");
    }

    private static DataSourceLocalDirectory Folder(string path) => new()
    {
        Num = 1,
        Id = DATA_SOURCE_ID,
        Name = "Test folder",
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = EMBEDDING_ID,
        ConfidenceLevel = ConfidenceLevel.LOW,
        Path = path,
    };

    private static void AssertTheIndexWasKept(IndexedRunOutcome outcome, List<DataSourceEmbeddingStatus> statuses, DataSourceEmbeddingManifest manifest, int indexedFiles, string unreachablePath)
    {
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(IndexedRunOutcome.DONE), "Nothing can be done until the file or the folder is back.");
            Assert.That(manifest.Files, Has.Count.EqualTo(indexedFiles), "Embedding everything again once the file or the folder is back costs hours.");
            Assert.That(manifest.PermanentFailures, Has.Count.EqualTo(1), "A file skipped for good would otherwise be read again, only to fail the same way.");
            Assert.That(manifest.SourceHash, Is.EqualTo(STORED_SOURCE_HASH), "The stored hash tells which state of the data source the index holds.");
            Assert.That(statuses[^1].State, Is.EqualTo(DataSourceEmbeddingState.FAILED));
            Assert.That(statuses[^1].IndexedDocuments, Is.EqualTo(indexedFiles), "The index still holds these files, and the page has to say so.");
            Assert.That(statuses[^1].LastError, Does.Contain(unreachablePath));
        });
    }

    private void TakeAwayTheRightToRead(string directory)
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("Taking away the right to read a folder needs an access control list on Windows.");
        else
            File.SetUnixFileMode(directory, UnixFileMode.None);

        this.lockedDirectories.Add(directory);
        try
        {
            _ = Directory.EnumerateFiles(directory).Any();
            Assert.Ignore("This account may read every folder, e.g. as root, so no folder can be locked.");
        }
        catch (UnauthorizedAccessException)
        {
            // The folder is locked, as intended.
        }
    }

    private static Task<RunResult> RunAsync<TDataSource>(TDataSource dataSource, params string[] indexedFiles) where TDataSource : IDataSource, IIndexedDataSource => RunAsync(dataSource, indexedFiles, []);

    private static async Task<RunResult> RunAsync<TDataSource>(TDataSource dataSource, string[] indexedFiles, string[] unfinishedFiles) where TDataSource : IDataSource, IIndexedDataSource
    {
        var statuses = new List<DataSourceEmbeddingStatus>();
        var manifest = new DataSourceEmbeddingManifest { SourceHash = STORED_SOURCE_HASH };
        foreach (var file in indexedFiles)
            manifest.Files[file] = new EmbeddedFileRecord("FINGERPRINT", 1024, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 4,
                RelativePath: Path.GetFileName(file), ParentFileId: IndexedDocumentIds.CreateParentId(dataSource.Id, file));

        manifest.UnfinishedFiles.UnionWith(unfinishedFiles);

        if (indexedFiles.Length > 0)
        {
            var scannedFile = Path.Combine(Path.GetDirectoryName(indexedFiles[0])!, "scan.pdf");
            manifest.PermanentFailures[scannedFile] = new PermanentIndexingFailureRecord("FINGERPRINT", FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text could be read.", DateTimeOffset.UnixEpoch);
        }

        var embeddingProvider = new EmbeddingProvider(1, EMBEDDING_ID, "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        var context = new IndexedRunContext(
            dataSource,
            embeddingProvider,
            embeddingProvider.CreateProvider(),
            new NoVectorStoreClient("Test vector store", "Not available in tests."),
            new NoIndexStoreClient("Test index store", "Not available in tests."),
            manifest,
            new RunShare(RunShare.DEFAULT_DURATION),
            new IndexingPass(new DataSourceRunSignature("TEST-EMBEDDING-SIGNATURE", string.Empty)),
            new PromptInjectionGuardService(null!, settingsManager, NullLogger<PromptInjectionGuardService>.Instance, NullLoggerFactory.Instance).BeginAction(),
            settingsManager,
            statuses.Add,
            NullLogger.Instance);

        // A run which reaches no file reads none, so it needs neither the runtime nor the chunker:
        var indexer = new FileSourceIndexer(settingsManager, null!, null!, null!, NullLogger.Instance);
        var outcome = await indexer.ProcessAsync(context, DataSourceEmbeddingRefreshMode.HASH_CHECK, CancellationToken.None);

        return new(outcome, statuses, manifest);
    }

    private sealed record RunResult(IndexedRunOutcome Outcome, List<DataSourceEmbeddingStatus> Statuses, DataSourceEmbeddingManifest Manifest);
}