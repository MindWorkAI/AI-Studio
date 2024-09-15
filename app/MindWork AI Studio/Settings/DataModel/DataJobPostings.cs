using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataJobPostings
{
    /// <summary>
    /// Do we want to preselect any translator options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// The mandatory information for the job posting.
    /// </summary>
    public string PreselectedMandatoryInformation { get; set; } = string.Empty;

    /// <summary>
    /// The job description.
    /// </summary>
    public string PreselectedJobDescription { get; set; } = string.Empty;

    /// <summary>
    /// The qualifications required for the job.
    /// </summary>
    public string PreselectedQualifications { get; set; } = string.Empty;

    /// <summary>
    /// What are the responsibilities of the job?
    /// </summary>
    public string PreselectedResponsibilities { get; set; } = string.Empty;

    /// <summary>
    /// Which company name should be preselected?
    /// </summary>
    public string PreselectedCompanyName { get; set; } = string.Empty;

    /// <summary>
    /// Where should the work be done?
    /// </summary>
    public string PreselectedWorkLocation { get; set; } = string.Empty;

    /// <summary>
    /// The preselected country legal framework.
    /// </summary>
    public string PreselectedCountryLegalFramework { get; set; } = string.Empty;
    
    /// <summary>
    /// Preselect the target language?
    /// </summary>
    public CommonLanguages PreselectedTargetLanguage { get; set; } = CommonLanguages.EN_US;

    /// <summary>
    /// Preselect any other language?
    /// </summary>
    public string PreselectOtherLanguage { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
    
    /// <summary>
    /// The preselected translator provider.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty; 
}