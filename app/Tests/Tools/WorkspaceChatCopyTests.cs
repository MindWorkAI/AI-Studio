using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks what a copy of a chat makes of the message the user has not sent yet.
/// </summary>
/// <remarks>
/// The copy continues where the original stands, so it takes the draft along. A transcript of the
/// draft is a file the chat manages itself, though: removing it from the composer deletes the file.
/// Shared between both chats, a transcript removed in the copy would be missing in the original.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class WorkspaceChatCopyTests
{
    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        //
        // The data directory is static state of the whole application, which is why this fixture
        // does not run alongside others. The chats live below it, and so do their transcripts.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-chat-copy-{Guid.NewGuid():N}");
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
    public async Task TheCopyGetsADraftWithTranscriptsOfItsOwn()
    {
        var chatId = Guid.NewGuid();
        var transcript = this.CreateTranscript(chatId, "meeting");
        var original = new ChatThread { ChatId = chatId, Name = "Meeting notes", LastMediaTranscriptNumber = 1 };
        original.Draft.SetText("Summarize the meeting");
        original.Draft.AddAttachments([transcript]);

        var copy = await WorkspaceBehaviour.CopyChatAsync(original, "Copy of Meeting notes");

        var copyDirectory = Path.Combine(this.testDirectory, "tempChats", copy.ChatId.ToString());
        var copiedTranscript = copy.Draft.MediaTranscripts.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Draft, Is.Not.SameAs(original.Draft), "Writing in the copy must not change the message of the original.");
            Assert.That(copy.Draft.Text, Is.EqualTo("Summarize the meeting"), "The copy continues where the original stands, the message being written included.");
            Assert.That(copiedTranscript.FilePath, Does.StartWith(copyDirectory), "The copy has to own a transcript of its own.");
            Assert.That(File.Exists(copiedTranscript.FilePath), Is.True, "Otherwise the transcript drops out of the message of the copy.");
            Assert.That(original.Draft.MediaTranscripts.Single(), Is.EqualTo(transcript), "The original keeps its transcript, with its path.");
            Assert.That(File.Exists(transcript.FilePath), Is.True, "Copying a chat must not take anything away from the original.");
        }
    }

    /// <summary>
    /// Writes a transcript where a temporary chat keeps its transcripts.
    /// </summary>
    /// <param name="chatId">The chat which owns the transcript.</param>
    /// <param name="stem">The name of the media file, without its extension.</param>
    /// <returns>The transcript, as the chat attaches it to a message.</returns>
    private ManagedTranscriptAttachment CreateTranscript(Guid chatId, string stem)
    {
        var transcriptDirectory = Path.Combine(this.testDirectory, "tempChats", chatId.ToString(), "attachments", "transcripts");
        Directory.CreateDirectory(transcriptDirectory);

        var path = Path.Combine(transcriptDirectory, $"{stem}-transcript-0001.md");
        File.WriteAllText(path, $"# Transcription: {stem}.mp4{Environment.NewLine}{Environment.NewLine}Hello and welcome.");
        return new ManagedTranscriptAttachment(Path.GetFileName(path), path, new FileInfo(path).Length, $"{stem}.mp4", false);
    }
}