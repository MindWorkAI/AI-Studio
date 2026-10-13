using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

public sealed class DirectoryFileTraversalTests
{
    [Test]
    public async Task HiddenSubdirectoriesAreExcludedByDefaultAndCanBeIncluded()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"ai-studio-hidden-folders-{Guid.NewGuid():N}");
        var hiddenDirectoryPath = Path.Combine(directoryPath, ".hidden");
        var visibleFilePath = Path.Combine(directoryPath, ".gitignore");
        var hiddenFilePath = Path.Combine(hiddenDirectoryPath, "notes.txt");
        Directory.CreateDirectory(hiddenDirectoryPath);

        try
        {
            await File.WriteAllTextAsync(visibleFilePath, "bin/");
            await File.WriteAllTextAsync(hiddenFilePath, "Hidden notes");

            var visibleDirectoriesOnly = DirectoryFileTraversal.EnumerateFiles(directoryPath, true, false).ToList();
            var includingHiddenDirectories = DirectoryFileTraversal.EnumerateFiles(directoryPath, true, true).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(visibleDirectoriesOnly, Does.Contain(visibleFilePath), "A hidden file directly in the selected folder remains eligible.");
                Assert.That(visibleDirectoriesOnly, Does.Not.Contain(hiddenFilePath));
                Assert.That(includingHiddenDirectories, Does.Contain(hiddenFilePath));
            });
        }
        finally
        {
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
