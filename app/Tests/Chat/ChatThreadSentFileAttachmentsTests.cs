using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks which attachments the overview of all files of a chat lists.
/// </summary>
/// <remarks>
/// Every message keeps the text its documents were sent with. A file changed between two messages
/// reached the AI in two versions, and the overview has to show both, while a file sent again
/// unchanged is one version only.
/// </remarks>
[TestFixture]
public sealed class ChatThreadSentFileAttachmentsTests
{
    private static readonly DateTimeOffset START = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly string MINUTES_PATH = Path.Combine(Path.GetTempPath(), "board", "minutes.txt");

    private static readonly FileVersion FIRST_VERSION = new(new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc), 120);

    private static readonly FileVersion SECOND_VERSION = new(new DateTime(2026, 11, 5, 17, 30, 0, DateTimeKind.Utc), 180);

    [Test]
    public void AFileChangedBetweenTwoMessagesAppearsInBothVersions()
    {
        var first = Minutes(FIRST_VERSION);
        var second = Minutes(SECOND_VERSION);
        var thread = Thread(Message(1, first), Message(2, second));

        var attachments = thread.CollectSentFileAttachments();

        Assert.That(attachments, Is.EquivalentTo([first, second]), "The AI received both versions, so the overview has to show both.");
    }

    [Test]
    public void AFileSentAgainUnchangedAppearsOnceWithItsLatestMessage()
    {
        var earlier = Minutes(FIRST_VERSION);
        var later = Minutes(FIRST_VERSION);

        // The chat orders its messages by their time, not by their place in the list:
        var thread = Thread(Message(2, later), Message(1, earlier));

        var attachments = thread.CollectSentFileAttachments();

        Assert.That(attachments, Is.EquivalentTo([later]));
    }

    [Test]
    public void AnAttachmentWithoutANotedStateAppearsOncePerPath()
    {
        var imagePath = Path.Combine(Path.GetTempPath(), "board", "chart.png");
        var thread = Thread(Message(1, new FileAttachmentImage("chart.png", imagePath, 2048)), Message(2, new FileAttachmentImage("chart.png", imagePath, 4096)));

        var attachments = thread.CollectSentFileAttachments();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(attachments, Has.Count.EqualTo(1), "Every request reads such a file anew, so there is only one version of it.");
            Assert.That(attachments.Single().FileSizeBytes, Is.EqualTo(4096), "The latest message names the file as it was last.");
        }
    }

    [Test]
    public void TheDraftAndTheHiddenMessagesAreLeftOut()
    {
        var thread = Thread(HiddenMessage(1, Minutes(FIRST_VERSION)), Message(2));
        thread.Draft.MirrorAttachments([Minutes(SECOND_VERSION)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.HasSentFileAttachments(), Is.False);
            Assert.That(thread.CollectSentFileAttachments(), Is.Empty);
        }
    }

    [Test]
    public void AMessageWithAFileCountsAsSent()
    {
        var thread = Thread(Message(1), Message(2, Minutes(FIRST_VERSION)));
        Assert.That(thread.HasSentFileAttachments(), Is.True);
    }

    [Test]
    public void AChatWithoutAttachmentsHasNone()
    {
        var thread = Thread(Message(1), Message(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.HasSentFileAttachments(), Is.False);
            Assert.That(thread.CollectSentFileAttachments(), Is.Empty);
        }
    }

    private static FileAttachment Minutes(FileVersion version) => new(FileAttachmentType.DOCUMENT, "minutes.txt", MINUTES_PATH, version.Length)
    {
        ProvenanceId = Guid.NewGuid(),
        Version = version,
    };

    private static ChatThread Thread(params ContentBlock[] blocks) => new() { Blocks = [..blocks] };

    private static ContentBlock Message(int minute, params FileAttachment[] attachments) => Block(minute, false, attachments);

    private static ContentBlock HiddenMessage(int minute, params FileAttachment[] attachments) => Block(minute, true, attachments);

    private static ContentBlock Block(int minute, bool hidden, FileAttachment[] attachments) => new()
    {
        Time = START.AddMinutes(minute),
        ContentType = ContentType.TEXT,
        Content = new ContentText { Text = "Please have a look.", FileAttachments = [..attachments] },
        Role = ChatRole.USER,
        HideFromUser = hidden,
    };
}