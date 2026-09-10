# Legacy Singular Profile Fields

- Status: Active
- Introduced: 2026-09-10
- Remove after: 2027-03-10 for the settings/plugin-config keys; for the saved chat-thread field,
  remove only when pre-v27 chat JSON files are no longer supported (they may never be re-saved)
- Code references:
  - `app/MindWork AI Studio/Chat/ChatThread.cs` (`LegacySelectedProfile`)
  - `app/MindWork AI Studio/Settings/ManagedConfiguration.ProfileSelections.cs` (`TryProcessLegacyProfileIds`, `TryProcessLegacyProfilePreselection`)
  - `app/MindWork AI Studio/Settings/DataModel/DataDocumentAnalysisPolicy.cs` (the `hasLegacyProfile` branch, `TryNormalizeLegacyProfileId`)
  - `app/MindWork AI Studio/Assistants/VisualBriefing/VisualBriefingLocalSettings.cs` (`LegacyProfileId`)

## User Impact

Profile selection changed from a single profile to a set of profiles. Users, configuration
plugins, and saved data written by older app versions still use the singular field name
(`PreselectedProfile`, `SelectedProfile`, `ProfileId`). Without this compatibility path, upgrading
would silently drop an existing profile choice: saved chats would lose their profile, and
configuration plugins that were not yet updated to the plural field would stop preselecting a
profile at all.

## Compatibility Behavior

Each listed site accepts the old singular field alongside the new plural field and, when only the
old one is present, converts its single value into the new set-based representation:

- `ChatThread.LegacySelectedProfile` reads the old `SelectedProfile` JSON property of a saved chat
  and adds it to `SelectedProfileIds` if that set is still empty. New chats only ever write
  `SelectedProfileIds`.
- `ManagedConfiguration.ProfileSelections.cs`'s legacy processors read a configuration plugin's old
  singular `PreselectedProfile`/`PreselectedProfileIds`-adjacent key when the new plural key is not
  present, and convert it into the same `ISet<string>`/`HashSet<string>?` shape the plural key
  produces.
- `DataDocumentAnalysisPolicy`'s `hasLegacyProfile` branch does the same for a document analysis
  policy's `PreselectedProfile` table entry.
- `VisualBriefingLocalSettings.LegacyProfileId` does the same for a saved Visual Briefing project's
  `ProfileId` JSON property.

All four reject the combination of both the old and the new field being set at once (surfaced as a
configuration error), rather than silently preferring one over the other.

## Removal Checklist

- Remove `ChatThread.LegacySelectedProfile`, `TryProcessLegacyProfileIds`,
  `TryProcessLegacyProfilePreselection`, the `hasLegacyProfile`/`TryNormalizeLegacyProfileId`
  branch, and `VisualBriefingLocalSettings.LegacyProfileId`.
- Remove the legacy-key branches in `app/MindWork AI Studio/Tools/PluginSystem/PluginConfiguration.cs`
  that call the above legacy processors.
- Remove or update any tests and static checks that mention these legacy fields.
- Update this document's status to `Removed`.
- Add a changelog entry if removing the shim is user-visible (e.g. old configuration plugins that
  still use the singular key would then fail to load).
