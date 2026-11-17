namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// What the protected data of an exported briefing tells about one of its pictures.
/// </summary>
/// <param name="Description">What the picture shows.</param>
/// <param name="AltText">The text for people who cannot see the picture.</param>
internal sealed record VisualBriefingAssetMetadata(string Description, string AltText);