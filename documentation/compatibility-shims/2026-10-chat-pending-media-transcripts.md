# Chat Pending Media Transcripts

- Status: Active
- Introduced: 2026-10-11
- Remove after: 2027-04-11
- Code references:
  - `app/MindWork AI Studio/Chat/ChatThread.cs` (`LegacyPendingMediaTranscripts`)
  - `app/Tests/Chat/ChatDraftTests.cs`

## User Impact

Up to v26.9.1, a chat stored only the transcripts of the message the user had not sent yet, in the list `pending_media_transcripts` of its `thread.json`. From v26.10.1 on, a chat stores the whole message as its draft instead, text and attachments, in `draft`.

Without this shim, a chat stored by v26.9.1 with an audio or video file that was transcribed but not sent yet would open with an empty composer. The transcript file would stay on the disk, without any message referring to it.

## Compatibility Behavior

`ChatThread.LegacyPendingMediaTranscripts` reads the former list and adds its transcripts to the draft. It never writes the list again, so the next time AI Studio stores the chat, only `draft` remains.

The list is read as a list of `ManagedTranscriptAttachment`, because the former entries carry no `$type`: their declared type was already the transcript type, so the serializer never wrote one.

A downgrade to v26.9.1 ignores `draft`, so a message which was not sent yet does not come back there.

## Removal Checklist

- Remove `ChatThread.LegacyPendingMediaTranscripts`, including its comment.
- Remove the tests which read a chat with `pending_media_transcripts` from `app/Tests/Chat/ChatDraftTests.cs`.
- Update this document's status to `Removed`.
- No changelog entry is needed: a chat which still holds the former list loses only the transcripts of a message which was not sent for at least half a year.