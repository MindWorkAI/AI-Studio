using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that a chat whose messages were replaced at once deletes only the transcripts nothing refers to anymore.
/// </summary>
/// <remarks>
/// Compressing a chat in place replaces the earlier messages with their summary, and their transcripts would stay
/// behind in the folder of the chat. A transcript which the remaining messages or the draft still refer to must stay,
/// or the chat would point at a file which is gone.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ChatThreadTranscriptCleanupTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;
    private string transcriptDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        // The data directory is static state of the whole application, which is why this fixture does not run alongside others:
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-transcript-cleanup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;

        var chatDirectory = Path.Combine(this.testDirectory, "workspaces", Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        this.transcriptDirectory = ManagedTranscriptAttachment.GetTranscriptDirectory(chatDirectory);
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
    public void TheTranscriptOfAReplacedMessageIsDeleted()
    {
        var summarized = this.WriteTranscript("summarized.md");
        var kept = this.WriteTranscript("kept.md");
        var drafted = this.WriteTranscript("drafted.md");
        var chat = new ChatThread { Blocks = [Block(ChatRole.USER, summarized, 1), Block(ChatRole.USER, kept, 2)] };
        chat.Draft.AddAttachments([drafted]);

        var formerTranscripts = chat.ManagedTranscripts();
        chat.Blocks = [chat.Blocks[1]];
        chat.DeleteTranscriptsNoLongerReferenced(formerTranscripts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(formerTranscripts.Select(transcript => transcript.FileName), Is.EquivalentTo(new[] { "summarized.md", "kept.md", "drafted.md" }), "The draft holds transcripts as well.");
            Assert.That(File.Exists(summarized.FilePath), Is.False, "Nothing refers to the transcript of a summarized message anymore.");
            Assert.That(File.Exists(kept.FilePath), Is.True, "The last message keeps its files.");
            Assert.That(File.Exists(drafted.FilePath), Is.True, "The message being written keeps its files.");
        }
    }

    [Test]
    public void NothingIsDeletedWhileEveryTranscriptIsStillReferenced()
    {
        var transcript = this.WriteTranscript("transcript.md");
        var chat = new ChatThread { Blocks = [Block(ChatRole.USER, transcript, 1)] };

        chat.DeleteTranscriptsNoLongerReferenced(chat.ManagedTranscripts());

        Assert.That(File.Exists(transcript.FilePath), Is.True);
    }

    private ManagedTranscriptAttachment WriteTranscript(string fileName)
    {
        Directory.CreateDirectory(this.transcriptDirectory);
        var path = Path.Combine(this.transcriptDirectory, fileName);
        File.WriteAllText(path, "# Transcription: meeting.mp4");
        return new ManagedTranscriptAttachment(fileName, path, new FileInfo(path).Length, "meeting.mp4", false);
    }

    private static ContentBlock Block(ChatRole role, FileAttachment attachment, int minute) => new()
    {
        Time = DateTimeOffset.UnixEpoch.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Role = role,
        Content = new ContentText { Text = "A message with a transcript.", FileAttachments = [attachment] },
    };
}