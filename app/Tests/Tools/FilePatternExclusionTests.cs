using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

public sealed class FilePatternExclusionTests
{
    [TestCase("*", "*.mp3;*.webm", "recording.WEBM", false)]
    [TestCase("*", "*.mp3;*.webm", "report.pdf", true)]
    [TestCase("*.txt;*.pdf", "draft-*", "draft-report.pdf", false)]
    [TestCase("*.txt;*.pdf", "draft-*", "report.pdf", true)]
    [TestCase("*.txt", "*.mp3", "report.pdf", false)]
    [TestCase("*", "Dockerfile;Caddyfile", "Dockerfile", false)]
    [TestCase("*", ".env", ".env", false)]
    [TestCase("*", "*", "report.pdf", false)]
    [TestCase("*", "", "report.pdf", true)]
    [TestCase("*", "*-draft-*", "report-draft-final.md", false)]
    [TestCase("*", "draft-*", "draft-", false)]
    [TestCase("*", "report-?.md", "report-a.md", false)]
    [TestCase("*", "report-?.md", "report-ab.md", true)]
    public void ExclusionsOverrideIncludesButNeverSelectOtherFiles(string include, string exclude, string fileName, bool expected)
    {
        var path = Path.Combine(Path.GetTempPath(), "nested", fileName);
        var selection = new FolderFileSelection(
            FileNamePatterns.Parse(include),
            FileExclusions.Parse(exclude),
            false,
            false,
            FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);

        Assert.That(selection.Selects(path), Is.EqualTo(expected));
    }

    [TestCase("build", true)]
    [TestCase("app-build", true)]
    [TestCase("my-build", true)]
    [TestCase("BUILD", true)]
    [TestCase("builder", false)]
    [TestCase("build-output", false)]
    public void TheFolderExampleMatchesNamesEndingInBuildButNotFiles(string name, bool expected)
    {
        var patterns = FileExclusions.Parse("*build/");
        var path = Path.Combine(Path.GetTempPath(), "test-data", name);

        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.ExcludesFolder(path, patterns), Is.EqualTo(expected));
            Assert.That(FileExclusions.ExcludesFile(path, patterns), Is.False);
        });
    }

    [TestCase(false, "", true, false)]
    [TestCase(true, "", true, true)]
    [TestCase(false, ".gitignore", false, false)]
    [TestCase(true, ".gitignore", false, false)]
    [TestCase(false, ".hidden/", true, false)]
    [TestCase(true, ".hidden/", true, false)]
    public async Task ExplicitDotPatternsBypassOnlyTheHiddenFileRuleNotExclusionsOrHiddenFolders(
        bool includeHidden, string exclusions, bool selectsRootFile, bool selectsNestedFile)
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-dot-pattern-priority-{Guid.NewGuid():N}");
        var hiddenDirectory = Path.Combine(directoryPath, ".hidden");
        var rootFile = Path.Combine(directoryPath, ".gitignore");
        var nestedFile = Path.Combine(hiddenDirectory, ".gitignore");
        Directory.CreateDirectory(hiddenDirectory);

        try
        {
            await File.WriteAllTextAsync(rootFile, "bin/");
            await File.WriteAllTextAsync(nestedFile, "obj/");
            var selection = new FolderFileSelection(
                [".gitignore"], FileExclusions.Parse(exclusions), true, includeHidden,
                FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);
            var selectedFiles = selection.EnumerateCandidates(directoryPath).Where(selection.Selects).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(selectedFiles.Contains(rootFile), Is.EqualTo(selectsRootFile));
                Assert.That(selectedFiles.Contains(nestedFile), Is.EqualTo(selectsNestedFile));
                Assert.That(selection.LeavesOut(directoryPath, rootFile), Is.EqualTo(!selectsRootFile));
                Assert.That(selection.LeavesOut(directoryPath, nestedFile), Is.EqualTo(!selectsNestedFile));
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public void EnvironmentDefaultsExcludeVisibleEnvironmentFoldersRatherThanDotEnvFiles()
    {
        var defaults = FileExclusions.Parse(FileExclusions.DEFAULT_PATTERNS);

        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.ExcludesFolder("env", defaults), Is.True);
            Assert.That(FileExclusions.ExcludesFolder("venv", defaults), Is.True);
            Assert.That(FileExclusions.ExcludesFile(".env", defaults), Is.False);
            Assert.That(FileExclusions.ExcludesFolder(".env", defaults), Is.False,
                "Hidden folders are handled by the hidden-file switch, not by the default exclusion list.");
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ExclusionsMayBeEmptyWhileIncludesAreRequired(string? patterns)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.Validate(patterns), Is.Null);
            Assert.That(FileExclusions.IsValid(patterns), Is.True);
            Assert.That(FileNamePatterns.IsValid(patterns), Is.False);
        });
    }

    [TestCase("**/*.txt")]
    [TestCase("folder/*.txt")]
    public void OptionalExclusionsStillRejectMalformedRules(string patterns)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileExclusions.Validate(patterns), Is.Not.Null);
            Assert.That(FileExclusions.IsValid(patterns), Is.False);
        });
    }

    [Test]
    public async Task TextSelectorsCanBeCombinedWithFilenameExclusions()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-exclusions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        try
        {
            var terraform = Path.Combine(directoryPath, "main.tf");
            var caddy = Path.Combine(directoryPath, "Caddyfile");
            await File.WriteAllTextAsync(terraform, "resource example {}");
            await File.WriteAllTextAsync(caddy, "localhost { respond hello }");
            var selection = new FolderFileSelection(
                [":text:"], ["*.tf"], false, false, FolderFileSelection.DEFAULT_MAX_FILE_SIZE_MEGABYTES);

            Assert.Multiple(() =>
            {
                Assert.That(selection.Selects(terraform), Is.False);
                Assert.That(selection.Selects(caddy), Is.True);
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }
}