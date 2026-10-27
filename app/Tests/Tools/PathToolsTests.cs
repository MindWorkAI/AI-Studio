using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks where a file dialog opens for a file or a folder which was moved or renamed.
/// </summary>
/// <remarks>
/// The native dialog cannot open a place which no longer exists and falls back to wherever it was
/// used last, which may be anywhere. The nearest folder which still exists is where the file or the
/// folder was moved from, so its new place is often close by.
/// </remarks>
[TestFixture]
public sealed class PathToolsTests
{
    private string testRoot = string.Empty;

    [SetUp]
    public void CreateTestRoot()
    {
        this.testRoot = Path.Combine(Path.GetTempPath(), $"ai-studio-path-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testRoot);
    }

    [TearDown]
    public void DeleteTestRoot()
    {
        if (Directory.Exists(this.testRoot))
            Directory.Delete(this.testRoot, true);
    }

    [Test]
    public void AnExistingFolderOpensAsItIs()
    {
        var folder = Directory.CreateDirectory(Path.Combine(this.testRoot, "documents")).FullName;

        Assert.That(PathTools.FindNearestExistingPath(folder), Is.EqualTo(folder));
    }

    [Test]
    public void AnExistingFileOpensAsItIs()
    {
        var file = Path.Combine(this.testRoot, "report.pdf");
        File.WriteAllText(file, "report");

        Assert.That(PathTools.FindNearestExistingPath(file), Is.EqualTo(file));
    }

    [Test]
    public void AMovedFolderOpensAtItsNearestFolderWhichStillExists()
    {
        var parent = Directory.CreateDirectory(Path.Combine(this.testRoot, "projects")).FullName;
        var movedFolder = Path.Combine(parent, "2026", "documents");

        Assert.That(PathTools.FindNearestExistingPath(movedFolder), Is.EqualTo(parent), "Every parent folder counts, not only the immediate one.");
    }

    [Test]
    public void AMovedFileOpensAtItsFolder()
    {
        var file = Path.Combine(this.testRoot, "report.pdf");

        Assert.That(PathTools.FindNearestExistingPath(file), Is.EqualTo(this.testRoot));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("documents/report.pdf")]
    public void APathWithoutAPlaceOpensWhereverTheDialogWants(string path)
    {
        Assert.That(PathTools.FindNearestExistingPath(path), Is.Null);
    }

    [Test]
    public void APathOfAnotherOperatingSystemOpensWhereverTheDialogWants()
    {
        var path = OperatingSystem.IsWindows() ? "/Users/somebody/documents" : @"C:\Users\somebody\documents";

        Assert.That(PathTools.FindNearestExistingPath(path), Is.Null, "A backup from another computer must not make the dialog open at a folder which happens to exist here.");
    }
}