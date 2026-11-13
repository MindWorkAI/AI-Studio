namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// The content of a table slot.
/// </summary>
/// <param name="Headers">The column headers, which also give the number of columns.</param>
/// <param name="Rows">The rows below the header, each with one text per column.</param>
internal sealed record SlideTableSpec(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);