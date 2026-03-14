using AIStudio.Assistants.SlideBuilder;
using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public class DataSlideBuilder
{
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