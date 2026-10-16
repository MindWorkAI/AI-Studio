using System.Linq.Expressions;

using AIStudio.Assistants.SlideBuilder;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores managed default settings for the Visual Briefing Assistant.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataVisualBriefing(Expression<Func<Data, DataVisualBriefing>>? configSelection = null)
{
    /// <summary>
    /// Initializes an unmanaged Visual Briefing settings instance.
    /// </summary>
    public DataVisualBriefing() : this(null)
    {
    }

    /// <summary>
    /// Gets or sets the preselected profile identifier.
    /// </summary>
    public string PreselectedProfile { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedProfile, string.Empty);

    /// <summary>
    /// Gets or sets the preselected provider identifier.
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedProvider, string.Empty);

    /// <summary>
    /// Gets or sets the default target language.
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedTargetLanguage, CommonLanguages.EN_US);

    /// <summary>
    /// Gets or sets the default free-form target language.
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedOtherLanguage, string.Empty);

    /// <summary>
    /// Gets or sets the default audience profile.
    /// </summary>
    public AudienceProfile PreselectedAudienceProfile { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedAudienceProfile, AudienceProfile.UNSPECIFIED);

    /// <summary>
    /// Gets or sets the default audience age group.
    /// </summary>
    public AudienceAgeGroup PreselectedAudienceAgeGroup { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedAudienceAgeGroup, AudienceAgeGroup.UNSPECIFIED);

    /// <summary>
    /// Gets or sets the default audience organizational level.
    /// </summary>
    public AudienceOrganizationalLevel PreselectedAudienceOrganizationalLevel { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedAudienceOrganizationalLevel, AudienceOrganizationalLevel.UNSPECIFIED);

    /// <summary>
    /// Gets or sets the default audience expertise.
    /// </summary>
    public AudienceExpertise PreselectedAudienceExpertise { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedAudienceExpertise, AudienceExpertise.UNSPECIFIED);

    /// <summary>
    /// Gets or sets whether generated briefings show source references by default.
    /// </summary>
    public bool ShowSourceReferences { get; set; } = ManagedConfiguration.Register(configSelection, value => value.ShowSourceReferences, true);

    /// <summary>
    /// Gets or sets whether visual assets are optimized by default.
    /// </summary>
    public bool OptimizeImages { get; set; } = ManagedConfiguration.Register(configSelection, value => value.OptimizeImages, true);

    /// <summary>
    /// Gets or sets the minimum confidence accepted for the selected provider.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, value => value.MinimumProviderConfidence, ConfidenceLevel.NONE);
}