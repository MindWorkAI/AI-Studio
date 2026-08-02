using AIStudio.Assistants.SlideBuilder;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingExportManifest</c> for the visual briefing feature.
/// </summary>
[CanonicalJsonShape("fc2235e8")]
public sealed class VisualBriefingExportManifest
{
    /// <summary>
    /// Defines <c>ArtifactVersion</c> for the visual briefing feature.
    /// </summary>
    public int ArtifactVersion { get; init; } = VisualBriefingVersions.ARTIFACT;

    /// <summary>
    /// Defines <c>SchemaVersion</c> for the visual briefing feature.
    /// </summary>
    public int SchemaVersion { get; init; } = VisualBriefingVersions.SCHEMA;

    /// <summary>
    /// Defines <c>RuntimeVersion</c> for the visual briefing feature.
    /// </summary>
    public int RuntimeVersion { get; init; } = VisualBriefingVersions.RUNTIME;

    /// <summary>
    /// Defines <c>BriefingId</c> for the visual briefing feature.
    /// </summary>
    public Guid BriefingId { get; init; }

    /// <summary>
    /// Defines <c>RevisionId</c> for the visual briefing feature.
    /// </summary>
    public Guid RevisionId { get; init; }

    /// <summary>
    /// Defines <c>ParentRevisionId</c> for the visual briefing feature.
    /// </summary>
    public Guid? ParentRevisionId { get; init; }

    /// <summary>
    /// Defines <c>Name</c> for the visual briefing feature.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Defines <c>Author</c> for the visual briefing feature.
    /// </summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Defines <c>CreatedAtUtc</c> for the visual briefing feature.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Defines <c>TargetLanguage</c> for the visual briefing feature.
    /// </summary>
    public CommonLanguages TargetLanguage { get; init; }

    /// <summary>
    /// Defines <c>CustomTargetLanguage</c> for the visual briefing feature.
    /// </summary>
    public string CustomTargetLanguage { get; init; } = string.Empty;

    /// <summary>
    /// Defines <c>AudienceProfile</c> for the visual briefing feature.
    /// </summary>
    public AudienceProfile AudienceProfile { get; init; }

    /// <summary>
    /// Defines <c>AudienceAgeGroup</c> for the visual briefing feature.
    /// </summary>
    public AudienceAgeGroup AudienceAgeGroup { get; init; }

    /// <summary>
    /// Defines <c>AudienceOrganizationalLevel</c> for the visual briefing feature.
    /// </summary>
    public AudienceOrganizationalLevel AudienceOrganizationalLevel { get; init; }

    /// <summary>
    /// Defines <c>AudienceExpertise</c> for the visual briefing feature.
    /// </summary>
    public AudienceExpertise AudienceExpertise { get; init; }

    /// <summary>
    /// Defines <c>ShowSourceReferences</c> for the visual briefing feature.
    /// </summary>
    public bool ShowSourceReferences { get; init; }

    /// <summary>
    /// Defines <c>ProtectionLevel</c> for the visual briefing feature.
    /// </summary>
    public VisualBriefingProtectionLevel ProtectionLevel { get; init; }

    /// <summary>
    /// Defines <c>CustomProtectionLevel</c> for the visual briefing feature.
    /// </summary>
    public string CustomProtectionLevel { get; init; } = string.Empty;

    /// <summary>
    /// Defines <c>AIStudioVersion</c> for the visual briefing feature.
    /// </summary>
    public string AIStudioVersion { get; init; } = string.Empty;

    /// <summary>
    /// Defines <c>RuntimeAIStudioVersion</c> for the visual briefing feature.
    /// </summary>
    public string RuntimeAIStudioVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the complete standalone HTML document.
    /// </summary>
    public string DocumentHash { get; set; } = string.Empty;
}