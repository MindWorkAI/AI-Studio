using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the size limit for files, which data sources and the batch processing assistant share.
/// </summary>
public sealed class FileSizeLimitTests
{
    private const int LIMIT_MEGABYTES = 1;

    private string testRoot = string.Empty;

    [SetUp]
    public void CreateTestRoot()
    {
        this.testRoot = Path.Combine(Path.GetTempPath(), $"ai-studio-size-limit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testRoot);
    }

    [TearDown]
    public void DeleteTestRoot()
    {
        if (Directory.Exists(this.testRoot))
            Directory.Delete(this.testRoot, true);
    }

    [Test]
    public void SettingsFromBeforeTheLimitGetTheDefault()
    {
        var dataSource = JsonSerializer.Deserialize<DataSourceLocalDirectory>("{}", SettingsManager.JSON_OPTIONS);
        var batchSettings = JsonSerializer.Deserialize<DataBatchProcessing>("{}", SettingsManager.JSON_OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(dataSource.MaxFileSizeMegabytes, Is.EqualTo(FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES));
            Assert.That(batchSettings!.MaxFileSizeMegabytes, Is.EqualTo(FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES));
        });
    }

    [TestCase(0)]
    [TestCase(-5)]
    public void AnInvalidStoredLimitFallsBackToTheDefault(int storedLimit)
    {
        var dataSource = new DataSourceLocalDirectory { MaxFileSizeMegabytes = storedLimit };

        Assert.That(dataSource.EffectiveMaxFileSizeMegabytes, Is.EqualTo(FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES), "There is no value for no limit.");
    }

    [Test]
    public void AFileOfExactlyTheLimitIsNotTooLarge()
    {
        var limitBytes = FolderFileSelection.MegabytesToBytes(LIMIT_MEGABYTES);
        var atTheLimit = this.CreateFile("at-the-limit.txt", limitBytes);
        var aboveTheLimit = this.CreateFile("above-the-limit.txt", limitBytes + 1);
        var selection = new FolderFileSelection(["*"], [], true, false, LIMIT_MEGABYTES);

        Assert.Multiple(() =>
        {
            Assert.That(selection.IsTooLarge(new FileInfo(atTheLimit)), Is.False);
            Assert.That(selection.IsTooLarge(new FileInfo(aboveTheLimit)), Is.True);
        });
    }

    [Test]
    public void AFolderDataSourceReportsATooLargeFileInsteadOfIndexingIt()
    {
        var smallFile = this.CreateFile("notes.txt", FolderFileSelection.MegabytesToBytes(LIMIT_MEGABYTES));
        var largeFile = this.CreateFile("dump.txt", FolderFileSelection.MegabytesToBytes(LIMIT_MEGABYTES) + 1);
        var dataSource = new DataSourceLocalDirectory
        {
            Type = DataSourceType.LOCAL_DIRECTORY,
            Path = this.testRoot,
            FilePatterns = "*.txt",
            MaxFileSizeMegabytes = LIMIT_MEGABYTES,
        };

        var result = CreateIndexer().GetInputFiles(dataSource);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files.Select(file => file.FullName), Is.EqualTo(new[] { smallFile }));
            Assert.That(result.IgnoredFiles, Is.EqualTo(new[] { new DataSourceEmbeddingIgnoredFile(largeFile, DataSourceEmbeddingIgnoreReason.TOO_LARGE) }), "The user selected the file, so the embedding page has to say why it is missing.");
        });
    }

    [Test]
    public void ASingleFileDataSourceHasNoSizeLimit()
    {
        var largeFile = this.CreateFile("dump.txt", FolderFileSelection.MegabytesToBytes(FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES) + 1);
        var dataSource = new DataSourceLocalFile
        {
            Type = DataSourceType.LOCAL_FILE,
            FilePath = largeFile,
        };

        var result = CreateIndexer().GetInputFiles(dataSource);

        Assert.Multiple(() =>
        {
            Assert.That(result.Files.Select(file => file.FullName), Is.EqualTo(new[] { largeFile }), "The user chose this very file.");
            Assert.That(result.IgnoredFiles, Is.Empty);
        });
    }

    [Test]
    public async Task DirectoryStatisticsLeaveOutTooLargeFiles()
    {
        this.CreateFile("notes.txt", FolderFileSelection.MegabytesToBytes(LIMIT_MEGABYTES));
        this.CreateFile("dump.txt", FolderFileSelection.MegabytesToBytes(LIMIT_MEGABYTES) + 1);
        long fileCount = 0;

        await new DirectoryInfo(this.testRoot).DetermineContentSize(
            _ => { },
            count => fileCount = count,
            _ => { },
            selection: new FolderFileSelection(["*"], [], true, false, LIMIT_MEGABYTES));

        Assert.That(fileCount, Is.EqualTo(1), "The information dialog counts the files which get indexed.");
    }

    /// <remarks>
    /// Setting the length writes no content, so a file of a gigabyte costs no time and no disk space
    /// on file systems which support sparse files.
    /// </remarks>
    private string CreateFile(string fileName, long length)
    {
        var filePath = Path.Combine(this.testRoot, fileName);
        using var stream = File.Create(filePath);
        stream.SetLength(length);
        return filePath;
    }

    // Finding the files reads none of them, so it needs neither the runtime nor the chunker:
    private static FileSourceIndexer CreateIndexer() => new(null!, null!, null!, NullLogger.Instance);
}