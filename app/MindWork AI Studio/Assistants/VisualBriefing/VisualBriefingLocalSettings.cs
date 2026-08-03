using AIStudio.Assistants.SlideBuilder;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Defines <c>VisualBriefingLocalSettings</c> for the visual briefing feature.
/// </summary>
public sealed class VisualBriefingLocalSettings
{
    /// <summary>
    /// Defines <c>ProviderId</c> for the visual briefing feature.
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>ModelId</c> for the visual briefing feature.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>ProfileId</c> for the visual briefing feature.
    /// </summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>TargetLanguage</c> for the visual briefing feature.
    /// </summary>
    public CommonLanguages TargetLanguage { get; set; } = CommonLanguages.EN_US;

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
    public bool ShowSourceReferences { get; set; } = true;

    /// <summary>
    /// Defines <c>OptimizeImages</c> for the visual briefing feature.
    /// </summary>
    public bool OptimizeImages { get; set; } = true;

    /// <summary>
    /// Defines <c>Instruction</c> for the visual briefing feature.
    /// </summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>
    /// Defines <c>ProtectionLevel</c> for the visual briefing feature.
    /// </summary>
    public VisualBriefingProtectionLevel ProtectionLevel { get; set; } = VisualBriefingProtectionLevel.INTERNAL;

    /// <summary>
    /// Defines <c>CustomProtectionLevel</c> for the visual briefing feature.
    /// </summary>
    public string CustomProtectionLevel { get; set; } = string.Empty;
}