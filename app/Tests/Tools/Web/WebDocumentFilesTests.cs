using AIStudio.Tools.Web;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.Web;

/// <summary>
/// Checks the files a document from the web is downloaded to.
/// </summary>
/// <remarks>
/// A document of the intranet must not stay on the disk once it was read. What a crash left behind
/// goes with a later download, while the file of a read which is still running stays.
/// </remarks>
[TestFixture]
public sealed class WebDocumentFilesTests
{
    private string directory = string.Empty;

    [SetUp]
    public void CreateDirectory() => this.directory = Path.Join(Path.GetTempPath(), $"web-documents-test-{Guid.NewGuid():N}");

    [TearDown]
    public void DeleteDirectory()
    {
        if (Directory.Exists(this.directory))
            Directory.Delete(this.directory, true);
    }

    [Test]
    public void APathIsRandomAndCarriesNoExtension()
    {
        var files = new WebDocumentFiles(this.directory);
        var first = files.CreatePath();
        var second = files.CreatePath();

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(this.directory), Is.True, "The directory is created on demand.");
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(Path.GetExtension(first), Is.Empty, "Without an extension, the runtime decides the format by the content alone.");
            Assert.That(File.Exists(first), Is.False);
        });
    }

    [Test]
    public void OnlyOldFilesAreLeftovers()
    {
        var files = new WebDocumentFiles(this.directory);
        var old = files.CreatePath();
        var running = files.CreatePath();
        File.WriteAllText(old, "left behind");
        File.WriteAllText(running, "still being read");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-2));

        var deleted = files.DeleteLeftovers(NullLogger.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.EqualTo(1));
            Assert.That(File.Exists(old), Is.False);
            Assert.That(File.Exists(running), Is.True, "A download or an extraction which is still running keeps its file.");
        });
    }

    [Test]
    public void AMissingDirectoryHasNoLeftovers()
    {
        Assert.That(new WebDocumentFiles(this.directory).DeleteLeftovers(NullLogger.Instance), Is.Zero);
    }

    [Test]
    public void DeletingAMissingFileIsNoError()
    {
        Assert.DoesNotThrow(() => WebDocumentFiles.Delete(Path.Join(this.directory, "missing"), NullLogger.Instance));
    }
}