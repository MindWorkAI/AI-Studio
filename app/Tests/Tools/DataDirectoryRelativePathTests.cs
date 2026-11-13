using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the paths of files below the data directory are stored relative to it, and every other path as it is.
/// </summary>
/// <remarks>
/// A backup of the data directory brings the stored paths to another computer, where the user name,
/// the OS, or the data directory itself may differ. An absolute path to a transcript or a tokenizer
/// copy leads nowhere there, so the transcript drops out of its chat, and the token count turns into
/// an estimate. Relative to the data directory, the path still names the same file.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class DataDirectoryRelativePathTests
{
    private static readonly DateTimeOffset START = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid WORKSPACE_ID = Guid.Parse("3f2b7c1e-9a4d-4e6b-8c2f-1d5e7a9b0c3d");

    private static readonly Guid CHAT_ID = Guid.Parse("8a1c4e7f-2b5d-4f9a-b3c6-0e2d4f6a8b1c");

    private static readonly string ELSEWHERE = Path.Combine(Path.GetTempPath(), "ai-studio-documents-elsewhere");

    private string? previousDataDirectory;
    private string dataDirectory = string.Empty;

    [SetUp]
    public void PrepareDataDirectory()
    {
        //
        // The data directory is static state of the whole application, which is why this fixture
        // does not run alongside others. Nothing is written to it: storing a path only asks where
        // the path lies.
        //
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-relative-paths-{Guid.NewGuid():N}");
        SettingsManager.DataDirectory = this.dataDirectory;
    }

    [TearDown]
    public void RestoreDataDirectory() => SettingsManager.DataDirectory = this.previousDataDirectory;

    [Test]
    public void AFileBelowTheDataDirectoryIsStoredRelative()
    {
        var transcriptPath = Path.Combine(this.dataDirectory, "workspaces", WORKSPACE_ID.ToString(), CHAT_ID.ToString(), "attachments", "transcripts", "meeting-transcript-0001.md");
        var picturePath = Path.Combine(this.dataDirectory, "plugins", "templates", "chart.png");
        var documentPath = Path.Combine(ELSEWHERE, "report.pdf");
        var message = new ContentText
        {
            Text = "Summarize the meeting",
            FileAttachments =
            [
                new ManagedTranscriptAttachment("meeting-transcript-0001.md", transcriptPath, 512, "meeting.mp3", false),
                new FileAttachmentImage("chart.png", picturePath, 2048),
                new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", documentPath, 1024),
            ],
        };
        var chat = new ChatThread
        {
            WorkspaceId = WORKSPACE_ID,
            ChatId = CHAT_ID,
            Name = "Meeting notes",
            Blocks = [new ContentBlock { Time = START, ContentType = ContentType.TEXT, Content = message, Role = ChatRole.USER }],
        };

        var json = JsonSerializer.Serialize(chat, WorkspaceBehaviour.JSON_OPTIONS);
        var attachments = ((ContentText)JsonSerializer.Deserialize<ChatThread>(json, WorkspaceBehaviour.JSON_OPTIONS)!.Blocks.Single().Content!).FileAttachments;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(json, Does.Contain($"\"workspaces/{WORKSPACE_ID}/{CHAT_ID}/attachments/transcripts/meeting-transcript-0001.md\""), "Forward slashes, so that every OS reads the path.");
            Assert.That(json, Does.Not.Contain(JsonEncodedText.Encode(this.dataDirectory).ToString()), "Not a single path may name the data directory of this computer.");
            Assert.That(json, Does.Contain(JsonEncodedText.Encode(documentPath).ToString()), "A file of the user stays where it is, so its path stays absolute.");
            Assert.That(attachments.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { transcriptPath, picturePath, documentPath }), "In memory, every path is absolute again.");
            Assert.That(attachments[0], Is.TypeOf<ManagedTranscriptAttachment>(), "A transcript has to stay a transcript, or deleting the chat leaves its file behind.");
            Assert.That(((FileAttachmentImage)attachments[1]).Source, Is.EqualTo(picturePath), "The model gets the image from its source, which has to follow the path.");
        }
    }

    [Test]
    public void ATokenizerCopyIsStoredRelative()
    {
        var tokenizerPath = Path.Combine(this.dataDirectory, "tokenizers", "openai-gpt-6", "tokenizer.json");
        var provider = new AIStudio.Settings.Provider(1, "provider", "Chat provider", LLMProviders.OPEN_AI, new("gpt-6", "gpt-6"), TokenizerPath: tokenizerPath);
        var embeddingProvider = new EmbeddingProvider(1, "embedding", "Embedding provider", LLMProviders.OPEN_AI, new("text-embedding-3-small", "text-embedding-3-small"), TokenizerPath: tokenizerPath);

        var providerJson = JsonSerializer.Serialize(provider, SettingsManager.JSON_OPTIONS);
        var embeddingProviderJson = JsonSerializer.Serialize(embeddingProvider, SettingsManager.JSON_OPTIONS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(providerJson, Does.Contain("\"tokenizers/openai-gpt-6/tokenizer.json\""));
            Assert.That(embeddingProviderJson, Does.Contain("\"tokenizers/openai-gpt-6/tokenizer.json\""));
            Assert.That(JsonSerializer.Deserialize<AIStudio.Settings.Provider>(providerJson, SettingsManager.JSON_OPTIONS)!.TokenizerPath, Is.EqualTo(tokenizerPath));
            Assert.That(JsonSerializer.Deserialize<EmbeddingProvider>(embeddingProviderJson, SettingsManager.JSON_OPTIONS)!.TokenizerPath, Is.EqualTo(tokenizerPath));
        }
    }

    [Test]
    public void AnAbsolutePathIsReadAsItIs()
    {
        // Stored before AI Studio stored such paths relative:
        var transcriptPath = Path.Combine(this.dataDirectory, "tempChats", CHAT_ID.ToString(), "attachments", "transcripts", "call-transcript-0001.md");
        var documentPath = Path.Combine(ELSEWHERE, "notes.txt");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataDirectoryRelativePathConverter.ToAbsolute(transcriptPath, this.dataDirectory), Is.EqualTo(transcriptPath), "On this computer, the path still names the transcript.");
            Assert.That(DataDirectoryRelativePathConverter.ToAbsolute(documentPath, this.dataDirectory), Is.EqualTo(documentPath));
        }
    }

    [Test]
    public void ARelativePathLeadingOutOfTheDataDirectoryIsNoPath()
    {
        Assert.That(DataDirectoryRelativePathConverter.ToAbsolute("workspaces/../../notes.txt", this.dataDirectory), Is.Empty, "AI Studio never writes such a path, so it must not name a file anywhere else.");
    }

    [Test]
    public void WithoutADataDirectoryEveryPathStaysAsItIs()
    {
        var transcriptPath = Path.Combine(this.dataDirectory, "tempChats", CHAT_ID.ToString(), "attachments", "transcripts", "call-transcript-0001.md");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(DataDirectoryRelativePathConverter.ToStored(transcriptPath, null), Is.EqualTo(transcriptPath));
            Assert.That(DataDirectoryRelativePathConverter.ToAbsolute("tokenizers/openai-gpt-6/tokenizer.json", null), Is.EqualTo("tokenizers/openai-gpt-6/tokenizer.json"));
        }
    }
}