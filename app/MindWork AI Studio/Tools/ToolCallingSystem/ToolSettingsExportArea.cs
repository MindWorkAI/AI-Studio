namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// One independently selectable area in the export of a tool collection: one area of one of its tools.
/// </summary>
/// <param name="Id">Unique within the export of the collection, made of the tool's ID and the area's ID.</param>
/// <param name="Label">The translated name shown to the administrator, naming the tool when the collection has several with settings.</param>
/// <param name="Tool">The tool whose settings the area holds.</param>
/// <param name="Area">The area as the tool divides its settings.</param>
public sealed record ToolSettingsExportArea(string Id, string Label, ToolCatalogTool Tool, ExportableSettings Area);