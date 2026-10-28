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
    public void SingleFileWatcherIgnoresAnotherCaseDistinctFileOnUnix()
    {
        var configuration = new FileSourceIndexer.DataSourceWatcherConfiguration(Path.GetTempPath(), false, TargetFileName: "Report.md");

        Assert.That(FileSourceIndexer.IsRelevantWatcherPath(configuration, Path.Combine(Path.GetTempPath(), "report.md"), WatcherChangeTypes.Changed),
            Is.EqualTo(OperatingSystem.IsWindows()));
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
    [TestCase("notes.md", WatcherChangeTypes.Created, false)]
    [TestCase("notes.md", WatcherChangeTypes.Changed, false)]
    [TestCase("notes.md", WatcherChangeTypes.Deleted, false)]
    [TestCase("notes.md", WatcherChangeTypes.Renamed, false)]
    [TestCase("docs", WatcherChangeTypes.Deleted, true)]
    [TestCase("docs", WatcherChangeTypes.Renamed, true)]
    public void DirectoryWatcherRefreshesOnlyForWhatCanBeInTheIndex(string fileName, WatcherChangeTypes changeType, bool expected)
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
                FilePatterns: FileNamePatterns.Parse("*.txt"));
            var includingHiddenDirectories = visibleDirectoriesOnly with { IncludeHiddenFilesAndFolders = true };

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

    [TestCase(WatcherChangeTypes.Created)]
    [TestCase(WatcherChangeTypes.Changed)]
    [TestCase(WatcherChangeTypes.Deleted)]
    public void DirectoryWatcherIgnoresHiddenFilesUnlessAPatternNamesThem(WatcherChangeTypes changeType)
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-file-watcher-{Guid.NewGuid():N}");
        var environmentPath = Path.Combine(directoryPath, ".env");
        var everything = new FileSourceIndexer.DataSourceWatcherConfiguration(
            directoryPath,
            true,
            FilePatterns: FileNamePatterns.Parse("*"));
        var namingTheFile = everything with { FilePatterns = FileNamePatterns.Parse("*;.env") };
        var includingHiddenFiles = everything with { IncludeHiddenFilesAndFolders = true };

        Assert.Multiple(() =>
        {
            Assert.That(FileSourceIndexer.IsRelevantWatcherPath(everything, environmentPath, changeType), Is.False, "A hidden file is no part of the data source, so changing it needs no run.");
            Assert.That(FileSourceIndexer.IsRelevantWatcherPath(namingTheFile, environmentPath, changeType), Is.True);
            Assert.That(FileSourceIndexer.IsRelevantWatcherPath(includingHiddenFiles, environmentPath, changeType), Is.True);
        });
    }

    [Test]
    public void GitRenamingItsIndexStartsNoRun()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-git-watcher-{Guid.NewGuid():N}");
        var renamed = new RenamedEventArgs(WatcherChangeTypes.Renamed, Path.Combine(rootPath, ".git"), "index", "index.lock");

        Assert.That(FileSourceIndexer.IsRelevantWatcherEvent(DefaultConfiguration(rootPath), renamed), Is.False, "Git does this with almost every command.");
    }

    [TestCase("bin/app.dll", WatcherChangeTypes.Created)]
    [TestCase("src/obj/Debug/app.pdb", WatcherChangeTypes.Changed)]
    [TestCase("node_modules/left-pad/index.js", WatcherChangeTypes.Changed)]
    [TestCase("node_modules/left-pad/index.js", WatcherChangeTypes.Deleted)]
    [TestCase("package-lock.json", WatcherChangeTypes.Changed)]
    [TestCase("Thumbs.db", WatcherChangeTypes.Created)]
    public void ChangesToExcludedFilesAndFoldersStartNoRun(string relativePath, WatcherChangeTypes changeType)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-excluded-watcher-{Guid.NewGuid():N}");
        var path = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var configuration = DefaultConfiguration(rootPath) with { FilePatterns = FileNamePatterns.Parse("*") };

        Assert.That(FileSourceIndexer.IsRelevantWatcherPath(configuration, path, changeType), Is.False);
    }

    [TestCase("main.tf", WatcherChangeTypes.Deleted, true)]
    [TestCase("notes.md", WatcherChangeTypes.Deleted, true)]
    [TestCase("photo.png", WatcherChangeTypes.Deleted, false)]
    [TestCase("photo.png", WatcherChangeTypes.Created, false)]
    [TestCase("archive.zip", WatcherChangeTypes.Created, false)]
    public void AGoneFileCountsWhenTheDocumentsSelectorMayHaveReadIt(string fileName, WatcherChangeTypes changeType, bool expected)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-selector-watcher-{Guid.NewGuid():N}");

        Assert.That(FileSourceIndexer.IsRelevantWatcherPath(DefaultConfiguration(rootPath), Path.Combine(rootPath, fileName), changeType), Is.EqualTo(expected), "Only media never matches the selector.");
    }

    [Test]
    public void ADeletedFileWhichMatchedAPatternStartsARun()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-deleted-watcher-{Guid.NewGuid():N}");
        var configuration = DefaultConfiguration(rootPath) with { FilePatterns = FileNamePatterns.Parse("*.md") };

        Assert.That(FileSourceIndexer.IsRelevantWatcherPath(configuration, Path.Combine(rootPath, "docs", "notes.md"), WatcherChangeTypes.Deleted), Is.True);
    }

    [Test]
    public void ChangingTheExclusionListReconfiguresTheWatcher()
    {
        var before = DefaultConfiguration(Path.GetTempPath());
        var after = before with { ExcludePatterns = FileExclusions.Parse("bin/") };

        Assert.That(FileSourceIndexer.IsSameWatchConfiguration(before, after), Is.False);
    }

    [Test]
    public void ReorderingEquivalentExclusionsDoesNotRecreateTheWatcher()
    {
        var before = DefaultConfiguration(Path.GetTempPath()) with
        {
            ExcludePatterns = FileExclusions.Parse("*.mp3;draft-*"),
        };
        var after = before with { ExcludePatterns = FileExclusions.Parse("DRAFT-*;*.MP3;*.mp3") };

        Assert.That(FileSourceIndexer.IsSameWatchConfiguration(before, after), Is.True);
    }

    [Test]
    public void RenamingAnIncludedFileIntoAnExcludedNameStillRefreshesTheIndex()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-excluded-rename-{Guid.NewGuid():N}");
        var configuration = DefaultConfiguration(rootPath) with
        {
            FilePatterns = FileNamePatterns.Parse("*.md"),
            ExcludePatterns = FileExclusions.Parse("draft-*"),
        };
        var renamed = new RenamedEventArgs(WatcherChangeTypes.Renamed, rootPath, "draft-report.md", "report.md");

        Assert.That(FileSourceIndexer.IsRelevantWatcherEvent(configuration, renamed), Is.True);
    }

    [Test]
    public void RelocatingToADirectoryWithDifferentCasingUsesTheLocalPathRules()
    {
        var before = DefaultConfiguration(Path.Combine(Path.GetTempPath(), "Documents"));
        var after = before with { RootPath = Path.Combine(Path.GetTempPath(), "documents") };

        Assert.That(FileSourceIndexer.IsSameWatchConfiguration(before, after), Is.EqualTo(OperatingSystem.IsWindows()), "On Unix these can be different directories; their watchers must not be treated as interchangeable.");
    }

    [Test]
    public async Task DirectoryWatcherIgnoresTheBatchTranscriptWhenTheMediaIsSelected()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-transcript-watcher-{Guid.NewGuid():N}");
        var mediaPath = Path.Combine(directoryPath, "recording.mp3");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(mediaPath, [1, 2, 3]);
            var transcriptPath = MediaSidecarTranscripts.GetFilePath(mediaPath);
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
                FileSourceIndexer.IsRelevantWatcherPath(configuration, MediaSidecarTranscripts.GetFilePath(mediaPath), WatcherChangeTypes.Changed),
                Is.True);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [TestCase("recording.mp3")]
    [TestCase("meeting.mp4")]
    [TestCase("voice.webm")]
    public void MediaStartsARunOnlyWhenAPatternSelectsIt(string fileName)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-media-watcher-{Guid.NewGuid():N}");
        var path = Path.Combine(rootPath, fileName);
        var documents = DefaultConfiguration(rootPath);
        var everything = documents with { FilePatterns = FileNamePatterns.Parse("*") };

        Assert.Multiple(() =>
        {
            Assert.That(FileSourceIndexer.IsRelevantWatcherPath(documents, path, WatcherChangeTypes.Created), Is.False, "The documents selector never selects audio or video.");
            Assert.That(FileSourceIndexer.IsRelevantWatcherPath(everything, path, WatcherChangeTypes.Created), Is.True);
        });
    }

    private static FileSourceIndexer.DataSourceWatcherConfiguration DefaultConfiguration(string rootPath) => new(
        rootPath,
        true,
        FilePatterns: FileNamePatterns.Parse(FileNamePatterns.ALL_DOCUMENTS_PATTERN),
        ExcludePatterns: FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS));
}
