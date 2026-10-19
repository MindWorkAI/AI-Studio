# Enterprise Encryption Export Format

- Status: Active
- Introduced: 2026-10-17
- Remove after: 2027-10-31
- Code references:
  - `app/MindWork AI Studio/Settings/SettingsManager.cs` (`KeepTheLegacyEncryptionExportFormat` and the migration path in `ReadSettingsSnapshot`)
  - `app/MindWork AI Studio/Settings/DataModel/DataApp.cs` (`EnterpriseEncryptionExportFormat`)
  - `app/Tests/Settings/EnterpriseEncryptionExportFormatTests.cs`

## User Impact

From v26.10.1 on, exports can encrypt API keys, tokens, and passwords in the authenticated format `ENC:v2:`. Versions up to v26.9.1 read only `ENC:v1:`: they take a v2 value for plaintext and leave the key out.

Large organizations package updates themselves, and it takes them weeks to roll one out. Their administrators run the new version first, because they test and approve it. When such an administrator exported a provider in v2, nobody else in the organization could use its key until the update arrived everywhere.

The setting `DataApp.EnterpriseEncryptionExportFormat` decides the format, and new installations start with v2. This shim makes installations which existed before v26.10.1 start with v1 instead, so that their exports keep working for everybody until an administrator switches.

## Compatibility Behavior

Settings written before v26.10.1 lack the property `App.EnterpriseEncryptionExportFormat`. When `SettingsManager` reads such settings, from the settings file or from its backup, it sets the format to v1. Settings of an older schema version always get v1 while they are migrated. Once the settings are stored, they hold the format, and the shim changes nothing anymore.

Reading `ENC:v1:` values is no part of this shim: AI Studio keeps reading them for good, because organizations deployed configuration plugins with them.

## Removal Checklist

- Remove `KeepTheLegacyEncryptionExportFormat` and its call in `SettingsManager.TryDeserializeCurrentSettings`.
- Remove the line which sets v1 after `SettingsMigrations.Migrate` in `SettingsManager.ReadSettingsSnapshot`.
- Remove the tests of `app/Tests/Settings/EnterpriseEncryptionExportFormatTests.cs` which expect v1 for settings without the property, and keep the others.
- Keep the setting `DataApp.EnterpriseEncryptionExportFormat` and the v1 format: both stay.
- Update this document's status to `Removed`.
- No changelog entry is needed: an installation which skipped every version from v26.10.1 until then starts with v2, like a new one.