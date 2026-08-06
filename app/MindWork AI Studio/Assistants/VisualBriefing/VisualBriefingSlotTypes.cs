using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Derives and validates the required JSON shape of every planned content slot.
/// </summary>
internal static class VisualBriefingSlotTypes
{
    /// <summary>
    /// Determines the slot type of one planned semantic slot.
    /// </summary>
    /// <param name="slot">The planned semantic slot.</param>
    /// <returns>The required slot type.</returns>
    internal static VisualBriefingSlotType Expected(VisualBriefingPlanSlot slot) => slot.Role switch
    {
        VisualBriefingSlotRole.TABLE_DATA => VisualBriefingSlotType.TABLE,
        VisualBriefingSlotRole.TIMELINE_DATA => VisualBriefingSlotType.TIMELINE,
        _ => VisualBriefingSlotType.TEXT,
    };

    /// <summary>
    /// Determines whether a slot carries the tabular data of a table component.
    /// </summary>
    /// <param name="component">The planned component owning the slot.</param>
    /// <param name="slotId">The planned slot identifier.</param>
    /// <returns>Whether the slot carries tabular data.</returns>
    internal static bool IsTableDataSlot(VisualBriefingPlanComponent component, string slotId) =>
        component.Slots.Any(slot => slot.Role is VisualBriefingSlotRole.TABLE_DATA && string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));

    /// <summary>
    /// Maps every planned slot to its required slot type.
    /// </summary>
    /// <param name="sections">The planned sections.</param>
    /// <returns>The slot types keyed by slot identifier.</returns>
    internal static Dictionary<string, VisualBriefingSlotType> Map(IReadOnlyList<VisualBriefingPlanSection> sections)
    {
        Dictionary<string, VisualBriefingSlotType> types = new(StringComparer.Ordinal);
        foreach (var section in sections)
        {
            types[section.TitleSlotId] = VisualBriefingSlotType.TEXT;
            types[section.SummarySlotId] = VisualBriefingSlotType.TEXT;
        }
        
        foreach (var slot in sections.SelectMany(section => section.Components).SelectMany(component => component.Slots))
            types[slot.SlotId] = Expected(slot);

        return types;
    }

    /// <summary>
    /// Describes the required JSON shape of a slot type.
    /// </summary>
    /// <param name="type">The slot type.</param>
    /// <returns>The human-readable shape description.</returns>
    internal static string Describe(VisualBriefingSlotType type) => type switch
    {
        VisualBriefingSlotType.TABLE => "object with a columns array and a rows array of cells arrays",
        VisualBriefingSlotType.TIMELINE => "object with an items array of period, title, and description strings",
        _ => "string, number, or boolean",
    };

    /// <summary>
    /// Checks a slot value against its required slot type.
    /// </summary>
    /// <param name="type">The required slot type.</param>
    /// <param name="value">The slot value returned by the model.</param>
    /// <returns>A short reason when the value does not match, otherwise an empty string.</returns>
    internal static string Validate(VisualBriefingSlotType type, JsonElement value)
    {
        if (type is VisualBriefingSlotType.TEXT)
            return value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? string.Empty : "A text slot requires a string, number, or boolean value.";

        if (type is VisualBriefingSlotType.TIMELINE)
            return ValidateTimeline(value);

        if (value.ValueKind is not JsonValueKind.Object)
            return "A table slot requires an object with columns and rows.";

        if (value.EnumerateObject().Any(property => property.Name is not "columns" and not "rows"))
            return "A table slot must contain only columns and rows.";

        if (!value.TryGetProperty("columns", out var columns) || columns.ValueKind is not JsonValueKind.Array || columns.GetArrayLength() == 0)
            return "A table slot requires a non-empty columns array.";

        if (columns.EnumerateArray().Any(column => column.ValueKind is not JsonValueKind.String || string.IsNullOrWhiteSpace(column.GetString())))
            return "Every table column requires a non-empty name.";

        if (!value.TryGetProperty("rows", out var rows) || rows.ValueKind is not JsonValueKind.Array)
            return "A table slot requires a rows array.";

        var columnCount = columns.GetArrayLength();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind is not JsonValueKind.Object || row.EnumerateObject().Any(property => property.Name is not "cells"))
                return "Every table row requires exactly one cells array.";

            if (!row.TryGetProperty("cells", out var cells) || cells.ValueKind is not JsonValueKind.Array)
                return "Every table row requires a cells array.";

            if (cells.GetArrayLength() != columnCount)
                return "Every table row requires exactly one cell per column.";

            if (cells.EnumerateArray().Any(cell =>
                    cell.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)))
                return "Every table cell requires a string, number, or boolean value.";
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks the fixed timeline content shape used by the deterministic compiler.
    /// </summary>
    /// <param name="value">The timeline slot value returned by the model.</param>
    /// <returns>A short reason when the value does not match, otherwise an empty string.</returns>
    private static string ValidateTimeline(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.Object || value.EnumerateObject().Select(property => property.Name).ToArray() is not ["items"])
            return "A timeline slot requires exactly one items array.";

        var items = value.GetProperty("items");
        if (items.ValueKind is not JsonValueKind.Array || items.GetArrayLength() < 2)
            return "A timeline requires at least two ordered items.";

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind is not JsonValueKind.Object)
                return "Every timeline item requires period, title, and description strings.";

            var properties = item.EnumerateObject().Select(property => property.Name).ToArray();
            if (properties.Length != 3 || !properties.ToHashSet(StringComparer.Ordinal).SetEquals(["period", "title", "description"]))
                return "Every timeline item requires exactly period, title, and description.";

            if (properties.Any(property => item.GetProperty(property).ValueKind is not JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetProperty(property).GetString())))
                return "Every timeline period, title, and description requires a non-empty string.";
        }

        return string.Empty;
    }
}