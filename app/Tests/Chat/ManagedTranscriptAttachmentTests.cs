using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks which transcript files AI Studio deletes as its own.
/// </summary>
/// <remarks>
/// A transcript carries a full path, which a chat file edited outside the app may point anywhere. AI
/// Studio therefore deletes a file only where it keeps transcripts itself: in the transcript folder of a
/// chat, or in the staging area.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ManagedTranscriptAttachmentTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        // The data directory is static state of the whole application, which is why this fixture does not run alongside others:
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-transcripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        SettingsManager.DataDirectory = this.previousDataDirectory;

        try
        {
            Directory.Delete(this.testDirectory, true);
        }
        catch (IOException)
        {
            // A temporary directory we could not remove says nothing about the code under test.
        }
    }

    [Test]
    public void TheTranscriptOfAWorkspaceChatIsDeleted()
    {
        var chatDirectory = Path.Combine(this.testDirectory, "workspaces", Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var transcript = WriteTranscript(ManagedTranscriptAttachment.GetTranscriptDirectory(chatDirectory));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ManagedTranscriptAttachment.TryDeleteOwnedFile(transcript), Is.True);
            Assert.That(File.Exists(transcript.FilePath), Is.False);
        }
    }

    [Test]
    public void AFileInAnotherFolderOfTheChatStays()
    {
        var chatDirectory = Path.Combine(this.testDirectory, "workspaces", Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var file = WriteTranscript(Path.Combine(chatDirectory, "attachments", "images"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ManagedTranscriptAttachment.TryDeleteOwnedFile(file), Is.False, "Only the transcript folder belongs to the transcripts.");
            Assert.That(File.Exists(file.FilePath), Is.True);
        }
    }

    [Test]
    public void AFileOutsideTheDataDirectoryStays()
    {
        var outsideDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-elsewhere-{Guid.NewGuid():N}");
        try
        {
            var file = WriteTranscript(ManagedTranscriptAttachment.GetTranscriptDirectory(Path.Combine(outsideDirectory, "tempChats", Guid.NewGuid().ToString())));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ManagedTranscriptAttachment.TryDeleteOwnedFile(file), Is.False, "The same folders elsewhere belong to somebody else.");
                Assert.That(File.Exists(file.FilePath), Is.True);
            }
        }
        finally
        {
            Directory.Delete(outsideDirectory, true);
        }
    }

    /// <summary>
    /// Writes a transcript into a folder.
    /// </summary>
    /// <param name="directory">Where the transcript goes.</param>
    /// <returns>The transcript, as a chat attaches it to a message.</returns>
    private static ManagedTranscriptAttachment WriteTranscript(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "meeting-transcript-0001.md");
        File.WriteAllText(path, "# Transcription: meeting.mp4");
        return new ManagedTranscriptAttachment(Path.GetFileName(path), path, new FileInfo(path).Length, "meeting.mp4", false);
    }
}