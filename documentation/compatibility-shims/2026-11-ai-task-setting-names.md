# Former Names of the AI Task Settings

- Status: Active
- Introduced: 2026-11-26
- Remove after: 2027-05-26
- Code references:
  - `app/MindWork AI Studio/Tools/PluginSystem/FormerSettingNames.cs`
  - `app/MindWork AI Studio/Tools/PluginSystem/PluginConfiguration.cs` (`TryProcessConfiguration`, the call of `FormerSettingNames.Resolve`)
  - `app/MindWork AI Studio/Plugins/configuration/plugin.lua` (the paragraph before the settings of the text content cleaner)
  - `app/Tests/Tools/PluginSystem/FormerSettingNamesTests.cs`

## User Impact

AI Studio v26.11.2 took the word "agent" out of the names of the AI tasks, because the agent mode needs that word for real agents. Seven settings which configuration plugins could already set in v26.11.1 changed their names with it:

| Name up to v26.11.1 | Name from v26.11.2 |
|---|---|
| `DataAgentDataSourceSelection.PreselectAgentOptions` | `DataDataSourceSelection.PreselectOptions` |
| `DataAgentDataSourceSelection.PreselectedAgentProvider` | `DataDataSourceSelection.PreselectedProvider` |
| `DataAgentRetrievalContextValidation.EnableRetrievalContextValidation` | `DataRetrievalContextValidation.EnableRetrievalContextValidation` |
| `DataAgentRetrievalContextValidation.PreselectAgentOptions` | `DataRetrievalContextValidation.PreselectOptions` |
| `DataAgentRetrievalContextValidation.PreselectedAgentProvider` | `DataRetrievalContextValidation.PreselectedProvider` |
| `DataAgentRetrievalContextValidation.NumParallelValidations` | `DataRetrievalContextValidation.NumParallelValidations` |
| `DataAssistantPluginAudit.PreselectedAgentProvider` | `DataAssistantPluginAudit.PreselectedProvider` |

Configuration plugins ignore a name they do not know without a word. Without this shim, an organization which updates to v26.11.2 would silently lose these settings, among them the provider which audits assistant plugins. An organization also rolls out a new version over weeks, and devices with v26.11.1 read only the former names, so its configuration plugin has to serve both versions meanwhile.

The other settings of the AI tasks, such as the minimum provider confidence or the settings of the text content cleaner, came after v26.11.1. No released version knew them under a former name, so they need no shim.

## Compatibility Behavior

Right after reading the table `SETTINGS` of a configuration plugin, `PluginConfiguration.TryProcessConfiguration` hands it to `FormerSettingNames.Resolve`. Everything after that, including the validation and the count of declared settings for the import preview, reads the table which `Resolve` returns:

- A setting the plugin sets under its former name only gets its current name. The same holds for its `.AllowUserOverride` companion.
- A setting the plugin sets under both names keeps the value of the current name; the former name is left out. This lets an organization set both names while its devices run both versions.
- For each setting set under its former name only, AI Studio logs a warning which names the plugin and the new name. A dry run, such as the preview of an import, logs nothing.

The settings file needs nothing of this: the settings migration to version V7 in `SettingsMigrations.MigrateV6ToV7` moves the locks, organization defaults, and snapshots of the user's values to the new names.

## Removal Checklist

- Remove `FormerSettingNames.cs`, its call in `PluginConfiguration.TryProcessConfiguration`, and the comment there.
- Remove the paragraph about the former names from the example `plugin.lua`.
- Remove `FormerSettingNamesTests.cs`.
- Update this document's status to `Removed`.
- Add a changelog entry: configuration plugins which still use the former names lose these settings.