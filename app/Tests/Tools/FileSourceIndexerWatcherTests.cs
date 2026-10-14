using AIStudio.Tools;
using AIStudio.Tools.Media;
using AIStudio.Tools.Services.Indexing;

namespace AIStudio.Tests.Tools;

public sealed class FileSourceIndexerWatcherTests
{
    [TestCase(WatcherChangeTypes.Created)]
    [TestCase(WatcherChangeTypes.Changed)]
    [TestCase(WatcherChangeTypes.Deleted)]
    [TestCase(WatcherChangeTypes.Renamed)]
    public void SingleFileWatcherRecognizesEveryChangeToAnUnknownTextExtension(WatcherChangeTypes changeType)
    {
        var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            false,
            TargetFileName: "main.tf");

        Assert.That(
            FileSourceIndexer.IsRelevantWatcherPath(configuration, Path.Combine(Path.GetTempPath(), "main.tf"), changeType),
            Is.True);
    }

    [Test]
    public void SingleFileWatcherIgnoresOtherFilesInTheSameDirectory()
    {
        var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            false,
            TargetFileName: "main.tf");

        Assert.That(
            FileSourceIndexer.IsRelevantWatcherPath(configuration, Path.Combine(Path.GetTempPath(), "other.tf"), WatcherChangeTypes.Changed),
            Is.False);
    }

    [Test]
    public void SingleFileWatcherRecognizesARenameAwayFromTheConfiguredFile()
    {
        var rootPath = Path.GetTempPath();
        var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
            rootPath,
            false,
            TargetFileName: "main.tf");
        var renamed = new RenamedEventArgs(
            WatcherChangeTypes.Renamed,
            rootPath,
            "renamed.tf",
            "main.tf");

        Assert.That(FileSourceIndexer.IsRelevantWatcherEvent(configuration, renamed), Is.True);
    }

    [TestCase("main.tf", WatcherChangeTypes.Created, true)]
    [TestCase("MAIN.TF", WatcherChangeTypes.Changed, true)]
    [TestCase(".gitignore", WatcherChangeTypes.Changed, true)]
    [TestCase("notes.md", WatcherChangeTypes.Created, true)]
    [TestCase("notes.md", WatcherChangeTypes.Changed, false)]
    [TestCase("notes.md", WatcherChangeTypes.Deleted, true)]
    [TestCase("notes.md", WatcherChangeTypes.Renamed, true)]
    public void DirectoryWatcherUsesPatternsAndRefreshesConservativelyAfterRemovals(string fileName, WatcherChangeTypes changeType, bool expected)
    {
        var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            FilePatterns: FileNamePatterns.Parse("*.tf;.gitignore"));

        Assert.That(
            FileSourceIndexer.IsRelevantWatcherPath(configuration, Path.Combine(Path.GetTempPath(), fileName), changeType),
            Is.EqualTo(expected));
    }

    [Test]
    public void DirectoryWatcherRecognizesChangesToSubdirectories()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ai-studio-watcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
                Path.GetTempPath(),
                true,
                FilePatterns: FileNamePatterns.Parse("*.tf"));

            Assert.That(
                FileSourceIndexer.IsRelevantWatcherPath(configuration, directory, WatcherChangeTypes.Created),
                Is.True);
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Test]
    public void ReorderingEquivalentPatternsDoesNotRecreateTheWatcher()
    {
        var left = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            FilePatterns: FileNamePatterns.Parse("*.txt;*.tf"));
        var right = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            FilePatterns: FileNamePatterns.Parse("*.TF;*.TXT"));

        Assert.That(FileSourceIndexer.IsSameWatchConfiguration(left, right), Is.True);
    }

    [Test]
    public void ChangingPatternsReconfiguresTheWatcher()
    {
        var before = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            FilePatterns: FileNamePatterns.Parse("*.txt"));
        var after = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            FilePatterns: FileNamePatterns.Parse("*.txt;*.tf"));

        Assert.That(FileSourceIndexer.IsSameWatchConfiguration(before, after), Is.False);
    }

    [Test]
    public void ChangingWhetherSubdirectoriesAreIncludedReconfiguresTheWatcher()
    {
        var recursive = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            FilePatterns: FileNamePatterns.Parse("*.txt"));
        var topDirectoryOnly = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            false,
            FilePatterns: FileNamePatterns.Parse("*.txt"));

        Assert.That(FileSourceIndexer.IsSameWatchConfiguration(recursive, topDirectoryOnly), Is.False);
    }

    [Test]
    public void ChangingWhetherHiddenSubdirectoriesAreIncludedReconfiguresTheWatcher()
    {
        var visibleDirectoriesOnly = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            false,
            FilePatterns: FileNamePatterns.Parse("*.txt"));
        var includingHiddenDirectories = new FileSourceIndexer.DataSourceWatcherConfiguration(
            Path.GetTempPath(),
            true,
            true,
            FilePatterns: FileNamePatterns.Parse("*.txt"));

        Assert.That(FileSourceIndexer.IsSameWatchConfiguration(visibleDirectoriesOnly, includingHiddenDirectories), Is.False);
    }

    [Test]
    public async Task DirectoryWatcherIgnoresExistingFilesInHiddenSubdirectoriesUnlessTheyAreIncluded()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-watcher-{Guid.NewGuid():N}");
        var hiddenDirectoryPath = Path.Combine(directoryPath, ".hidden");
        var filePath = Path.Combine(hiddenDirectoryPath, "notes.txt");
        Directory.CreateDirectory(hiddenDirectoryPath);

        try
        {
            await File.WriteAllTextAsync(filePath, "Hidden notes");
            var visibleDirectoriesOnly = new FileSourceIndexer.DataSourceWatcherConfiguration(
                directoryPath,
                true,
                false,
                FilePatterns: FileNamePatterns.Parse("*.txt"));
            var includingHiddenDirectories = visibleDirectoriesOnly with { IncludeHiddenSubdirectories = true };

            Assert.Multiple(() =>
            {
                Assert.That(FileSourceIndexer.IsRelevantWatcherPath(visibleDirectoriesOnly, filePath, WatcherChangeTypes.Changed), Is.False);
                Assert.That(FileSourceIndexer.IsRelevantWatcherPath(includingHiddenDirectories, filePath, WatcherChangeTypes.Changed), Is.True);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task DirectoryWatcherIgnoresTheGeneratedTranscriptWhenTheMediaIsSelected()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-transcript-watcher-{Guid.NewGuid():N}");
        var mediaPath = Path.Combine(directoryPath, "recording.mp3");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(mediaPath, [1, 2, 3]);
            var transcriptPath = PersistentMediaTranscript.GetFilePath(mediaPath);
            var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
                directoryPath,
                true,
                FilePatterns: FileNamePatterns.Parse("*"));
            var unrelatedPatterns = configuration with { FilePatterns = FileNamePatterns.Parse("*.tf") };

            Assert.Multiple(() =>
            {
                Assert.That(FileSourceIndexer.IsRelevantWatcherPath(configuration, mediaPath, WatcherChangeTypes.Changed), Is.True);
                Assert.That(FileSourceIndexer.IsRelevantWatcherPath(configuration, transcriptPath, WatcherChangeTypes.Created), Is.False);
                Assert.That(FileSourceIndexer.IsRelevantWatcherPath(unrelatedPatterns, transcriptPath, WatcherChangeTypes.Created), Is.False);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task DirectoryWatcherKeepsTranscriptAsADocumentWhenTheMediaIsNotSelected()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-transcript-document-{Guid.NewGuid():N}");
        var mediaPath = Path.Combine(directoryPath, "recording.mp3");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(mediaPath, [1, 2, 3]);
            var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(
                directoryPath,
                true,
                FilePatterns: FileNamePatterns.Parse("*.md"));

            Assert.That(
                FileSourceIndexer.IsRelevantWatcherPath(configuration, PersistentMediaTranscript.GetFilePath(mediaPath), WatcherChangeTypes.Changed),
                Is.True);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }
}
