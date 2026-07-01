# Compatibility Shims

Compatibility shims are temporary fallback paths that keep older installations, settings, secrets, plugin data, or external integrations working while users move to a newer release.

Use this folder for short-lived compatibility code such as legacy aliases, read-repair logic, temporary import fallbacks, or cleanup paths. Do not use it for permanent settings schema migrations; those belong in `app/MindWork AI Studio/Settings/SettingsMigrations.cs`.

Every compatibility shim must have:

- A Markdown file in this folder.
- A clear status.
- An introduced date.
- A remove-after date.
- Code references.
- A short explanation of user impact.
- The compatibility behavior.
- A removal checklist.
- A short code comment near the shim that references the Markdown file and remove-after date.

## Template

```md
# Short Title

- Status: Active
- Introduced: YYYY-MM-DD
- Remove after: YYYY-MM-DD
- Code references:
  - path/to/file.cs

## User Impact

Describe who needs this compatibility path and what breaks without it.

## Compatibility Behavior

Describe the temporary fallback, alias, read-repair, or cleanup behavior.

## Removal Checklist

- Remove the temporary constants, fallback branches, aliases, or cleanup paths.
- Remove or update tests and static checks that mention the shim.
- Update this document's status to `Removed`.
- Add a changelog entry if removing the shim is user-visible.
```
