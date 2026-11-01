using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Tools;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks the draft of a chat, the message the user has not sent yet.
/// </summary>
/// <remarks>
/// The draft is stored with its chat, so the message is still there when the user comes back to it.
/// Two parties change the draft: the user in the composer, and the media transcription service,
/// which adds a finished transcript before the composer shows it. That is why a transcript only
/// leaves the draft when the user removes it, or when the message is sent.
/// </remarks>
[TestFixture]
public sealed class ChatDraftTests
{
    private static readonly FileAttachment AGENDA = new(FileAttachmentType.DOCUMENT, "agenda.pdf", "/documents/agenda.pdf", 1024);
    private static readonly FileAttachment MINUTES = new(FileAttachmentType.DOCUMENT, "minutes.pdf", "/documents/minutes.pdf", 2048);
    private static readonly FileAttachmentImage CHART = new("chart.png", "/documents/chart.png", 4096);
    private static readonly ManagedTranscriptAttachment TRANSCRIPT = new("meeting-transcript-0001.md", "/data/tempChats/meeting-transcript-0001.md", 512, "meeting.mp4", false);
    private static readonly ManagedTranscriptAttachment STAGED_TRANSCRIPT = new("4f1c.md", "/data/media-staging/4f1c.md", 256, "kickoff.mp4", true);

    [Test]
    public void MirroringTheComposerKeepsATranscriptItDoesNotShowYet()
    {
        var draft = new ChatDraft();
        draft.AddAttachments([AGENDA]);

        // The transcription service finished, and the composer has not shown the transcript yet:
        draft.AddAttachments([TRANSCRIPT]);

        // Meanwhile, the user replaced the agenda by the minutes:
        draft.MirrorAttachments([MINUTES]);

        Assert.That(draft.FileAttachments, Is.EqualTo(new[] { MINUTES, TRANSCRIPT }), "The composer decides on the other attachments, but a transcript it does not show yet has to stay, or its file is left without an owner.");
    }

    [Test]
    public void AnAttachmentIsAddedOnlyOnce()
    {
        var draft = new ChatDraft();

        var added = draft.AddAttachments([TRANSCRIPT]);
        var addedAgain = draft.AddAttachments([TRANSCRIPT with { FileSizeBytes = 1024 }]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(added, Is.True);
            Assert.That(addedAgain, Is.False, "The same file counts once, even in a newer state.");
            Assert.That(draft.FileAttachments, Is.EqualTo(new FileAttachment[] { TRANSCRIPT }), "The composer would show the transcript twice otherwise.");
        }
    }

    [Test]
    public void RemovingAnAttachmentFindsItByItsPath()
    {
        var draft = new ChatDraft();
        draft.AddAttachments([AGENDA, TRANSCRIPT]);

        // The composer holds the transcript as it normalized it, with the current size of its file:
        draft.RemoveAttachments([TRANSCRIPT with { FileSizeBytes = 1024 }]);

        Assert.That(draft.FileAttachments, Is.EqualTo(new[] { AGENDA }), "The transcript the user removed must not come back when the chat is opened again.");
    }

    [Test]
    public void ReplacingTheAttachmentsKeepsTheirKind()
    {
        var draft = new ChatDraft();
        draft.AddAttachments([AGENDA, TRANSCRIPT]);

        draft.ReplaceAttachments(attachment => attachment with { FilePath = $"/moved{attachment.FilePath}" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft.FileAttachments.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { "/moved/documents/agenda.pdf", "/moved/data/tempChats/meeting-transcript-0001.md" }));
            Assert.That(draft.MediaTranscripts, Has.Count.EqualTo(1), "A transcript stays a transcript, so removing it from the composer still deletes its file.");
        }
    }

    [Test]
    public void EditingAMessageKeepsTheTranscriptsOfTheDraft()
    {
        var draft = new ChatDraft();
        draft.SetText("An older draft");
        draft.AddAttachments([AGENDA, TRANSCRIPT]);

        draft.RestoreFromMessage(new ContentText { Text = "Summarize the minutes", FileAttachments = [MINUTES] });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft.Text, Is.EqualTo("Summarize the minutes"), "The message to edit replaces the older draft.");
            Assert.That(draft.FileAttachments, Is.EqualTo(new[] { MINUTES, TRANSCRIPT }), "The draft is the only owner of its transcripts, so dropping them would leave their files behind.");
        }
    }

    [Test]
    public void SendingEmptiesTheDraft()
    {
        var draft = new ChatDraft();
        draft.SetText("Summarize the meeting");
        draft.AddAttachments([TRANSCRIPT]);
        draft.NotePreviewedText(new(TRANSCRIPT.FilePath, Guid.NewGuid(), new(DateTime.UtcNow, 42)));

        draft.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(draft.IsEmpty, Is.True, "The attachments belong to the sent message now, and a transcript has only one owner.");
            Assert.That(draft.PreviewedTexts, Is.Empty, "A text the preview read went with the message, or is of no use to the next one.");
        }
    }

    [Test]
    public void APreviewOfTheSameFileReplacesTheTextNotedBefore()
    {
        var draft = new ChatDraft();
        var firstPreview = new PreviewedText(AGENDA.FilePath, Guid.NewGuid(), new(new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc), 42));
        var otherFile = new PreviewedText(MINUTES.FilePath, Guid.NewGuid(), new(new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc), 42));
        var secondPreview = firstPreview with { ProvenanceId = Guid.NewGuid(), Version = new(new DateTime(2026, 10, 24, 10, 0, 0, DateTimeKind.Utc), 43) };

        var replacedByFirst = draft.NotePreviewedText(firstPreview);
        draft.NotePreviewedText(otherFile);
        var replacedBySecond = draft.NotePreviewedText(secondPreview);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(replacedByFirst, Is.Null);
            Assert.That(replacedBySecond, Is.EqualTo(firstPreview.ProvenanceId), "Nothing refers to the text of the first preview any longer, so the caller removes it from the store.");
            Assert.That(draft.PreviewedTexts, Is.EquivalentTo(new[] { otherFile, secondPreview }));
            Assert.That(draft.GetPreviewedText(AGENDA.FilePath), Is.EqualTo(secondPreview));
        }
    }

    [Test]
    public void AFileTakenOutTakesTheTextOfItsPreviewAlong()
    {
        var draft = new ChatDraft();
        var agendaPreview = new PreviewedText(AGENDA.FilePath, Guid.NewGuid(), new(new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc), 42));
        var minutesPreview = new PreviewedText(MINUTES.FilePath, Guid.NewGuid(), new(new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc), 42));
        draft.MirrorAttachments([AGENDA, MINUTES]);
        draft.NotePreviewedText(agendaPreview);
        draft.NotePreviewedText(minutesPreview);

        draft.MirrorAttachments([MINUTES]);
        var forgotten = draft.ForgetPreviewsOfDetachedFiles();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forgotten, Is.EqualTo(new[] { agendaPreview.ProvenanceId }), "Nothing refers to the text any longer, so the caller removes it from the store.");
            Assert.That(draft.PreviewedTexts, Is.EqualTo(new[] { minutesPreview }));
            Assert.That(draft.ForgetPreviewsOfDetachedFiles(), Is.Empty);
        }
    }

    [Test]
    public void TheTextsAPreviewReadAreStoredWithTheChat()
    {
        var thread = new ChatThread { ChatId = Guid.NewGuid(), Name = "Meeting notes" };
        var previewed = new PreviewedText(AGENDA.FilePath, Guid.NewGuid(), new(new DateTime(2026, 10, 24, 9, 30, 15, 123, DateTimeKind.Utc).AddTicks(4567), 42));
        thread.Draft.AddAttachments([AGENDA]);
        thread.Draft.NotePreviewedText(previewed);

        var loaded = JsonSerializer.Deserialize<ChatThread>(JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS), WorkspaceBehaviour.JSON_OPTIONS)!;

        Assert.That(loaded.Draft.PreviewedTexts, Is.EqualTo(new[] { previewed }), "Leaving the chat and coming back must not lose the text, and sending compares the time of the file to the tick.");
    }

    [Test]
    public void TheDraftKeepsTheKindOfEveryAttachmentWhenStored()
    {
        var thread = new ChatThread { ChatId = Guid.NewGuid(), Name = "Meeting notes" };
        thread.Draft.SetText("Summarize the meeting");
        thread.Draft.AddAttachments([AGENDA, CHART, STAGED_TRANSCRIPT]);

        var loaded = JsonSerializer.Deserialize<ChatThread>(JsonSerializer.Serialize(thread, WorkspaceBehaviour.JSON_OPTIONS), WorkspaceBehaviour.JSON_OPTIONS)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Draft.Text, Is.EqualTo("Summarize the meeting"));
            Assert.That(loaded.Draft.FileAttachments, Is.EqualTo(new[] { AGENDA, CHART, STAGED_TRANSCRIPT }), "A transcript loaded as a plain document would no longer be managed, and its file would never be deleted.");
            Assert.That(loaded.Draft.MediaTranscripts.Single().IsStaged, Is.True, "A staged transcript has to move into the chat directory once the chat is stored.");
        }
    }

    [Test]
    public void AChatOfTheFormerVersionBringsItsPendingTranscriptsIntoTheDraft()
    {
        // How v26.9.1 stored a chat with a transcript which was not sent yet, without a type discriminator:
        const string CHAT_WITH_PENDING_TRANSCRIPTS = """
                                                     {
                                                       "chat_id": "2b7c9e1f-4a6d-4c8b-9e3f-1d5a7c9b2e4f",
                                                       "pending_media_transcripts": [
                                                         {
                                                           "original_file_name": "meeting.mp4",
                                                           "is_staged": false,
                                                           "type": "DOCUMENT",
                                                           "file_name": "meeting-transcript-0001.md",
                                                           "file_path": "/data/tempChats/meeting-transcript-0001.md",
                                                           "file_size_bytes": 512,
                                                           "is_forbidden": false,
                                                           "is_valid": true,
                                                           "is_image": false,
                                                           "file_path_as_url": "/local/file?path=%2Fdata%2FtempChats%2Fmeeting-transcript-0001.md",
                                                           "exists": true
                                                         }
                                                       ],
                                                       "name": "Meeting notes",
                                                       "blocks": []
                                                     }
                                                     """;

        var loaded = JsonSerializer.Deserialize<ChatThread>(CHAT_WITH_PENDING_TRANSCRIPTS, WorkspaceBehaviour.JSON_OPTIONS)!;
        var stored = JsonSerializer.Serialize(loaded, WorkspaceBehaviour.JSON_OPTIONS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.Draft.FileAttachments, Is.EqualTo(new FileAttachment[] { TRANSCRIPT }), "Opening the chat has to bring the transcript back into the composer.");
            Assert.That(stored, Does.Not.Contain("pending_media_transcripts"), "The draft holds the transcript now, so storing the chat must not write the former list again.");
            Assert.That(stored, Does.Contain("\"draft\""));
        }
    }

    [Test]
    public void AChatWithoutADraftOpensWithAnEmptyOne()
    {
        const string CHAT_WITHOUT_DRAFT = """
                                          {
                                            "chat_id": "2b7c9e1f-4a6d-4c8b-9e3f-1d5a7c9b2e4f",
                                            "name": "An older chat",
                                            "blocks": []
                                          }
                                          """;

        var loaded = JsonSerializer.Deserialize<ChatThread>(CHAT_WITHOUT_DRAFT, WorkspaceBehaviour.JSON_OPTIONS);

        Assert.That(loaded?.Draft.IsEmpty, Is.True);
    }

    [Test]
    public void ACopyOfTheSameChatSharesTheDraft()
    {
        var thread = new ChatThread { ChatId = Guid.NewGuid() };

        // What the chat does when the user picks another profile:
        var withOtherProfile = thread with { SelectedProfile = Guid.NewGuid().ToString() };
        withOtherProfile.Draft.SetText("Summarize the meeting");

        Assert.That(thread.Draft.Text, Is.EqualTo("Summarize the meeting"), "Both are the same chat, and the transcription service may still hold the former one.");
    }
}