using AIStudio.Assistants.SlideBuilder;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingExportManifest</c> for the visual briefing feature.
/// </summary>
public sealed class VisualBriefingExportManifest
{
    /// <summary>
    /// Defines <c>ArtifactVersion</c> for the visual briefing feature.
    /// </summary>
    public int ArtifactVersion { get; set; } = VisualBriefingVersions.ARTIFACT;

    /// <summary>
    /// Defines <c>SchemaVersion</c> for the visual briefing feature.
    /// </summary>
    public int SchemaVersion { get; set; } = VisualBriefingVersions.SCHEMA;

    /// <summary>
    /// Defines <c>RuntimeVersion</c> for the visual briefing feature.
    /// </summary>
    public int RuntimeVersion { get; set; } = VisualBriefingVersions.RUNTIME;

    /// <summary>
    /// Defines <c>BriefingId</c> for the visual briefing feature.
    /// </summary>
    public Guid BriefingId { get; set; }

    /// <summary>
    /// Defines <c>RevisionId</c> for the visual briefing feature.
    /// </summary>
    public Guid RevisionId { get; set; }

    /// <summary>
    /// Defines <c>ParentRevisionId</c> for the visual briefing feature.
    /// </summary>
    public Guid? ParentRevisionId { get; set; }

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
    /// Defines <c>TargetLanguage</c> for the visual briefing feature.
    /// </summary>
    public CommonLanguages TargetLanguage { get; set; }

    /// <summary>
    /// Defines <c>CustomTargetLanguage</c> for the visual briefing feature.
    /// </summary>
    public string CustomTargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>AudienceProfile</c> for the visual briefing feature.
    /// </summary>
    public AudienceProfile AudienceProfile { get; set; }

    /// <summary>
    /// Defines <c>AudienceAgeGroup</c> for the visual briefing feature.
    /// </summary>
    public AudienceAgeGroup AudienceAgeGroup { get; set; }

    /// <summary>
    /// Defines <c>AudienceOrganizationalLevel</c> for the visual briefing feature.
    /// </summary>
    public AudienceOrganizationalLevel AudienceOrganizationalLevel { get; set; }

    /// <summary>
    /// Defines <c>AudienceExpertise</c> for the visual briefing feature.
    /// </summary>
    public AudienceExpertise AudienceExpertise { get; set; }

    /// <summary>
    /// Defines <c>ShowSourceReferences</c> for the visual briefing feature.
    /// </summary>
    public bool ShowSourceReferences { get; set; }

    /// <summary>
    /// Defines <c>ProtectionLevel</c> for the visual briefing feature.
    /// </summary>
    public VisualBriefingProtectionLevel ProtectionLevel { get; set; }

    /// <summary>
    /// Defines <c>CustomProtectionLevel</c> for the visual briefing feature.
    /// </summary>
    public string CustomProtectionLevel { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>AIStudioVersion</c> for the visual briefing feature.
    /// </summary>
    public string AIStudioVersion { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>RuntimeAIStudioVersion</c> for the visual briefing feature.
    /// </summary>
    public string RuntimeAIStudioVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the complete standalone HTML document.
    /// </summary>
    public string DocumentHash { get; set; } = string.Empty;
}