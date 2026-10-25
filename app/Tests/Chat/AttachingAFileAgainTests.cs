using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that a file attached again goes with its text as it is now, and only once.
/// </summary>
/// <remarks>
/// A message being edited brings its attachments along, each with the text it was sent with. When
/// the user changes the file and attaches it again, the message must not go with both texts.
/// </remarks>
[TestFixture]
public sealed class AttachingAFileAgainTests
{
    private string testDirectory = string.Empty;

    [SetUp]
    public void CreateDirectory()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-attach-again-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
    }

    [TearDown]
    public void RemoveDirectory() => Directory.Delete(this.testDirectory, true);

    [Test]
    public void AFileAttachedAgainTakesThePlaceOfItsEarlierAttachment()
    {
        var path = Path.Combine(this.testDirectory, "minutes.txt");
        File.WriteAllText(path, "The board met on Monday.");
        var sentBefore = FileAttachment.FromPath(path) with { ProvenanceId = Guid.NewGuid() };
        var otherFile = FileAttachment.FromPath(Path.Combine(this.testDirectory, "agenda.txt"));
        var attachments = new HashSet<FileAttachment> { sentBefore, otherFile };

        File.WriteAllText(path, "The board met on Monday and on Tuesday.");
        attachments.AttachInPlaceOfSameFile(FileAttachment.FromPath(path));

        Assert.Multiple(() =>
        {
            Assert.That(attachments.Count(attachment => attachment.FilePath == path), Is.EqualTo(1), "The message would go with the old and the new text of the file.");
            Assert.That(attachments.Single(attachment => attachment.FilePath == path).ProvenanceId, Is.Null, "Attaching a file again means the file as it is now.");
            Assert.That(attachments, Does.Contain(otherFile));
        });
    }

    [Test]
    public void AFileOfTheSameTimeButAnotherSizeChanged()
    {
        var path = Path.Combine(this.testDirectory, "minutes.txt");
        File.WriteAllText(path, "The board met on Monday.");
        var writtenAt = File.GetLastWriteTimeUtc(path);
        var before = FileVersion.Of(path);

        // A file system which keeps the time coarsely, e.g., FAT, reports the same time after a quick change:
        File.WriteAllText(path, "The board met on Monday and on Tuesday.");
        File.SetLastWriteTimeUtc(path, writtenAt);

        Assert.That(FileVersion.Of(path), Is.Not.EqualTo(before));
    }

    [Test]
    public void AFileWhichIsGoneHasNoVersion() => Assert.That(FileVersion.Of(Path.Combine(this.testDirectory, "gone.txt")), Is.Null);
}