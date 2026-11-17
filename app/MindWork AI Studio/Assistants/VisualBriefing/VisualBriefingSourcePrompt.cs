namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// A source as the evidence stage shows it its model.
/// </summary>
/// <remarks>
/// The model never sees the internal ID of a source, only its short handle.
/// </remarks>
/// <param name="SourceId">The short handle of the source.</param>
/// <param name="Kind">The kind of the source.</param>
/// <param name="AssetId">The asset ID of the source, or null when it is no picture of the briefing.</param>
/// <param name="Name">The file name of the source.</param>
internal sealed record VisualBriefingSourcePrompt(string SourceId, VisualBriefingSourceKind Kind, string? AssetId, string Name);