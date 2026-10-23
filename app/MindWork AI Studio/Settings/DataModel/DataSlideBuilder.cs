using System.Linq.Expressions;

using AIStudio.Assistants.SlideBuilder;
using AIStudio.Provider;
using AIStudio.Tools.PowerPoint;

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
    /// Empty for no template, which makes the assistant plan an outline only. Like the other
    /// preselections, the template applies only while preselection is enabled: an organization
    /// which wants its corporate template used for sure locks PreselectOptions as well.
    /// </remarks>
    public string TemplatePath { get; set; } = ManagedConfiguration.Register(configSelection, value => value.TemplatePath, string.Empty);

    /// <summary>
    /// Preselect the footer text of the slides?
    /// </summary>
    public string PreselectedFooterText { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedFooterText, string.Empty);

    /// <summary>
    /// Preselect the layouts for kinds of slides, i.e., the name of a layout of the template by kind of slide?
    /// </summary>
    /// <remarks>
    /// A kind of slide which is missing has no preference, and the LLM decides. Names instead of IDs
    /// keep the choices valid across sessions, computers, and versions of the template; a name which
    /// the current template lacks counts as no preference.
    /// </remarks>
    public Dictionary<LayoutPurpose, string> PreselectedLayouts { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedLayouts, new Dictionary<LayoutPurpose, string>());

    /// <summary>
    /// Preselect what the presentation does with the slides of the preselected template, by the number of each slide?
    /// </summary>
    /// <remarks>
    /// The slides count from 1, in the order of the file, hidden ones included. A slide which is
    /// missing is left out, as by default. The choices belong to the preselected template: for any
    /// other template, the assistant ignores them, since its slides are other ones.
    /// </remarks>
    public Dictionary<int, TemplateSlideUse> PreselectedTemplateSlideUses { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedTemplateSlideUses, new Dictionary<int, TemplateSlideUse>());

    /// <summary>
    /// Preselect any Slide Builder  options?
    /// </summary>
    public bool PreselectOptions { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectOptions, true);
    
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
    /// Preselect the occasion of the presentation?
    /// </summary>
    public PresentationOccasion PreselectedOccasion { get; set; }

    /// <summary>
    /// Preselect any other occasion?
    /// </summary>
    public string PreselectedOtherOccasion { get; set; } = string.Empty;

    /// <summary>
    /// Preselect any important aspects that the Slide Builder should take into account?
    /// </summary>
    public string PreselectedImportantAspects { get; set; } = string.Empty;
    
    /// <summary>
    /// The minimum confidence level required for a provider to be considered.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;
}