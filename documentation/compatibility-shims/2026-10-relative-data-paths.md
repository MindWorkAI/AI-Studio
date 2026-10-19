# Relative Data Paths

- Status: Active
- Introduced: 2026-10-14
- Remove after: 2027-04-14
- Code references:
  - `app/MindWork AI Studio/Tools/Services/RelativeDataPathsMigrationService.cs`
  - `app/MindWork AI Studio/Tools/WorkspaceBehaviour.RelativeDataPaths.cs`
  - `app/MindWork AI Studio/Settings/DataModel/Data.cs` (`StoredDataPathsAreRelative`)
  - `app/MindWork AI Studio/Program.cs` (registration of the service)
  - `app/Tests/Tools/RelativeDataPathsMigrationTests.cs`

## User Impact

Up to v26.9.1, AI Studio stored the paths of its own files below the data directory as absolute paths: the transcripts of a chat in its `thread.json`, and the copies of the tokenizers in the settings. From v26.10.1 on, it stores them relative to the data directory, so that a backup of the data directory works on another computer, under another user name, or on another OS.

AI Studio reads an absolute path as it is, so a chat stored by v26.9.1 keeps working on the same computer without this shim. It stores the path relative the next time it stores the chat. A chat which is never stored again, though, would keep its absolute paths, and a backup would bring them to a computer where they lead nowhere.

## Compatibility Behavior

`RelativeDataPathsMigrationService` runs once after the start. It reads and writes every chat once more, within one hold of the chat's lock, so that nothing AI Studio stores meanwhile gets lost, and it keeps the time of the chat's last change. Then it stores the settings and marks the work as done in `StoredDataPathsAreRelative`. When a chat is busy for too long or cannot be written, the flag stays unset, and the next start tries again.

A downgrade to v26.9.1 does not understand relative paths: it no longer finds the transcripts of the chats nor the tokenizers which were copied into the data directory.

## Removal Checklist

- Remove `RelativeDataPathsMigrationService` and its registration in `Program.cs`.
- Remove `WorkspaceBehaviour.RelativeDataPaths.cs`. When no other partial part of `WorkspaceBehaviour` is left, make the class non-partial again.
- Remove `Data.StoredDataPathsAreRelative`. Settings which still hold the property read fine without it.
- Remove `app/Tests/Tools/RelativeDataPathsMigrationTests.cs`.
- Keep `DataDirectoryRelativePathConverter`: it is no shim. Reading an absolute path stays necessary for the files of the user.
- Update this document's status to `Removed`.
- No changelog entry is needed: a chat which was not stored since then still works on the same computer.