# AGENTS.md

These instructions cover the plugin system in `app/MindWork AI Studio/Tools/PluginSystem/` and what
configuration plugins can provide. They add to the `AGENTS.md` in the repository root, which applies here
as well.

## Configuration plugins

Plugins can configure:
- Self-hosted LLM providers
- Update behavior
- Preview features visibility
- Preselected profiles
- Chat templates
- etc.

Configuration plugins provide three kinds of values:
- **Managed settings:** simple values such as booleans, numbers, strings, enums, lists, or sets handled through `ManagedConfiguration`. These values may be locked or used as organization defaults. Which configuration plugin owns a locked setting is persisted in `Data.ManagedLockedConfigurations`, and organization defaults are tracked in `Data.ManagedEditableDefaults`. Both are cleaned up generically by `ManagedConfiguration.CleanupLeftOverManagedConfigurations(...)` when the owning plugin is gone. The value a setting had before a configuration plugin took it over is kept in `Data.ManagedUserValueSnapshots` and restored by that same clean-up, so removing a plugin hands the user's own value back instead of the app default.
- **Managed configuration objects:** complex Lua tables that are persisted into `SettingsManager.ConfigurationData`, implement `IConfigurationObject`, and are cleaned up through `PluginConfigurationObject.CleanLeftOverConfigurationObjects(...)`. Examples include providers, profiles, chat templates, data sources, and document analysis policies.
- **Live plugin content:** complex Lua tables that implement `ILivePluginContent` and are read live from running plugins instead of being persisted to `ConfigurationData`. Examples include `MANDATORY_INFOS` and `INTRODUCTIONS`. If live plugin content creates persistent side data, add a dedicated cleanup path for that side data, like mandatory-info acceptances.

When adding configuration plugin capabilities:
- For managed settings, update the corresponding data class in `app/MindWork AI Studio/Settings/DataModel/` to call `ManagedConfiguration.Register(...)` and process the setting in `PluginConfiguration.TryProcessConfiguration`. Cleaning up the setting when its configuration plugin was removed needs no extra step: `ManagedConfiguration.CleanupLeftOverManagedConfigurations(...)` iterates all registered settings. Do not add per-setting cleanup calls to `PluginFactory.Loading.LoadAll`.
- For managed configuration objects, update `PluginConfigurationObject.cs` and `PluginConfigurationObjectType.cs`, persist them in the appropriate `ConfigurationData` collection, and add cleanup via `PluginConfigurationObject.CleanLeftOverConfigurationObjects(...)`.
- For live plugin content, add a data type implementing `ILivePluginContent`, parse it in `PluginConfiguration`, expose it through `PluginFactory`, and add any required cleanup only for persistent side data.
- Always document the new capability in `app/MindWork AI Studio/Plugins/configuration/plugin.lua`.
