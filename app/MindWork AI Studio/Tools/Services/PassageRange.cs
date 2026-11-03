namespace AIStudio.Tools.Services;

/// <summary>
/// The rows or lines a passage spans, as far as it names them, cf. DataSourceLocalRetrievalService.LocatePassage.
/// </summary>
/// <param name="Start">The first row or line, or null when the passage names none.</param>
/// <param name="End">The last row or line, or null when the passage does not go on beyond its first one.</param>
internal readonly record struct PassageRange(int? Start, int? End)
{
    /// <summary>
    /// The passage names no rows or lines.
    /// </summary>
    public static readonly PassageRange NONE = new(null, null);
}