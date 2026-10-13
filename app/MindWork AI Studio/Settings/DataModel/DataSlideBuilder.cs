using System.Linq.Expressions;

using AIStudio.Assistants.SlideBuilder;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores the settings of the Slide Planner Assistant.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public class DataSlideBuilder(Expression<Func<Data, DataSlideBuilder>>? configSelection = null)
{
    /// <summary>
    /// Initializes an unmanaged Slide Planner settings instance.
    /// </summary>
    public DataSlideBuilder() : this(null)
    {
    }

    /// <summary>
    /// The PowerPoint presentation or template (.pptx, .potx) whose layouts the slides use.
    /// </summary>
    /// <remarks>
    /// Empty for no template, which makes the assistant plan an outline only. Unlike the
    /// preselections below, the template applies whether preselection is enabled or not: an
    /// organization which provides its corporate template wants it used.
    /// </remarks>
    public string TemplatePath { get; set; } = ManagedConfiguration.Register(configSelection, value => value.TemplatePath, string.Empty);

    /// <summary>
    /// Preselect the footer text of the slides?
    /// </summary>
    public string PreselectedFooterText { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedFooterText, string.Empty);

    /// <summary>
    /// Preselect whether every chapter moves on to the next color variant of the template?
    /// </summary>
    public bool PreselectedChangeVariantPerChapter { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedChangeVariantPerChapter, false);

    /// <summary>
    /// Preselect any Slide Builder  options?
    /// </summary>
    public bool PreselectOptions { get; set; } = true;
    
    /// <summary>
    /// Preselect a profile?
    /// </summary>
    public string PreselectedProfile { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect a Slide Builder provider?
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;
        
    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; }
    
    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectedOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the audience profile?
    /// </summary>
    public AudienceProfile PreselectedAudienceProfile { get; set; }

    /// <summary>
    /// Preselect the audience age group?
    /// </summary>
    public AudienceAgeGroup PreselectedAudienceAgeGroup { get; set; }

    /// <summary>
    /// Preselect the audience organizational level?
    /// </summary>
    public AudienceOrganizationalLevel PreselectedAudienceOrganizationalLevel { get; set; }

    /// <summary>
    /// Preselect the audience expertise?
    /// </summary>
    public AudienceExpertise PreselectedAudienceExpertise { get; set; }
    
    /// <summary>
    /// Preselect any important aspects that the Slide Builder should take into account?
    /// </summary>
    public string PreselectedImportantAspects { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
}