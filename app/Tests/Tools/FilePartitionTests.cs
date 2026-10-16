using AIStudio.Tools;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how a run of the file indexer sorts the files of a folder before it reads the first one.
/// </summary>
/// <remarks>
/// A large folder is indexed in steps of a few minutes, and every step lists the folder anew. What a
/// step counts up front is what its progress starts with: a file counted as to be read lets the bar
/// jump back with every step, and costs the time to read it once more. A file left out wrongly is
/// never indexed at all.
/// </remarks>
[TestFixture]
public sealed class FilePartitionTests
{
    private static readonly FileInfo UNCHANGED = new("/tmp/test-data/unchanged.pdf");
    private static readonly FileInfo CHANGED = new("/tmp/test-data/changed.pdf");
    private static readonly FileInfo ADDED = new("/tmp/test-data/added.pdf");
    private static readonly FileInfo SCANNED = new("/tmp/test-data/scanned.pdf");
    private static readonly FileInfo SCANNED_AND_RECOGNIZED = new("/tmp/test-data/scanned-and-recognized.pdf");
    private static readonly FileInfo FAILED_EARLIER = new("/tmp/test-data/failed-earlier.pdf");
    private static readonly FileInfo CHANGED_SINCE_IT_FAILED = new("/tmp/test-data/changed-since-it-failed.pdf");

    private static readonly DataSourceEmbeddingFailure EARLIER_FAILURE = new(FAILED_EARLIER.FullName, "The file changed while it was being indexed.", DateTimeOffset.UnixEpoch);

    private FilePartition partition = null!;

    [SetUp]
    public void SortTheFiles()
    {
        var files = new[] { UNCHANGED, CHANGED, ADDED, SCANNED, SCANNED_AND_RECOGNIZED, FAILED_EARLIER, CHANGED_SINCE_IT_FAILED };
        var fingerprints = files.ToDictionary(file => file.FullName, file => $"NOW:{file.Name}", StringComparer.OrdinalIgnoreCase);

        var manifest = new DataSourceEmbeddingManifest();
        manifest.Files[UNCHANGED.FullName] = Indexed($"NOW:{UNCHANGED.Name}");
        manifest.Files[CHANGED.FullName] = Indexed("BEFORE");
        manifest.PermanentFailures[SCANNED.FullName] = Unreadable($"NOW:{SCANNED.Name}");
        manifest.PermanentFailures[SCANNED_AND_RECOGNIZED.FullName] = Unreadable("BEFORE-OCR");

        var pass = new IndexingPass(new DataSourceRunSignature("EMBEDDING-SIGNATURE"));
        pass.RecordFailure(FAILED_EARLIER.FullName, $"NOW:{FAILED_EARLIER.Name}", EARLIER_FAILURE);
        pass.RecordFailure(CHANGED_SINCE_IT_FAILED.FullName, "BEFORE", EARLIER_FAILURE with { DocumentKey = CHANGED_SINCE_IT_FAILED.FullName });

        this.partition = FileSourceIndexer.PartitionFiles(files, fingerprints, manifest, pass);
    }

    [Test]
    public void UnchangedFilesAreCountedButNotRead() =>
        Assert.That(this.partition.UnchangedCount, Is.EqualTo(1));

    [Test]
    public void FilesSkippedForGoodAreNotReadUntilTheyChange() =>
        Assert.That(this.partition.StillUnreadable.Select(file => file.Key), Is.EqualTo(new[] { SCANNED.FullName }), "An OCR run over a scanned document changes it, which is exactly when it deserves another attempt.");

    [Test]
    public void FilesWhichFailedEarlierInThePassAreNotReadUntilTheyChange() =>
        Assert.That(this.partition.FailedEarlierInPass, Is.EqualTo(new[] { EARLIER_FAILURE }), "The failure keeps its place in the list, or the list loses what the steps before found.");

    [Test]
    public void EveryOtherFileIsReadInTheOrderOfTheFolder() =>
        Assert.That(this.partition.ToRead.Select(file => file.FullName), Is.EqualTo(new[] { CHANGED.FullName, ADDED.FullName, SCANNED_AND_RECOGNIZED.FullName, CHANGED_SINCE_IT_FAILED.FullName }));

    private static EmbeddedFileRecord Indexed(string fingerprint) => new(fingerprint, 1024, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 4);

    private static PermanentIndexingFailureRecord Unreadable(string fingerprint) => new(fingerprint, FileExtractionErrorCode.NO_TEXT_EXTRACTED, "No text could be read.", DateTimeOffset.UnixEpoch);
}