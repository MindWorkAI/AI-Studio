using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

public sealed class DirectoryFileTraversalTests
{
    [Test]
    public async Task HiddenFilesAndFoldersAreLeftOutByDefaultAndCanBeIncluded()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-folders-{Guid.NewGuid():N}");
        var hiddenDirectoryPath = Path.Combine(directoryPath, ".hidden");
        var visibleFilePath = Path.Combine(directoryPath, "notes.md");
        var hiddenFilePath = Path.Combine(directoryPath, ".env");
        var fileInHiddenDirectoryPath = Path.Combine(hiddenDirectoryPath, "notes.txt");
        Directory.CreateDirectory(hiddenDirectoryPath);

        try
        {
            await File.WriteAllTextAsync(visibleFilePath, "Visible notes");
            await File.WriteAllTextAsync(hiddenFilePath, "API_KEY=secret");
            await File.WriteAllTextAsync(fileInHiddenDirectoryPath, "Hidden notes");

            var visibleOnly = DirectoryFileTraversal.EnumerateFiles(directoryPath, true, false).ToList();
            var includingHidden = DirectoryFileTraversal.EnumerateFiles(directoryPath, true, true).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(visibleOnly, Is.EqualTo(new[] { visibleFilePath }), "Like * in a shell, nothing hidden is found unless asked for.");
                Assert.That(includingHidden, Is.EquivalentTo(new[] { visibleFilePath, hiddenFilePath, fileInHiddenDirectoryPath }));
            });
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task APatternWhichBeginsWithADotSelectsHiddenFilesButEntersNoHiddenFolder()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-pattern-{Guid.NewGuid():N}");
        var hiddenDirectoryPath = Path.Combine(directoryPath, ".github");
        var gitignorePath = Path.Combine(directoryPath, ".gitignore");
        var environmentPath = Path.Combine(directoryPath, ".env");
        var fileInHiddenDirectoryPath = Path.Combine(hiddenDirectoryPath, ".gitignore");
        Directory.CreateDirectory(hiddenDirectoryPath);

        try
        {
            await File.WriteAllTextAsync(gitignorePath, "bin/");
            await File.WriteAllTextAsync(environmentPath, "API_KEY=secret");
            await File.WriteAllTextAsync(fileInHiddenDirectoryPath, "obj/");
            var patterns = FileNamePatterns.Parse("*;.gitignore");

            var files = DirectoryFileTraversal.EnumerateFiles(
                directoryPath,
                true,
                false,
                includeHiddenFile: filePath => FileNamePatterns.NamesHiddenFile(filePath, patterns)).ToList();

            Assert.That(files, Is.EqualTo(new[] { gitignorePath }), "The pattern .gitignore names that file; * names no hidden file, and no pattern opens a hidden folder.");
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task AFileHiddenByTheOperatingSystemIsLeftOutAsWell()
    {
        if (OperatingSystem.IsLinux())
            Assert.Ignore("Linux hides files by their names only.");

        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-attribute-{Guid.NewGuid():N}");
        var hiddenFilePath = Path.Combine(directoryPath, "notes.txt");
        Directory.CreateDirectory(directoryPath);

        try
        {
            await File.WriteAllTextAsync(hiddenFilePath, "Hidden notes");
            File.SetAttributes(hiddenFilePath, File.GetAttributes(hiddenFilePath) | FileAttributes.Hidden);
            if (!File.GetAttributes(hiddenFilePath).HasFlag(FileAttributes.Hidden))
                Assert.Ignore("The file system of the temporary folder cannot hide files.");

            Assert.Multiple(() =>
            {
                Assert.That(DirectoryFileTraversal.EnumerateFiles(directoryPath, false, false), Is.Empty);
                Assert.That(DirectoryFileTraversal.EnumerateFiles(directoryPath, false, true), Is.EqualTo(new[] { hiddenFilePath }));
                Assert.That(DirectoryFileTraversal.IsHiddenPath(hiddenFilePath), Is.True);
            });
        }
        finally
        {
            File.SetAttributes(hiddenFilePath, FileAttributes.Normal);
            Directory.Delete(directoryPath, true);
        }
    }

    [Test]
    public async Task AnExplicitlySelectedHiddenRootIsStillRead()
    {
        var parentPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-root-{Guid.NewGuid():N}");
        var hiddenRootPath = Path.Combine(parentPath, ".selected");
        var filePath = Path.Combine(hiddenRootPath, "notes.txt");
        Directory.CreateDirectory(hiddenRootPath);

        try
        {
            await File.WriteAllTextAsync(filePath, "Selected hidden root");

            Assert.That(DirectoryFileTraversal.EnumerateFiles(hiddenRootPath, true, false), Does.Contain(filePath));
        }
        finally
        {
            Directory.Delete(parentPath, true);
        }
    }

    [Test]
    public async Task DeepLongPathsAndUnusualFileNamesRemainDiscoverable()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ai-studio-long-path-{Guid.NewGuid():N}");
        var directoryPath = rootPath;
        var depth = 0;
        while (directoryPath.Length < 320)
        {
            directoryPath = Path.Combine(directoryPath, $"nested-{depth++}-{new string('x', 32)}");
        }

        var filePath = Path.Combine(directoryPath, "Dockerfile.開発 (final) [β] #1");

        try
        {
            Directory.CreateDirectory(directoryPath);
            await File.WriteAllTextAsync(filePath, "FROM alpine:latest\nRUN echo 'Grüße'");

            var discoveredFiles = DirectoryFileTraversal.EnumerateFiles(rootPath, true, false).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(filePath.Length, Is.GreaterThan(300));
                Assert.That(discoveredFiles, Does.Contain(filePath));
                Assert.That(FileNamePatterns.Matches(filePath, FileNamePatterns.Parse("Dockerfile*")), Is.True);
                Assert.That(FileNamePatterns.Matches(filePath, FileNamePatterns.Parse(":text:")), Is.True);
            });
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, true);
        }
    }
}
