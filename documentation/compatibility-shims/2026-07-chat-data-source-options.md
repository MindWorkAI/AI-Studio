# Chat Data Source Options

- Status: Active
- Introduced: 2026-07-05
- Remove after: 2027-01-05
- Code references:
  - `app/MindWork AI Studio/Settings/DataModel/DataChat.cs`

## User Impact

Older settings files store default chat data source options in the nested `DataChat.PreselectedDataSourceOptions` object.

Without this shim, those defaults would be lost after upgrading to a version that exposes the individual data source default fields to configuration plugins.

## Compatibility Behavior

`DataChat.PreselectedDataSourceOptions` remains available as a compatibility property. Reading it maps the new individual fields into a `DataSourceOptions` object, and setting it copies values from the legacy nested object into the new fields.

`PreselectedDataSourcesRetrievalMode` came later than the other individual fields and is mapped the same way. A legacy-nested object never carried it, so setting the property from one gives semantic search, the default of both fields.

This lets older settings files load without a settings version migration and keeps existing UI bindings working while configuration plugins manage the individual fields.

## Removal Checklist

- Confirm supported settings files are expected to contain `PreselectedDataSourcesDisabled`, `PreselectedDataSourcesAutomaticSelection`, `PreselectedDataSourcesAutomaticValidation`, `PreselectedDataSourcesRetrievalMode`, and `PreselectedDataSourceIds`.
- Remove `DataChat.PreselectedDataSourceOptions`.
- Update any remaining callers to use the individual fields or a dedicated helper.
- Update this document's status to `Removed`.