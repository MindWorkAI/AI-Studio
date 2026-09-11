namespace AIStudio.Tools;

/// <summary>
/// Names the drop zone under the cursor of a running drag.
/// </summary>
/// <remarks>
/// Every zone receives this and compares the ID with its own: at most one zone is highlighted at a
/// time, and all others have to give their highlight up. A null ID means that the cursor is over no
/// zone at all, or that the drag has ended.
/// </remarks>
/// <param name="ZoneId">The ID of the zone under the cursor, or null when there is none.</param>
public readonly record struct DropZoneHighlight(string? ZoneId);