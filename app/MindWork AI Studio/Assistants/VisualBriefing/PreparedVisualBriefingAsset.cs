namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Describes one prepared visual asset while its Data URL remains outside persistent intermediate artifacts.
/// </summary>
/// <param name="AssetId">The stable asset identifier.</param>
/// <param name="DataUrl">The optimized Data URL used only during assembly.</param>
/// <param name="Width">The prepared pixel width.</param>
/// <param name="Height">The prepared pixel height.</param>
internal sealed record PreparedVisualBriefingAsset(
    string AssetId,
    string DataUrl,
    uint Width,
    uint Height);