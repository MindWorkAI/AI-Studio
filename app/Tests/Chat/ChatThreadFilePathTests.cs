using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks that rewriting the file paths of a chat reaches every one of them, and nothing else.
/// </summary>
/// <remarks>
/// Moving a chat, copying it, and writing it into a backup or reading it back all change the paths
/// a chat stores. A path left out points at a place where the file is no longer, and the file drops
/// out of the chat without a word: a transcript of the message which is still being written, or an
/// image the model no longer gets to see.
/// </remarks>
[TestFixture]
public sealed class ChatThreadFilePathTests
{
    private static readonly DateTimeOffset START = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly string OLD_DIRECTORY = Path.Combine(Path.GetTempPath(), "old-chat");

    private static readonly string NEW_DIRECTORY = Path.Combine(Path.GetTempPath(), "new-chat");

    [Test]
    public void EveryPathOfTheChatIsRewritten()
    {
        var document = new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", Old("report.pdf"), 1024);
        var transcript = new ManagedTranscriptAttachment("meeting.md", Old("meeting.md"), 512, "meeting.mp3", false);
        var picture = new FileAttachmentImage("chart.png", Old("chart.png"), 2048);
        var photo = new ContentImage { SourceType = ContentImageSource.LOCAL_PATH, Source = Old("photo.jpg") };
        var chat = new ChatThread
        {
            ChatId = Guid.NewGuid(),
            Name = "Quarterly report",
            Blocks = [Block(new ContentText { Text = "Summarize the report", FileAttachments = [document, transcript, picture] }), Block(photo)],
        };
        chat.Draft.AddAttachments([new ManagedTranscriptAttachment("call.md", Old("call.md"), 256, "call.m4a", false)]);

        chat.RewriteFilePaths(Move);

        var message = (ContentText)chat.Blocks[0].Content!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.FileAttachments.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { New("report.pdf"), New("meeting.md"), New("chart.png") }));
            Assert.That(photo.Source, Is.EqualTo(New("photo.jpg")), "The model gets such an image from its path.");
            Assert.That(chat.Draft.FileAttachments.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { New("call.md") }), "The draft is the one place every path rewrite once forgot.");
        }
    }

    [Test]
    public void WhatTheRewriteLeavesAloneStaysAsItIs()
    {
        var elsewhere = new FileAttachment(FileAttachmentType.DOCUMENT, "notes.txt", Path.Combine(Path.GetTempPath(), "elsewhere", "notes.txt"), 64);
        var linked = new ContentImage { SourceType = ContentImageSource.URL, Source = "https://example.org/chart.png" };
        var chat = new ChatThread
        {
            ChatId = Guid.NewGuid(),
            Name = "Notes",
            Blocks = [Block(new ContentText { Text = "Read my notes", FileAttachments = [elsewhere] }), Block(linked)],
        };

        chat.RewriteFilePaths(Move);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((ContentText)chat.Blocks[0].Content!).FileAttachments.Single(), Is.SameAs(elsewhere));
            Assert.That(linked.Source, Is.EqualTo("https://example.org/chart.png"), "Only an image held by a local path has a path to rewrite.");
        }
    }

    [Test]
    public void AnAttachmentKeepsItsKindAtItsNewPlace()
    {
        var transcript = new ManagedTranscriptAttachment("meeting.md", Old("meeting.md"), 512, "meeting.mp3", true);
        var picture = new FileAttachmentImage("chart.png", Old("chart.png"), 2048);
        var document = new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", Old("report.pdf"), 1024);

        var movedTranscript = transcript.WithFilePath(New("meeting.md"));
        var movedPicture = picture.WithFilePath(New("chart.png"));
        var movedDocument = document.WithFilePath(New("report.pdf"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(movedTranscript, Is.EqualTo(new ManagedTranscriptAttachment("meeting.md", New("meeting.md"), 512, "meeting.mp3", true)), "A transcript has to stay a transcript, or deleting the chat leaves its file behind.");
            Assert.That(movedPicture, Is.TypeOf<FileAttachmentImage>());
            Assert.That(((FileAttachmentImage)movedPicture).Source, Is.EqualTo(New("chart.png")), "The model gets the image from its source, not from its path.");
            Assert.That(movedPicture.FilePathAsUrl, Is.EqualTo(new FileAttachmentImage("chart.png", New("chart.png"), 2048).FilePathAsUrl), "The preview of the image loads it from there.");
            Assert.That(movedDocument, Is.EqualTo(new FileAttachment(FileAttachmentType.DOCUMENT, "report.pdf", New("report.pdf"), 1024)));
        }
    }

    private static string Old(string fileName) => Path.Combine(OLD_DIRECTORY, fileName);

    private static string New(string fileName) => Path.Combine(NEW_DIRECTORY, fileName);

    private static string Move(string path) => path.StartsWith(OLD_DIRECTORY, StringComparison.Ordinal)
        ? Path.Combine(NEW_DIRECTORY, Path.GetRelativePath(OLD_DIRECTORY, path))
        : path;

    private static ContentBlock Block(IContent content) => new()
    {
        Time = START,
        ContentType = content is ContentImage ? ContentType.IMAGE : ContentType.TEXT,
        Content = content,
        Role = ChatRole.USER,
    };
}