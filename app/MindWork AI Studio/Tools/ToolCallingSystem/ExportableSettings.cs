namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// One independently selectable area of a tool's configuration export.
/// </summary>
/// <param name="Id">A stable ID, independent of the translated label. The empty ID denotes ungrouped settings.</param>
/// <param name="Label">The translated name shown to the administrator.</param>
/// <param name="FieldNames">Settings schema field names, without the tool ID prefix.</param>
public sealed record ExportableSettings(string Id, string Label, IReadOnlyList<string> FieldNames);