using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when an indexing run stops because the embedding settings of its data source, or the
/// documents it selects, changed.
/// </summary>
/// <remarks>
/// A run resolves its data source and its embedding provider once, when it starts, and may then run
/// for hours. A run which carries on after a change of those settings embeds for an index the next
/// run throws away, at a cloud embedding provider for real money. Worse, it keeps sending documents
/// to a provider which was deleted or may no longer read them. A run which carries on after the
/// data source selects other files embeds files which no longer belong to it, while the ones which
/// now do wait for the next run. Stopping a run for nothing is not free either: everything it did
/// so far is done again.
/// </remarks>
[TestFixture]
public sealed class OutdatedRunTests
{
    private const string SELECTION = "patterns::documents:|exclusions:bin/|subfolders:True|hidden:False|max-mb:1024";

    private static readonly DataSourceRunSignature RUN_SIGNATURE = new("2|b0a4c4d2|OPEN_AI|text-embedding-3-small|512|100", SELECTION);

    private static readonly DataSourceRunSignature OTHER_SIGNATURE = new("2|b0a4c4d2|OPEN_AI|text-embedding-3-large|512|100", SELECTION);

    [Test]
    public void ARunWhichIsStillPreparingCarriesOn()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceEmbeddingService.IsRunOutdated(null, OTHER_SIGNATURE), Is.False, "The run has not resolved its embedding provider yet, so it has embedded nothing so far.");
            Assert.That(DataSourceEmbeddingService.IsRunOutdated(null, DataSourceRunSignature.NONE), Is.False, "The run has not resolved its embedding provider yet, so it has sent nothing anywhere so far.");
        }
    }

    [Test]
    public void ARunForTheCurrentSettingsCarriesOn()
    {
        var sameSignature = new DataSourceRunSignature(RUN_SIGNATURE.EmbeddingSignature, RUN_SIGNATURE.SelectionSignature);
        Assert.That(DataSourceEmbeddingService.IsRunOutdated(RUN_SIGNATURE, sameSignature), Is.False, "Nothing the vectors depend on changed, and neither did the documents, so stopping the run would only repeat its work.");
    }

    [Test]
    public void ARunForOtherSettingsStops()
    {
        Assert.That(DataSourceEmbeddingService.IsRunOutdated(RUN_SIGNATURE, OTHER_SIGNATURE), Is.True, "The next run throws away whatever this run embeds.");
    }

    [Test]
    public void ARunForAnotherSelectionStops()
    {
        var otherSelection = RUN_SIGNATURE with { SelectionSignature = SELECTION.Replace(":documents:", "*.md", StringComparison.Ordinal) };
        Assert.That(DataSourceEmbeddingService.IsRunOutdated(RUN_SIGNATURE, otherSelection), Is.True, "The run works with files which no longer belong to the data source.");
    }

    [TestCase("file patterns")]
    [TestCase("exclusions")]
    [TestCase("size limit")]
    [TestCase("subfolders")]
    [TestCase("hidden files and folders")]
    public void AFolderWhichSelectsOtherFilesStopsTheRunButKeepsItsIndex(string change)
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var changedDataSource = change switch
        {
            "file patterns" => dataSource with { FilePatterns = "*.md" },
            "exclusions" => dataSource with { ExcludePatterns = "node_modules/" },
            "size limit" => dataSource with { MaxFileSizeMegabytes = 1 },
            "subfolders" => dataSource with { IncludeSubdirectories = !dataSource.IncludeSubdirectories },
            "hidden files and folders" => dataSource with { IncludeHiddenFilesAndFolders = !dataSource.IncludeHiddenFilesAndFolders },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };

        var embeddingProvider = TestEmbeddingProvider();
        var runSignature = DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.MEDIUM);
        var currentRunSignature = DataSourceEmbeddingService.GetRunSignature(changedDataSource, embeddingProvider, ConfidenceLevel.MEDIUM);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceEmbeddingService.IsRunOutdated(runSignature, currentRunSignature), Is.True, "The run works with files which no longer belong to the data source, or misses the ones which now do.");
            Assert.That(currentRunSignature.EmbeddingSignature, Is.EqualTo(runSignature.EmbeddingSignature), "Every vector stored so far stays valid, so the index must not be built anew.");
        }
    }

    [TestCase("name and description")]
    [TestCase("order of the file patterns")]
    [TestCase("case of the file patterns")]
    [TestCase("spaces in the file patterns")]
    [TestCase("order and case of the exclusions")]
    public void AFolderWhichSelectsTheSameFilesKeepsTheRun(string change)
    {
        var dataSource = DataSource(ConfidenceLevel.LOW) with { FilePatterns = "*.md;*.pdf", ExcludePatterns = "bin/;obj/" };
        var changedDataSource = change switch
        {
            "name and description" => dataSource with { Name = "Other name", Description = "Other description" },
            "order of the file patterns" => dataSource with { FilePatterns = "*.pdf;*.md" },
            "case of the file patterns" => dataSource with { FilePatterns = "*.MD;*.PDF" },
            "spaces in the file patterns" => dataSource with { FilePatterns = " *.md ;  *.pdf " },
            "order and case of the exclusions" => dataSource with { ExcludePatterns = "OBJ/\nbin/" },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };

        var embeddingProvider = TestEmbeddingProvider();
        Assert.That(
            DataSourceEmbeddingService.GetRunSignature(changedDataSource, embeddingProvider, ConfidenceLevel.MEDIUM),
            Is.EqualTo(DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.MEDIUM)),
            "File patterns and exclusions ignore case and order, so the run works with the same files as before.");
    }

    [Test]
    public void RelocatingASingleFileStopsTheOldRunWithoutInvalidatingItsEmbeddings()
    {
        var dataSource = new DataSourceLocalFile
        {
            Num = 1,
            Id = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
            Name = "Test file",
            Type = DataSourceType.LOCAL_FILE,
            EmbeddingId = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01",
            ConfidenceLevel = ConfidenceLevel.LOW,
            FilePath = "/tmp/test-data/report.pdf",
        };

        var before = DataSourceEmbeddingService.GetRunSignature(dataSource, TestEmbeddingProvider(), ConfidenceLevel.MEDIUM);
        var after = DataSourceEmbeddingService.GetRunSignature(dataSource with { FilePath = "/tmp/moved/report.pdf" }, TestEmbeddingProvider(), ConfidenceLevel.MEDIUM);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceEmbeddingService.IsRunOutdated(before, after), Is.True);
            Assert.That(after.EmbeddingSignature, Is.EqualTo(before.EmbeddingSignature));
        }
    }

    [Test]
    public void RelocatingAFolderStopsTheOldRunWithoutInvalidatingItsEmbeddings()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var before = DataSourceEmbeddingService.GetRunSignature(dataSource, TestEmbeddingProvider(), ConfidenceLevel.MEDIUM);
        var after = DataSourceEmbeddingService.GetRunSignature(dataSource with { Path = "/tmp/moved" }, TestEmbeddingProvider(), ConfidenceLevel.MEDIUM);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataSourceEmbeddingService.IsRunOutdated(before, after), Is.True);
            Assert.That(after.EmbeddingSignature, Is.EqualTo(before.EmbeddingSignature));
        }
    }

    [Test]
    public void ARunWhichWouldNotStartAnymoreStops()
    {
        Assert.That(DataSourceEmbeddingService.IsRunOutdated(RUN_SIGNATURE, DataSourceRunSignature.NONE), Is.True, "The embedding provider is gone or may no longer read the data source, so the run must not send it anything else.");
    }

    [Test]
    public void AnAllowedProviderGetsARunWithTheEmbeddingSignature()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var embeddingProvider = TestEmbeddingProvider();

        Assert.That(
            DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.MEDIUM).EmbeddingSignature,
            Is.EqualTo(DataSourceEmbeddingService.BuildEmbeddingSignature(dataSource, embeddingProvider)),
            "A run counts as outdated exactly when the stored index would be thrown away.");
    }

    [Test]
    public void ChangingTheConfidenceOfAnAllowedProviderKeepsTheRun()
    {
        var dataSource = DataSource(ConfidenceLevel.LOW);
        var embeddingProvider = TestEmbeddingProvider();

        Assert.That(
            DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.HIGH),
            Is.EqualTo(DataSourceEmbeddingService.GetRunSignature(dataSource, embeddingProvider, ConfidenceLevel.MEDIUM)),
            "The provider may still read the data source, and the confidence level changes no vector.");
    }

    [Test]
    public void AProviderBelowTheLevelOfTheDataSourceGetsNoRun()
    {
        var dataSource = DataSource(ConfidenceLevel.HIGH);

        Assert.That(DataSourceEmbeddingService.GetRunSignature(dataSource, TestEmbeddingProvider(), ConfidenceLevel.LOW), Is.EqualTo(DataSourceRunSignature.NONE), "A run must not send the documents of the data source to a provider which may not read them.");
    }

    [Test]
    public void NoProviderGetsARunForAMailboxWithoutAValidLevel()
    {
        var mailbox = new DataSourceMailbox { ConfidenceLevel = ConfidenceLevel.NONE };

        Assert.That(DataSourceEmbeddingService.GetRunSignature(mailbox, TestEmbeddingProvider(), ConfidenceLevel.HIGH), Is.EqualTo(DataSourceRunSignature.NONE), "The run asked the rule of the other data sources, which lets every provider read a source without a level.");
    }

    private static DataSourceLocalDirectory DataSource(ConfidenceLevel confidenceLevel) => new()
    {
        Num = 1,
        Id = "6f1d6a4e-6a5e-4c62-9a4f-0f2d2c8b7a11",
        Name = "Test data",
        Description = "Documents used by the tests.",
        Type = DataSourceType.LOCAL_DIRECTORY,
        EmbeddingId = "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01",
        MaxChunkTokenLength = 512,
        ChunkOverlapTokenLength = 100,
        ConfidenceLevel = confidenceLevel,
        Path = "/tmp/test-data",
    };

    private static EmbeddingProvider TestEmbeddingProvider() =>
        new(1, "b0a4c4d2-1f3e-4f0a-8c9d-5a6b7c8d9e01", "Test embeddings", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"));
}