# AGENTS.md

These instructions cover the plugin system in `app/MindWork AI Studio/Tools/PluginSystem/` and what
configuration plugins can provide. They add to the `AGENTS.md` in the repository root, which applies here
as well.

## Lua runtime

Every plugin runs in a Lua runtime of its own, created by `PluginFactory.CreateLuaState`, whatever its type
and whoever runs it: loading, starting, checking an import, and the assistant builder.

- A plugin loads the files of its own directory with `require`, through `PluginLoader`, also from
  subdirectories: `require("lib/helpers")` loads `lib/helpers.lua`. The dot notation `require("lib.helpers")`
  is not supported.
- No other file is within reach. The runtime gets `NoLuaFileSystem` as its file system, `dofile` and
  `loadfile` are removed, and code without a directory gets `NoModuleLoader`. With the default file system
  of Lua-CSharp, `require` would look for a module it does not find next to the plugin along `package.path`,
  relative to the working directory of the app, and `dofile` and `loadfile` would open any path.
- Do not open the `io`, `os`, or `debug` library, and do not give the runtime a file system. Whatever a
  plugin needs to reach outside of Lua comes as a host function which checks every access.
- `PluginModuleLoadingTests` checks both sides: what plugins must load, and what they must not reach. Run
  it after every change to the runtime, the module loader, or the opened libraries.

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
- **Managed configuration objects:** complex Lua tables that are persisted into `SettingsManager.ConfigurationData`, implement `IConfigurationObject`, and are described by a `PluginConfigurationObjectDescriptor` in `PluginConfigurationObjectDescriptors`. Reading them from a plugin and cleaning up the left-over ones go through these descriptors. Examples include providers, profiles, chat templates, data sources, and document analysis policies.
- **Live plugin content:** complex Lua tables that implement `ILivePluginContent` and are read live from running plugins instead of being persisted to `ConfigurationData`. Examples include `MANDATORY_INFOS` and `INTRODUCTIONS`. If live plugin content creates persistent side data, add a dedicated cleanup path for that side data, like mandatory-info acceptances.

When adding configuration plugin capabilities:
- For managed settings, update the corresponding data class in `app/MindWork AI Studio/Settings/DataModel/` to call `ManagedConfiguration.Register(...)` and process the setting in `PluginConfiguration.TryProcessConfiguration`. Cleaning up the setting when its configuration plugin was removed needs no extra step: `ManagedConfiguration.CleanupLeftOverManagedConfigurations(...)` iterates all registered settings. Do not add per-setting cleanup calls to `PluginFactory.Loading.LoadAll`. `ManagedSettingsCoverageTests` fails for a registered setting which no configuration plugin can manage; a setting which must stay out of their reach goes into its list `EXEMPT_SETTINGS`, together with the reason.
- For managed configuration objects, add the type to `PluginConfigurationObjectType.cs`, persist the objects in the appropriate `ConfigurationData` collection, and add a descriptor to `PluginConfigurationObjectDescriptors` and its list `ALL`: its Lua table, its parser, the collection, the next number, and where secrets are kept. Parsing and cleanup need no extra call; `PluginConfigurationObjectDescriptorsTests` fails for a type without a descriptor.
- For live plugin content, add a data type implementing `ILivePluginContent`, parse it in `PluginConfiguration`, expose it through `PluginFactory`, and add any required cleanup only for persistent side data.
- Always document the new capability in `app/MindWork AI Studio/Plugins/configuration/plugin.lua`, the reference of every setting, and describe it for IT departments in the section "Wiki" of the pull request; see "Wiki" in the root `AGENTS.md`.

## Importing exported configuration objects

Users can paste a configuration object which somebody exported, such as a chat template, and keep it as one
of their own. Three parts carry this:

- `ConfigurationSnippetParser` reads the pasted text as data and never creates a Lua runtime: the text comes
  from somebody else, so it earns none of the trust a deployed plugin has. It accepts only what an export
  writes, and it reads that the way Lua does. `ConfigurationSnippetParserTests` compares both for every
  export; extend the parser only together with that test, and never toward running code.
- `ConfigurationSnippetImport` reads the table through `TryParseSnippet` of the descriptor, i.e., with the
  parser a configuration plugin goes through, as a dry run. It then makes the object local: an ID of its own,
  no plugin. Do not add a second description of the fields for the import.
- `DataApp.CanImportConfigurationSnippet` decides who may import which type.

A snippet may name things outside of itself: a file, a server, another configuration object. None of them may
exist on this computer, and none of them may be touched before the user agreed to the import. The importer
therefore looks up no path on a network share and contacts no server; the forms do that once the user asks.

A new type of configuration object needs a decision on its import: a switch in `DataApp`, its case in
`CanImportConfigurationSnippet`, and its documentation in `plugin.lua`, plus its export in
`ConfigurationSnippetParserTests` and its case in `ConfigurationSnippetImport.TryImport`. The coverage tests in
`DataAppImportPermissionTests`, `ConfigurationSnippetParserTests`, and `ConfigurationSnippetImportTests` fail
for a type without them.