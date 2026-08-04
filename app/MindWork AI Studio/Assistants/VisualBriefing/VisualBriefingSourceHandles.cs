namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Maps briefing sources to stable short handles used by model contracts.
/// </summary>
internal static class VisualBriefingSourceHandles
{
    /// <summary>
    /// Orders sources canonically and pairs them with their handles.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <returns>The handles and sources in canonical order.</returns>
    internal static IReadOnlyList<(string Handle, VisualBriefingSource Source)> Map(VisualBriefingManifest manifest) =>
    [
        .. manifest.Sources.OrderBy(source => source.SourceId).Select((source, index) => (Handle: Handle(index), Source: source))
    ];

    /// <summary>
    /// Names the handle at one zero-based canonical source position.
    /// </summary>
    /// <param name="index">The zero-based canonical position.</param>
    /// <returns>The source handle.</returns>
    private static string Handle(int index) => $"s{index + 1}";
}