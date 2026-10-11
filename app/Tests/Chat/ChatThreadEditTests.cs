using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks what editing a message does to the transcripts of its audio and video files.
/// </summary>
/// <remarks>
/// Editing hands a message back to the composer, attachments included, and removes it from the chat.
/// A transcript is a file the chat manages itself, and removing a message deletes such files. After
/// an edit, though, the composer still points at them, so the edit has to keep the files and hand them
/// over to the draft. Deleting a message goes on deleting them.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ChatThreadEditTests
{
    private static readonly DateTimeOffset START = new(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);

    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        //
        // The data directory is static state of the whole application, which is why this fixture
        // does not run alongside others. A transcript counts as one the chat manages only below that
        // directory; anywhere else, removing a message would never delete it, and the tests would
        // pass for the wrong reason.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-chat-edit-{Guid.NewGuid():N}");
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
    public void TheTranscriptOfAnEditedMessageStaysWithTheDraft()
    {
        var chatId = Guid.NewGuid();
        var transcript = this.CreateTranscript(chatId, "meeting");
        var question = Block(ChatRole.USER, "Summarize the meeting", 1, transcript);
        var answer = Block(ChatRole.AI, "The meeting was about", 2);
        var thread = new ChatThread { ChatId = chatId, Blocks = [question, answer] };
        var composer = new ChatComposerState();

        // What the chat does when the user stops the answer and edits the question:
        thread.RemoveForEditing(question.Content!);
        thread.Remove(answer.Content!);
        composer.RestoreFromDraft(thread.Draft);

        Assert.Multiple(() =>
        {
            Assert.That(thread.Blocks, Is.Empty, "Both the question and the stopped answer leave the chat.");
            Assert.That(File.Exists(transcript.FilePath), Is.True, "The composer still carries the transcript, so its file has to stay.");
            Assert.That(composer.FileAttachments.Single().Exists, Is.True, "The edited message goes out with the transcript, not with a reference to a deleted file.");
            Assert.That(thread.Draft.MediaTranscripts.Select(x => x.FilePath), Is.EqualTo(new[] { transcript.FilePath }), "The draft owns the transcript now: opening the chat again brings it back, and removing it from the composer deletes the file.");
            Assert.That(thread.Draft.Text, Is.EqualTo("Summarize the meeting"), "The message being edited is the draft now, its text included.");
        });
    }

    [Test]
    public void ATranscriptWhichIsAlreadyPendingIsNotAddedTwice()
    {
        var chatId = Guid.NewGuid();
        var transcript = this.CreateTranscript(chatId, "meeting");
        var question = Block(ChatRole.USER, "Summarize the meeting", 1, transcript);
        var thread = new ChatThread { ChatId = chatId, Blocks = [question] };
        thread.Draft.AddAttachments([transcript]);

        thread.RemoveForEditing(question.Content!);

        Assert.That(thread.Draft.FileAttachments, Has.Count.EqualTo(1), "The composer would show the same transcript twice otherwise.");
    }

    [Test]
    public void DeletingAMessageStillDeletesItsTranscript()
    {
        var chatId = Guid.NewGuid();
        var transcript = this.CreateTranscript(chatId, "meeting");
        var question = Block(ChatRole.USER, "Summarize the meeting", 1, transcript);
        var thread = new ChatThread { ChatId = chatId, Blocks = [question, Block(ChatRole.AI, "The meeting was about", 2)] };

        thread.Remove(question.Content!);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(transcript.FilePath), Is.False, "Nothing refers to the transcript of a deleted message any longer, so its file has to go.");
            Assert.That(thread.Draft.IsEmpty, Is.True, "A deleted message hands nothing over to the draft.");
        });
    }

    [Test]
    public void EditingAMessageOfAnotherChatChangesNothing()
    {
        var chatId = Guid.NewGuid();
        var transcript = this.CreateTranscript(chatId, "meeting");
        var thread = new ChatThread
        {
            ChatId = chatId,
            Blocks = [Block(ChatRole.USER, "Summarize the meeting", 1, transcript), Block(ChatRole.AI, "The meeting was about", 2)],
        };

        thread.RemoveForEditing(new ContentText { Text = "Question of another chat", FileAttachments = [transcript] });

        Assert.Multiple(() =>
        {
            Assert.That(thread.Blocks, Has.Count.EqualTo(2), "The message to edit is not part of this chat, so no block goes.");
            Assert.That(thread.Draft.IsEmpty, Is.True, "A message this chat does not hold never becomes its draft.");
            Assert.That(File.Exists(transcript.FilePath), Is.True);
        });
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

    private static ContentBlock Block(ChatRole role, string text, int minute, params FileAttachment[] attachments) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = text, FileAttachments = [..attachments] },
        Role = role,
    };
}