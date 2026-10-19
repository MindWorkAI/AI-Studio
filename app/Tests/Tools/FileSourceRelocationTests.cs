using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

public sealed class FileSourceRelocationTests
{
    private string testRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        this.testRoot = Path.Combine(Path.GetTempPath(), $"ai-studio-relocation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testRoot);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(this.testRoot, true);

    [Test]
    public void AMovedFolderFindsAnUnchangedNestedDocument()
    {
        var oldRoot = Path.Combine(this.testRoot, "original");
        var oldFile = Path.Combine(oldRoot, "nested", "notes.md");
        Directory.CreateDirectory(Path.GetDirectoryName(oldFile)!);
        File.WriteAllText(oldFile, "Some indexed text");
        var record = Record(oldFile, Path.Combine("nested", "notes.md"));
        var newRoot = Path.Combine(this.testRoot, "moved");
        Directory.Move(oldRoot, newRoot);

        var found = FileSourceIndexer.FindRelocatedFile(new DataSourceLocalDirectory { Path = newRoot }, oldFile, record);

        Assert.That(found?.FullName, Is.EqualTo(Path.Combine(newRoot, "nested", "notes.md")));
    }

    [TestCase("size")]
    [TestCase("modification time")]
    [TestCase("missing file")]
    public void AChangedOrMissingDocumentCannotReuseItsEmbeddings(string change)
    {
        var file = Path.Combine(this.testRoot, "notes.md");
        File.WriteAllText(file, "Some indexed text");
        var record = Record(file, "notes.md");
        switch (change)
        {
            case "size":
                File.AppendAllText(file, " with more text");
                File.SetLastWriteTimeUtc(file, record.LastWriteUtc.UtcDateTime);
                break;
            case "modification time":
                File.SetLastWriteTimeUtc(file, record.LastWriteUtc.UtcDateTime.AddMinutes(1));
                break;
            case "missing file":
                File.Delete(file);
                break;
        }

        Assert.That(FileSourceIndexer.FindRelocatedFile(new DataSourceLocalDirectory { Path = this.testRoot }, Path.Combine(this.testRoot, "old", "notes.md"), record), Is.Null);
    }

    [Test]
    public void ARenamedSingleFileCanReuseItsEmbeddings()
    {
        var oldFile = Path.Combine(this.testRoot, "notes.md");
        File.WriteAllText(oldFile, "Some indexed text");
        var record = Record(oldFile, "notes.md");
        var newFile = Path.Combine(this.testRoot, "renamed.md");
        File.Move(oldFile, newFile);

        Assert.That(FileSourceIndexer.FindRelocatedFile(new DataSourceLocalFile { FilePath = newFile }, oldFile, record)?.FullName, Is.EqualTo(newFile));
    }

    [Test]
    public void ARelativePathCannotEscapeTheConfiguredFolder()
    {
        var outside = Path.Combine(this.testRoot, "notes.md");
        File.WriteAllText(outside, "Some indexed text");
        var record = Record(outside, Path.Combine("..", "notes.md"));
        var folder = Path.Combine(this.testRoot, "selected");
        Directory.CreateDirectory(folder);

        Assert.That(FileSourceIndexer.FindRelocatedFile(new DataSourceLocalDirectory { Path = folder }, Path.Combine(this.testRoot, "old", "notes.md"), record), Is.Null);
    }

    private static EmbeddedFileRecord Record(string path, string relativePath)
    {
        var file = new FileInfo(path);
        return new("old-fingerprint", file.Length, new DateTimeOffset(file.LastWriteTimeUtc), DateTimeOffset.UtcNow, 1, RelativePath: relativePath, ParentFileId: Guid.NewGuid().ToString());
    }

    [Test]
    public void ANewFileAtAnOldLocationCannotOverwriteTheRelocatedDocument()
    {
        var source = new DataSourceLocalDirectory { Id = Guid.NewGuid().ToString(), Path = this.testRoot };
        var originalPath = Path.Combine(this.testRoot, "nested", "notes.md");
        var relocatedPath = Path.Combine(this.testRoot, "notes.md");
        var originalId = IndexedDocumentIds.CreateParentId(source.Id, originalPath);
        var manifest = new DataSourceEmbeddingManifest();
        manifest.Files[relocatedPath] = new("fingerprint", 100, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1, RelativePath: "notes.md", ParentFileId: originalId);

        // Only identity assignment is exercised; none of the run's services are called.
        var context = new IndexedRunContext(source, null!, null!, null!, null!, manifest,
            new RunShare(RunShare.DEFAULT_DURATION), new IndexingPass(new DataSourceRunSignature("signature", "selection")), null!, null!, _ => { }, NullLogger.Instance);
        var newId = context.GetFileParentId(originalPath);

        Assert.Multiple(() =>
        {
            Assert.That(context.GetFileParentId(relocatedPath), Is.EqualTo(originalId));
            Assert.That(newId, Is.Not.EqualTo(originalId));
            Assert.That(context.GetFileParentId(originalPath), Is.EqualTo(newId), "A media file may ask for its identity once before and once after transcription.");
        });
    }
}
