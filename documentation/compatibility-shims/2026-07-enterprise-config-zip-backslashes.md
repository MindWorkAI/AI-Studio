# Enterprise Configuration ZIP Backslashes

- Status: Active
- Introduced: 2026-07-09
- Remove after: when Microsoft fixes dotnet/runtime#27620 and dotnet/runtime#41914
- Code references:
  - `app/MindWork AI Studio/Tools/PluginSystem/PluginFactory.Download.cs`

## User Impact

Some enterprise administrators create configuration plugin ZIP files on Windows. Depending on the packaging tool, entries inside the ZIP may use Windows-style backslashes, for example `O\plugin.lua`.

Without this shim, Unix systems extract those entries as files whose names contain literal backslash characters. The plugin loader then cannot find `plugin.lua`, so the enterprise configuration plugin is not activated.

## Compatibility Behavior

AI Studio manually extracts downloaded enterprise configuration plugin ZIP files. During extraction, entry names are normalized so both `/` and `\` are treated as archive path separators.

The extraction still preserves the archive structure and validates each entry before writing it to disk. Rooted paths, drive-qualified paths, and parent-directory traversal paths are rejected.

This works around the behavior described in dotnet/runtime#27620. A related upstream context for ZIP entry creation is dotnet/runtime#41914, where the ZIP specification requirement for forward slashes is discussed.

## Removal Checklist

- Confirm supported .NET runtimes and administrator packaging guidance no longer require accepting backslashes in enterprise ZIP entry names.
- Replace the manual enterprise configuration plugin ZIP extraction with `ZipFile.ExtractToDirectory(...)`.
- Remove `ExtractConfigPluginArchive(...)`, `NormalizeConfigPluginZipEntryName(...)`, and `GetConfigPluginZipEntryDestinationPath(...)`.
- Update this document's status to `Removed`.
