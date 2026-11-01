using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that a chat stored before AI Studio stored paths relative gets them once, and nothing else changes.
/// </summary>
/// <remarks>
/// A chat which is never stored again would keep its absolute paths, and a backup of the data
/// directory would bring them to a computer where they lead nowhere. Writing the chat once more
/// must not look like a change, though: the workspaces sort their chats by the time of the last
/// change, and the maintenance of the temporary chats deletes them by it.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class RelativeDataPathsMigrationTests
{
    private static readonly DateTime LAST_EDIT_TIME = new(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc);

    private string? previousDataDirectory;
    private string testDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        //
        // The data directory is static state of the whole application, which is why this fixture
        // does not run alongside others. The workspace behaviour keeps the roots of the workspaces
        // and of the temporary chats fixed, as the app never changes its data directory, so it gets
        // told about the one of this test separately.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.testDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-relative-paths-migration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testDirectory);
        SettingsManager.DataDirectory = this.testDirectory;
        WorkspaceBehaviour.TestDataDirectory = this.testDirectory;
    }

    [TearDown]
    public void RemoveDataDirectory()
    {
        SettingsManager.DataDirectory = this.previousDataDirectory;
        WorkspaceBehaviour.TestDataDirectory = null;

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
    public async Task EveryStoredChatGetsRelativePathsAndKeepsItsTime()
    {
        var workspaceChat = this.StoreChatAsBefore(Guid.NewGuid(), Guid.NewGuid());
        var temporaryChat = this.StoreChatAsBefore(Guid.Empty, Guid.NewGuid());

        Assert.That(await WorkspaceBehaviour.StoreChatsWithRelativePathsAsync(CancellationToken.None), Is.True, "Nothing kept a chat busy, so every chat has to be done.");

        foreach (var chat in new[] { workspaceChat, temporaryChat })
        {
            var json = await File.ReadAllTextAsync(chat.ThreadPath);
            var stored = JsonSerializer.Deserialize<ChatThread>(json, WorkspaceBehaviour.JSON_OPTIONS)!;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(json, Does.Not.Contain(JsonEncodedText.Encode(this.testDirectory).ToString()), "Not a single path may name the data directory of this computer anymore.");
                Assert.That(stored.Draft.MediaTranscripts.Single().FilePath, Is.EqualTo(chat.TranscriptPath), "The draft still finds its transcript.");
                Assert.That(((ContentText)stored.Blocks.Single().Content!).FileAttachments.Single().FilePath, Is.EqualTo(chat.TranscriptPath), "The sent message still finds its transcript.");
                Assert.That(File.GetLastWriteTimeUtc(chat.ThreadPath), Is.EqualTo(LAST_EDIT_TIME), "Writing the chat once more is no change of the chat.");
            }
        }
    }

    /// <summary>
    /// Stores a chat with a transcript in a sent message and in its draft, with absolute paths as AI Studio stored them before.
    /// </summary>
    private StoredChat StoreChatAsBefore(Guid workspaceId, Guid chatId)
    {
        var chatDirectory = WorkspaceBehaviour.GetChatDirectory(workspaceId, chatId);
        var transcriptDirectory = Path.Combine(chatDirectory, "attachments", "transcripts");
        Directory.CreateDirectory(transcriptDirectory);

        var transcriptPath = Path.Combine(transcriptDirectory, "meeting-transcript-0001.md");
        File.WriteAllText(transcriptPath, $"# Transcription: meeting.mp4{Environment.NewLine}{Environment.NewLine}Hello and welcome.");
        var transcript = new ManagedTranscriptAttachment("meeting-transcript-0001.md", transcriptPath, new FileInfo(transcriptPath).Length, "meeting.mp4", false);

        var chat = new ChatThread
        {
            WorkspaceId = workspaceId,
            ChatId = chatId,
            Name = "Meeting notes",
            Blocks =
            [
                new ContentBlock
                {
                    Time = LAST_EDIT_TIME,
                    ContentType = ContentType.TEXT,
                    Content = new ContentText { Text = "Summarize the meeting", FileAttachments = [transcript] },
                    Role = ChatRole.USER,
                },
            ],
        };
        chat.Draft.AddAttachments([transcript]);

        // Without a known data directory, every path is stored as it is, i.e., absolute:
        SettingsManager.DataDirectory = null;
        var json = JsonSerializer.Serialize(chat, WorkspaceBehaviour.JSON_OPTIONS);
        SettingsManager.DataDirectory = this.testDirectory;

        var threadPath = Path.Combine(chatDirectory, "thread.json");
        File.WriteAllText(threadPath, json);
        File.SetLastWriteTimeUtc(threadPath, LAST_EDIT_TIME);
        return new(threadPath, transcriptPath);
    }

    private sealed record StoredChat(string ThreadPath, string TranscriptPath);
}