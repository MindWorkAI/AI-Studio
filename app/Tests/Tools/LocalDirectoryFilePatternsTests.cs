using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools;

public sealed class LocalDirectoryFilePatternsTests
{
    [Test]
    public void MissingDirectorySelectionSettingsUseTheirDefaults()
    {
        var dataSource = JsonSerializer.Deserialize<DataSourceLocalDirectory>("{}", SettingsManager.JSON_OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(dataSource.FilePatterns, Is.EqualTo(FileNamePatterns.ALL_DOCUMENTS_PATTERN), "A data source from before file patterns has to keep indexing what it indexed.");
            Assert.That(dataSource.IncludeSubdirectories, Is.True);
            Assert.That(dataSource.IncludeHiddenFilesAndFolders, Is.False);
        });
    }

    [Test]
    public void ExistingBatchSettingsDefaultToSkippingHiddenFilesAndFolders()
    {
        var settings = JsonSerializer.Deserialize<DataBatchProcessing>("{}", SettingsManager.JSON_OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings!.IncludeSubdirectories, Is.EqualTo(DataBatchProcessing.DEFAULT_INCLUDE_SUBDIRECTORIES));
            Assert.That(settings.IncludeHiddenFilesAndFolders, Is.EqualTo(DataBatchProcessing.DEFAULT_INCLUDE_HIDDEN_FILES_AND_FOLDERS));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("*.txt;")]
    [TestCase("folder/*.txt")]
    public void InvalidStoredPatternsFallBackWithoutRemovingExistingDocuments(string? storedPatterns)
    {
        var dataSource = new DataSourceLocalDirectory { FilePatterns = storedPatterns! };

        Assert.That(dataSource.EffectiveFilePatterns, Is.EqualTo(DataSourceLocalDirectory.DEFAULT_FILE_PATTERNS));
    }

    [Test]
    public void PatternsAreSplitAndTrimmed()
    {
        Assert.That(FileNamePatterns.Parse(" *.txt ; *.tf ; .gitignore "), Is.EqualTo(new[] { "*.txt", "*.tf", ".gitignore" }));
    }

    [Test]
    public void NormalizingPatternsRemovesCaseInsensitiveDuplicates()
    {
        Assert.That(FileNamePatterns.Normalize(" *.txt ; *.TF ; *.tf ; :text: ; :TEXT: "), Is.EqualTo("*.txt;*.TF;:text:"));
    }

    [Test]
    public void PatternSetsAreEquivalentRegardlessOfOrderOrDuplicates()
    {
        Assert.That(
            FileNamePatterns.AreEquivalent(["*.txt", "*.tf", "*.TF"], ["*.TF", "*.TXT"]),
            Is.True);
    }

    [Test]
    public void FilePatternsSurvivePolymorphicSettingsSerialization()
    {
        IDataSource written = new DataSourceLocalDirectory
        {
            Type = DataSourceType.LOCAL_DIRECTORY,
            FilePatterns = "*.txt;*.tf;.gitignore",
            IncludeSubdirectories = false,
            IncludeHiddenFilesAndFolders = true,
        };

        var json = JsonSerializer.Serialize(written, SettingsManager.JSON_OPTIONS);
        var read = JsonSerializer.Deserialize<IDataSource>(json, SettingsManager.JSON_OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain(nameof(DataSourceLocalDirectory.EffectiveFilePatterns)));
            Assert.That(read, Is.TypeOf<DataSourceLocalDirectory>());
            Assert.That(((DataSourceLocalDirectory)read!).FilePatterns, Is.EqualTo("*.txt;*.tf;.gitignore"));
            Assert.That(((DataSourceLocalDirectory)read).IncludeSubdirectories, Is.False);
            Assert.That(((DataSourceLocalDirectory)read).IncludeHiddenFilesAndFolders, Is.True);
        });
    }

    [Test]
    public async Task DirectoryStatisticsCanExcludeSubdirectories()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-nonrecursive-statistics-{Guid.NewGuid():N}");
        var subdirectoryPath = Path.Combine(directoryPath, "nested");
        Directory.CreateDirectory(subdirectoryPath);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(directoryPath, "included.txt"), new byte[5]);
            await File.WriteAllBytesAsync(Path.Combine(subdirectoryPath, "excluded.txt"), new byte[10]);

            long totalSize = 0;
            long fileCount = 0;
            var reportedFiles = new List<string>();

            await new DirectoryInfo(directoryPath).DetermineContentSize(
                size => totalSize = size,
                count => fileCount = count,
                reportedFiles.Add,
                includeSubdirectories: false);

            Assert.Multiple(() =>
            {
                Assert.That(totalSize, Is.EqualTo(5));
                Assert.That(fileCount, Is.EqualTo(1));
                Assert.That(reportedFiles, Has.Count.EqualTo(1));
                Assert.That(reportedFiles[0], Does.EndWith("included.txt"));
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task DirectoryStatisticsUseTheConfiguredHiddenFilesAndFoldersRule()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-statistics-{Guid.NewGuid():N}");
        var hiddenDirectoryPath = Path.Combine(directoryPath, ".hidden");
        Directory.CreateDirectory(hiddenDirectoryPath);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(directoryPath, "visible.txt"), new byte[5]);
            await File.WriteAllBytesAsync(Path.Combine(directoryPath, ".env"), new byte[20]);
            await File.WriteAllBytesAsync(Path.Combine(hiddenDirectoryPath, "hidden.txt"), new byte[10]);

            long visibleSize = 0;
            long visibleCount = 0;
            await new DirectoryInfo(directoryPath).DetermineContentSize(
                size => visibleSize = size,
                count => visibleCount = count,
                _ => { },
                includeSubdirectories: true,
                includeHiddenFilesAndFolders: false);

            long completeSize = 0;
            long completeCount = 0;
            await new DirectoryInfo(directoryPath).DetermineContentSize(
                size => completeSize = size,
                count => completeCount = count,
                _ => { },
                includeSubdirectories: true,
                includeHiddenFilesAndFolders: true);

            Assert.Multiple(() =>
            {
                Assert.That(visibleSize, Is.EqualTo(5));
                Assert.That(visibleCount, Is.EqualTo(1));
                Assert.That(completeSize, Is.EqualTo(35));
                Assert.That(completeCount, Is.EqualTo(3));
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [TestCase("notes.txt")]
    [TestCase("main.TF")]
    [TestCase(".gitignore")]
    [TestCase("nested/folder/variables.tf")]
    public void MatchingUsesTheFileName(string path)
    {
        var patterns = FileNamePatterns.Parse("*.txt;*.tf;.gitignore");

        Assert.That(FileNamePatterns.Matches(path, patterns), Is.True);
    }

    [TestCase("Dockerfile", "Dockerfile")]
    [TestCase("Dockerfile*", "Dockerfile.development")]
    [TestCase("Containerfile*", "nested/Containerfile.arm64")]
    [TestCase("Jenkinsfile*", "Jenkinsfile.release")]
    [TestCase("Makefile", "nested/Makefile")]
    [TestCase("GNUmakefile", "GNUmakefile")]
    [TestCase("Procfile", "Procfile")]
    [TestCase("Vagrantfile", "Vagrantfile")]
    [TestCase("Caddyfile*", "Caddyfile.production")]
    [TestCase("BUILD;WORKSPACE", "WORKSPACE")]
    [TestCase("*.env", ".env")]
    [TestCase("deployment notes*#?.txt", "deployment notes (final) [β] #1.txt")]
    public void GlobPatternsMatchSourceLikeAndUnusualFileNames(string pattern, string path)
    {
        Assert.That(FileNamePatterns.Matches(path, FileNamePatterns.Parse(pattern)), Is.True);
    }

    [Test]
    public void GlobPatternsMatchFileNamesAtTheEndOfExtremelyLongPaths()
    {
        var directorySegments = Enumerable.Range(0, 8)
            .Select(index => $"nested-{index}-{new string('x', 32)}")
            .ToArray();
        var path = Path.Combine([.. directorySegments, "Dockerfile.development"]);

        Assert.Multiple(() =>
        {
            Assert.That(path.Length, Is.GreaterThan(300));
            Assert.That(FileNamePatterns.Matches(path, FileNamePatterns.Parse("Dockerfile*")), Is.True);
        });
    }

    [TestCase("image.png")]
    [TestCase("notes.txt.bak")]
    [TestCase("gitignore")]
    public void FilesOutsideThePatternsDoNotMatch(string path)
    {
        var patterns = FileNamePatterns.Parse("*.txt;*.tf;.gitignore");

        Assert.That(FileNamePatterns.Matches(path, patterns), Is.False);
    }

    [TestCase(".gitignore")]
    [TestCase("LICENSE")]
    [TestCase("recording.mp3")]
    public void CatchAllPatternIncludesEveryFileName(string fileName)
    {
        Assert.That(FileNamePatterns.Matches(fileName, FileNamePatterns.Parse("*")), Is.True);
    }

    [Test]
    public async Task ReadableTextStillHasToMatchAFilePattern()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-readable-pattern-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directoryPath, "main.tf");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllTextAsync(filePath, "resource \"example\" \"main\" {}");

            Assert.Multiple(() =>
            {
                Assert.That(FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT), Is.True);
                Assert.That(FileNamePatterns.Matches(filePath, FileNamePatterns.Parse("*.txt")), Is.False);
                Assert.That(FileNamePatterns.Matches(filePath, FileNamePatterns.Parse(":text:")), Is.True);
                Assert.That(FileNamePatterns.Matches(filePath, FileNamePatterns.Parse("*")), Is.True);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task AllTextFilesPatternRejectsBinaryAndMediaFiles()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-text-pattern-binary-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        try
        {
            var binaryPath = Path.Combine(directoryPath, "archive.data");
            var mediaPath = Path.Combine(directoryPath, "recording.mp3");
            var imagePath = Path.Combine(directoryPath, "drawing.svg");
            await File.WriteAllBytesAsync(binaryPath, [0x50, 0x4B, 0x03, 0x04, 0x00]);
            await File.WriteAllTextAsync(mediaPath, "This has readable bytes but a media file name.");
            await File.WriteAllTextAsync(imagePath, "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
            var patterns = FileNamePatterns.Parse(":text:");

            Assert.Multiple(() =>
            {
                Assert.That(FileNamePatterns.Matches(binaryPath, patterns), Is.False);
                Assert.That(FileNamePatterns.Matches(mediaPath, patterns), Is.False);
                Assert.That(FileNamePatterns.Matches(imagePath, patterns), Is.False, "An image cannot be indexed, even when it is written as text.");
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [TestCase(".env", "*")]
    [TestCase(".env", ":text:")]
    [TestCase(".env", ":documents:")]
    [TestCase(".env", "*.env")]
    [TestCase("notes.txt", ".*")]
    public void OnlyAPatternWhichBeginsWithADotNamesAHiddenFile(string fileName, string patterns)
    {
        Assert.That(FileNamePatterns.NamesHiddenFile(fileName, FileNamePatterns.Parse(patterns)), Is.False);
    }

    [TestCase(".env", ".env")]
    [TestCase(".gitignore", "*.md;.gitignore")]
    [TestCase(".env.local", ".env*")]
    [TestCase(".editorconfig", ".*")]
    public void APatternWhichBeginsWithADotNamesHiddenFiles(string fileName, string patterns)
    {
        Assert.That(FileNamePatterns.NamesHiddenFile(fileName, FileNamePatterns.Parse(patterns)), Is.True);
    }

    [TestCase("report.pdf")]
    [TestCase("letter.docx")]
    [TestCase("budget.xlsx")]
    [TestCase("notes.md")]
    [TestCase("Program.cs")]
    public void DocumentsSelectorKeepsWhatFolderDataSourcesIndexedBefore(string fileName)
    {
        // Known document types are told by their extension, so the file needs no content:
        var filePath = Path.Combine(Path.GetTempPath(), "test-data", fileName);

        Assert.That(FileNamePatterns.Matches(filePath, FileNamePatterns.Parse(FileNamePatterns.ALL_DOCUMENTS_PATTERN)), Is.True);
    }

    [Test]
    public async Task DocumentsSelectorAddsReadableTextButNoMediaOrBinaries()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-documents-pattern-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        try
        {
            var terraformPath = Path.Combine(directoryPath, "main.tf");
            var photoPath = Path.Combine(directoryPath, "photo.png");
            var drawingPath = Path.Combine(directoryPath, "drawing.svg");
            var recordingPath = Path.Combine(directoryPath, "recording.mp3");
            var archivePath = Path.Combine(directoryPath, "archive.zip");
            await File.WriteAllTextAsync(terraformPath, "resource \"example\" \"main\" {}");
            await File.WriteAllBytesAsync(photoPath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);
            await File.WriteAllTextAsync(drawingPath, "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
            await File.WriteAllTextAsync(recordingPath, "This has readable bytes but a media file name.");
            await File.WriteAllBytesAsync(archivePath, [0x50, 0x4B, 0x03, 0x04, 0x00]);
            var patterns = FileNamePatterns.Parse(FileNamePatterns.ALL_DOCUMENTS_PATTERN);

            Assert.Multiple(() =>
            {
                Assert.That(FileNamePatterns.Matches(terraformPath, patterns), Is.True, "Readable text is what the selector adds to the documents.");
                Assert.That(FileNamePatterns.Matches(photoPath, patterns), Is.False);
                Assert.That(FileNamePatterns.Matches(drawingPath, patterns), Is.False, "An image cannot be indexed, even when it is written as text.");
                Assert.That(FileNamePatterns.Matches(recordingPath, patterns), Is.False);
                Assert.That(FileNamePatterns.Matches(archivePath, patterns), Is.False);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task ExplicitGlobSelectsMatchingBinaryContent()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-explicit-binary-pattern-{Guid.NewGuid():N}");
        var filePath = Path.Combine(directoryPath, "archive.data");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(filePath, [0x50, 0x4B, 0x03, 0x04, 0x00]);

            Assert.Multiple(() =>
            {
                Assert.That(FileTypes.IsAllowedPath(filePath, FileTypes.DOCUMENT), Is.False);
                Assert.That(FileNamePatterns.Matches(filePath, FileNamePatterns.Parse("*.data")), Is.True);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task DirectoryStatisticsIncludeOnlyMatchingFiles()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-pattern-statistics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllBytesAsync(Path.Combine(directoryPath, "included.tf"), new byte[5]);
            await File.WriteAllBytesAsync(Path.Combine(directoryPath, "excluded.md"), new byte[10]);

            long totalSize = 0;
            long fileCount = 0;
            var reportedFiles = new List<string>();
            var patterns = FileNamePatterns.Parse("*.tf");

            await new DirectoryInfo(directoryPath).DetermineContentSize(
                size => totalSize = size,
                count => fileCount = count,
                reportedFiles.Add,
                includeFile: file => FileNamePatterns.Matches(file.FullName, patterns));

            Assert.Multiple(() =>
            {
                Assert.That(totalSize, Is.EqualTo(5));
                Assert.That(fileCount, Is.EqualTo(1));
                Assert.That(reportedFiles, Has.Count.EqualTo(1));
                Assert.That(reportedFiles[0], Does.EndWith("included.tf"));
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [TestCase("*.txt;*.tf;.gitignore")]
    [TestCase("*")]
    [TestCase(":text:")]
    [TestCase(":TEXT:;*.pdf")]
    [TestCase(":documents:")]
    [TestCase(":DOCUMENTS:;*.tf")]
    [TestCase("report-??.md")]
    public void DataSourceValidationAcceptsSimplePatterns(string patterns)
    {
        Assert.That(new DataSourceValidation().ValidateFilePatterns(patterns), Is.Null);
    }

    [TestCase("")]
    [TestCase("*.txt;")]
    [TestCase("**/*.txt")]
    [TestCase("folder/*.txt")]
    [TestCase(":document:")]
    [TestCase("*.txt;:docs:")]
    public void DataSourceValidationRejectsInvalidPatterns(string patterns)
    {
        Assert.That(new DataSourceValidation().ValidateFilePatterns(patterns), Is.Not.Null);
    }
}
