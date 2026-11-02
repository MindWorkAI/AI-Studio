using AIStudio.Chat;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingManifest</c> for the visual briefing feature.
/// </summary>
public sealed class VisualBriefingManifest
{
    /// <summary>
    /// Defines <c>ManifestVersion</c> for the visual briefing feature.
    /// </summary>
    public int ManifestVersion { get; set; } = VisualBriefingVersions.MANIFEST;

    /// <summary>
    /// Defines <c>BriefingId</c> for the visual briefing feature.
    /// </summary>
    public Guid BriefingId { get; set; }

    /// <summary>
    /// Defines <c>Name</c> for the visual briefing feature.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>Author</c> for the visual briefing feature.
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>CreatedAtUtc</c> for the visual briefing feature.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Defines <c>ModifiedAtUtc</c> for the visual briefing feature.
    /// </summary>
    public DateTimeOffset ModifiedAtUtc { get; set; }

    /// <summary>
    /// Defines <c>Settings</c> for the visual briefing feature.
    /// </summary>
    public VisualBriefingLocalSettings Settings { get; set; } = new();

    /// <summary>
    /// What the text sent to this briefing demands, because of where it came from.
    /// </summary>
    /// <remarks>
    /// An answer written from mails demands here what it demanded in its chat, and every stage of every build
    /// holds to it. The restrictions are set when the briefing is created for the text, and they never loosen:
    /// the text may still be part of the instruction, and of the versions built from it. They live here rather
    /// than in the settings, which the editor replaces with every save. A briefing nothing was sent to, or one
    /// written before this property existed, demands nothing.
    /// </remarks>
    public ChatRestrictions Restrictions { get; init; } = ChatRestrictions.NONE;

    /// <summary>
    /// Defines <c>Sources</c> for the visual briefing feature.
    /// </summary>
    public List<VisualBriefingSource> Sources { get; set; } = [];

    /// <summary>
    /// Defines <c>Versions</c> for the visual briefing feature.
    /// </summary>
    public List<VisualBriefingVersion> Versions { get; set; } = [];
}