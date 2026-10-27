using System.Text.Json;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the list of excluded files and folders, which data sources and the batch processing
/// assistant share.
/// </summary>
public sealed class FileExclusionsTests
{
    [TestCase("")]
    [TestCase("bin/")]
    [TestCase("*.min.js")]
    [TestCase("cmake-build-*/")]
    [TestCase("bin/;\r\n*.min.js;;\n")]
    [TestCase(FileExclusions.DEFAULT_PATTERNS)]
    public void ValidExclusionListsAreAccepted(string patterns)
    {
        Assert.That(FileExclusions.Validate(patterns), Is.Null);
    }

    [TestCase("a/b/")]
    [TestCase("/bin")]
    [TestCase("./")]
    [TestCase(":text:")]
    [TestCase("bin/;:documents:")]
    [TestCase("**/bin/")]
    public void InvalidExclusionListsAreRejected(string patterns)
    {
        Assert.That(FileExclusions.Validate(patterns), Is.Not.Null);
    }

    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase(" ; \r\n", true)]
    [TestCase("build/;cmake-build-*/", true)]
    [TestCase(".env/;archive.v1/;*.vmwarevm/", true)]
    [TestCase("*.pdf", false)]
    [TestCase("Dockerfile", false)]
    [TestCase("node_modules", false)]
    [TestCase("build/;*.txt", false)]
    public void FolderFieldRequiresExplicitFolderRules(string? patterns, bool valid)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.IsValidFolders(patterns), Is.EqualTo(valid));
            Assert.That(FileExclusions.ValidateFolders(patterns) is null, Is.EqualTo(valid));
        });
    }

    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase(" ; \r\n", true)]
    [TestCase("*.pdf;draft-*;report-?.md", true)]
    [TestCase("Dockerfile;Caddyfile;.gitignore", true)]
    [TestCase("build", true)]
    [TestCase("build/", false)]
    [TestCase("*.tmp;obj/", false)]
    public void FileFieldRejectsFolderRulesButAcceptsExtensionlessFiles(string? patterns, bool valid)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.IsValidFiles(patterns), Is.EqualTo(valid));
            Assert.That(FileExclusions.ValidateFiles(patterns) is null, Is.EqualTo(valid));
        });
    }

    [TestCase(":text:")]
    [TestCase(":documents:")]
    [TestCase("src/build/")]
    [TestCase("**/*.tmp")]
    [TestCase("/")]
    [TestCase("./")]
    public void BothFieldValidatorsReuseTheExistingSyntaxErrors(string patterns)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.IsValidFolders(patterns), Is.False);
            Assert.That(FileExclusions.IsValidFiles(patterns), Is.False);
            Assert.That(FileExclusions.ValidateFolders(patterns), Is.EqualTo(FileExclusions.Validate(patterns)));
            Assert.That(FileExclusions.ValidateFiles(patterns), Is.EqualTo(FileExclusions.Validate(patterns)));
        });
    }

    [Test]
    public void StoredListsStillAcceptBothKindsOfRulesTogether()
    {
        const string PATTERNS = "bin/;*.pdf;Dockerfile;.git/";
        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.IsValid(PATTERNS), Is.True);
            Assert.That(FileExclusions.Validate(PATTERNS), Is.Null);
            Assert.That(FileExclusions.ExcludesFolder("bin", FileExclusions.Parse(PATTERNS)), Is.True);
            Assert.That(FileExclusions.ExcludesFile("report.pdf", FileExclusions.Parse(PATTERNS)), Is.True);
        });
    }

    [Test]
    public void EntriesAreSeparatedBySemicolonsAndLineBreaks()
    {
        Assert.That(FileExclusions.Parse(" bin/ ;obj/\r\n\r\n*.min.js\nBIN/ "), Is.EqualTo(new[] { "bin/", "obj/", "*.min.js" }));
    }

    [Test]
    public void SettingsFromBeforeTheListGetTheDefault()
    {
        var dataSource = JsonSerializer.Deserialize<DataSourceLocalDirectory>("{}", SettingsManager.JSON_OPTIONS);
        var batchSettings = JsonSerializer.Deserialize<DataBatchProcessing>("{}", SettingsManager.JSON_OPTIONS);

        Assert.Multiple(() =>
        {
            Assert.That(dataSource.ExcludePatterns, Is.EqualTo(FileExclusions.DEFAULT_PATTERNS));
            Assert.That(batchSettings!.ExcludePatterns, Is.EqualTo(FileExclusions.DEFAULT_PATTERNS));
        });
    }

    [Test]
    public void AnInvalidStoredListFallsBackToTheDefault()
    {
        var dataSource = new DataSourceLocalDirectory { ExcludePatterns = "src/bin/" };

        Assert.That(dataSource.EffectiveExcludePatterns, Is.EqualTo(FileExclusions.DEFAULT_PATTERNS));
    }

    [TestCase("node_modules")]
    [TestCase("release")]
    [TestCase("cmake-build-debug")]
    [TestCase("Windows 11.pvm")]
    [TestCase("__MACOSX")]
    [TestCase("$RECYCLE.BIN")]
    public void TheDefaultListExcludesTheseFolders(string folderName)
    {
        var folderPath = Path.Combine(Path.GetTempPath(), "test-data", folderName);

        Assert.That(FileExclusions.ExcludesFolder(folderPath, FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS)), Is.True);
    }

    [TestCase("docs")]
    [TestCase("vendor")]
    [TestCase("coverage")]
    [TestCase("packages")]
    [TestCase("binaries")]
    public void TheDefaultListKeepsTheseFolders(string folderName)
    {
        var folderPath = Path.Combine(Path.GetTempPath(), "test-data", folderName);

        Assert.That(FileExclusions.ExcludesFolder(folderPath, FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS)), Is.False);
    }

    [TestCase("package-lock.json")]
    [TestCase("app.min.js")]
    [TestCase("app.js.map")]
    [TestCase("tokenizer.json")]
    [TestCase("chat_tokenizer.json")]
    [TestCase("tool.dll")]
    [TestCase("index.sqlite-wal")]
    [TestCase("disk.vmdk")]
    public void TheDefaultListExcludesTheseFiles(string fileName)
    {
        var filePath = Path.Combine(Path.GetTempPath(), "test-data", fileName);

        Assert.That(FileExclusions.ExcludesFile(filePath, FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS)), Is.True);
    }

    [TestCase("package.json")]
    [TestCase("notes.md")]
    [TestCase("city.map")]
    [TestCase("tokenizer_config.json")]
    [TestCase("bin")]
    public void TheDefaultListKeepsTheseFiles(string fileName)
    {
        var filePath = Path.Combine(Path.GetTempPath(), "test-data", fileName);

        Assert.That(FileExclusions.ExcludesFile(filePath, FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS)), Is.False, "A folder entry such as bin/ names folders only.");
    }

    [TestCase("Thumbs.db")]
    [TestCase("desktop.ini")]
    [TestCase(".DS_Store")]
    [TestCase("Icon\r")]
    [TestCase("~$report.docx")]
    [TestCase("Report.lnk")]
    public void MetadataOfOperatingSystemsIsAlwaysExcluded(string fileName)
    {
        Assert.That(FileExclusions.ExcludesFile(fileName, []), Is.True, "An empty list must not let them in.");
    }

    [Test]
    public async Task ExcludedFoldersAreNotEnteredAndExclusionComesBeforeSelection()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-exclusions-{Guid.NewGuid():N}");
        var packagePath = Path.Combine(directoryPath, "node_modules", "left-pad");
        var binPath = Path.Combine(directoryPath, "src", "bin");
        Directory.CreateDirectory(packagePath);
        Directory.CreateDirectory(binPath);

        try
        {
            var manifestPath = Path.Combine(directoryPath, "package.json");
            var lockFilePath = Path.Combine(directoryPath, "package-lock.json");
            var sourcePath = Path.Combine(directoryPath, "src", "app.ts");
            await File.WriteAllTextAsync(manifestPath, "{}");
            await File.WriteAllTextAsync(lockFilePath, "{}");
            await File.WriteAllTextAsync(sourcePath, "export {};");
            await File.WriteAllTextAsync(Path.Combine(packagePath, "index.js"), "module.exports = {};");
            await File.WriteAllTextAsync(Path.Combine(binPath, "app.txt"), "build output");

            var selection = new FolderFileSelection(FileNamePatterns.Parse("*.json;*.ts;*.js;*.txt"), FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS), true, false, FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);
            var excludedFolders = new List<string>();
            var candidates = selection.EnumerateCandidates(directoryPath, reportExcludedFolder: excludedFolders.Add).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(candidates, Is.EquivalentTo(new[] { manifestPath, lockFilePath, sourcePath }), "Nothing below node_modules or bin is even looked at.");
                Assert.That(excludedFolders.Select(Path.GetFileName), Is.EquivalentTo(new[] { "node_modules", "bin" }));
                Assert.That(candidates.Where(selection.Selects), Is.EquivalentTo(new[] { manifestPath, sourcePath }), "*.json selects the lock file, but the list excludes it.");
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task MetadataOfOperatingSystemsIsLeftOutEvenWhenEverythingIsSelected()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-os-metadata-{Guid.NewGuid():N}");
        var archiveFolderPath = Path.Combine(directoryPath, "__MACOSX");
        Directory.CreateDirectory(archiveFolderPath);

        try
        {
            var notesPath = Path.Combine(directoryPath, "notes.md");
            await File.WriteAllTextAsync(notesPath, "Notes");

            // As after a copy from a ZIP file: no system attribute, only the name tells.
            await File.WriteAllTextAsync(Path.Combine(directoryPath, "desktop.ini"), "[.ShellClassInfo]");
            await File.WriteAllBytesAsync(Path.Combine(directoryPath, "Thumbs.db"), [0, 1, 2]);
            await File.WriteAllTextAsync(Path.Combine(archiveFolderPath, "notes.md"), "Resource fork");

            var selection = new FolderFileSelection(["*"], [], true, true, FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);

            Assert.That(selection.EnumerateCandidates(directoryPath), Is.EqualTo(new[] { notesPath }));
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task TheConfigurationOfAVirtualMachineStaysOut()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-virtual-machine-{Guid.NewGuid():N}");
        var machinePath = Path.Combine(directoryPath, "Windows 11.pvm");
        Directory.CreateDirectory(machinePath);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(machinePath, "config.pvs"), "<ParallelsVirtualMachine/>");
            var selection = new FolderFileSelection(FileNamePatterns.Parse(FileNamePatterns.ALL_DOCUMENTS_PATTERN), FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS), true, false, FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);

            Assert.That(selection.EnumerateCandidates(directoryPath), Is.Empty, "The readable text check would select config.pvs.");
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task TheCallerCanLeaveOutFurtherFolders()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-batch-exclusions-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(directoryPath, "ai-results");
        var binPath = Path.Combine(directoryPath, "bin");
        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(binPath);

        try
        {
            var reportPath = Path.Combine(directoryPath, "report.md");
            await File.WriteAllTextAsync(reportPath, "Report");
            await File.WriteAllTextAsync(Path.Combine(outputPath, "report_result.md"), "Result");
            await File.WriteAllTextAsync(Path.Combine(binPath, "notes.md"), "Build output");
            var selection = new FolderFileSelection(["*.md"], FileExclusions.Parse("bin/"), true, false, FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);

            var candidates = selection.EnumerateCandidates(
                directoryPath,
                includeDirectory: folderPath => !string.Equals(folderPath, outputPath, StringComparison.OrdinalIgnoreCase));

            Assert.That(candidates, Is.EqualTo(new[] { reportPath }), "Like the batch processing assistant leaves out its output folder.");
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public void ExcludedFoldersAreSummarizedByName()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), "test-data");
        var result = new FileEnumerationResult();

        result.AddExcludedFolder(Path.Combine(directoryPath, "bin"));
        result.AddExcludedFolder(Path.Combine(directoryPath, "src", "Bin"));
        result.AddExcludedFolder(Path.Combine(directoryPath, "node_modules"));

        Assert.Multiple(() =>
        {
            Assert.That(result.ExcludedFolderCount, Is.EqualTo(3));
            Assert.That(result.ExcludedFolders, Is.EquivalentTo(new[]
            {
                new DataSourceEmbeddingExcludedFolderGroup("bin", 2),
                new DataSourceEmbeddingExcludedFolderGroup("node_modules", 1),
            }));
        });
    }
}