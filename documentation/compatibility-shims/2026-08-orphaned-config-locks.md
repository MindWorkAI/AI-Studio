# Orphaned Configuration Locks

- Status: Active
- Introduced: 2026-08-06
- Remove after: 2027-08-06
- Code references:
  - `app/MindWork AI Studio/Tools/PluginSystem/PluginFactory.Loading.cs` (`RepairLegacyConfigOnlySettings`, `RepairLegacyConfigOnlyFlag`, `RepairLegacyConfigOnlyCollection`)

## User Impact

Until this release, AI Studio persisted the value a configuration plugin had set, but not the information which plugin owned that value. After a restart, the ownership was lost. When the configuration plugin was removed in the meantime, the cleanup in `PluginFactory.LoadAll` could not recognize the value as left over, so it stayed active forever.

For most settings, this was an inconvenience only, because users can change them in the settings dialog. For settings without any user interface, it was a dead end: hidden assistants stayed hidden, adding providers stayed disabled, and the home page panels stayed switched off. The only workaround was to edit the settings file by hand.

Installations that lost the ownership this way cannot be repaired by the new persistence alone, because the missing information cannot be reconstructed. They need this one-time repair.

## Compatibility Behavior

At the end of `PluginFactory.LoadAll`, AI Studio checks a fixed list of settings. A setting is repaired when it is not managed by any configuration plugin at that moment and still holds a value that only a configuration plugin could have produced:

- `DataApp.ShowIntroduction`, `DataApp.ShowQuickStartGuide`, `DataApp.ShowLastChangelog`, `DataApp.ShowVision`, `DataApp.AllowUserToAddProvider`, `DataApp.AllowUserToImportPlugins`, `DataApp.AllowUserToSharePlugins`: enabled by default, so a disabled value is repaired.
- `DataApp.HiddenAssistants`, `DataSourceSecuritySettings.TrustedProviderIds`, `DataAssistantPluginAudit.EnterpriseApprovedPlugins`: empty by default, so a filled collection is repaired.

Repairing means restoring the default value. Each repair is logged as a warning.

Nothing is repaired at all while a configuration plugin is deployed but could not be loaded, e.g. because of invalid Lua code. In that situation, we cannot tell whether a value comes from that plugin or from a removed one, so the repair is postponed to the next start.

The check runs on every start, not once. This is safe because none of these settings has a user interface that writes to it, so a non-default value can only originate from a configuration plugin. This is the load-bearing assumption of the whole shim: as soon as one of these settings gets a user interface, the shim would overwrite the user's choice on every start. In that case, remove the setting from `RepairLegacyConfigOnlySettings` and from the list above.

Settings that a configuration plugin can lock but that users can change themselves are deliberately not part of this list. Their owner is persisted from this release on, and the regular left-over cleanup handles them.

## Removal Checklist

- Remove `RepairLegacyConfigOnlySettings`, `RepairLegacyConfigOnlyFlag`, and `RepairLegacyConfigOnlyCollection` from `PluginFactory.Loading.cs`, including the call and the comment in `LoadAll`.
- Update this document's status to `Removed`.
- No changelog entry is needed, because removing the shim is not user-visible.
