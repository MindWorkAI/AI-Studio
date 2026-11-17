namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// A source as the fingerprint of the editor names it.
/// </summary>
/// <param name="Path">The path of the source.</param>
/// <param name="Kind">The kind of the source.</param>
internal sealed record VisualBriefingSourceFingerprint(string Path, VisualBriefingSourceKind Kind);