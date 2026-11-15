using System.Linq.Expressions;

using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// Stores the settings of the BeyondFacts Lab, which an organization may manage.
/// </summary>
/// <param name="configSelection">The managed-configuration selector.</param>
public sealed class DataBeyondFactsLab(Expression<Func<Data, DataBeyondFactsLab>>? configSelection = null)
{
    /// <summary>
    /// The lowest minimum confidence the BeyondFacts Lab accepts, whatever the person or their organization configures.
    /// </summary>
    /// <remarks>
    /// Unpublished research is the usual material of the lab. The built-in confidence schemes give this level
    /// to the cloud providers of the region one trusts and to self-hosted models; HIGH alone would leave only
    /// self-hosted models. A higher minimum of the person or the organization wins.
    /// </remarks>
    public const ConfidenceLevel MINIMUM_CONFIDENCE_FLOOR = ConfidenceLevel.MEDIUM;

    /// <summary>
    /// The most characters the notes of an organization may have, see OrganizationRules.
    /// </summary>
    /// <remarks>
    /// The chats of the steps receive the notes with every request, so they take room in the context of the model
    /// each time. A configuration plugin with longer notes leaves the lab without any, and the log says why.
    /// </remarks>
    public const int ORGANIZATION_RULES_MAX_CHARACTERS = 2_000;

    /// <summary>
    /// Initializes unmanaged settings of the BeyondFacts Lab.
    /// </summary>
    public DataBeyondFactsLab() : this(null)
    {
    }

    /// <summary>
    /// Gets or sets the provider which new preparations start with.
    /// </summary>
    public string PreselectedProvider { get; set; } = ManagedConfiguration.Register(configSelection, value => value.PreselectedProvider, string.Empty);

    /// <summary>
    /// Gets or sets the minimum confidence a provider needs for the lab.
    /// </summary>
    /// <remarks>
    /// A value below MINIMUM_CONFIDENCE_FLOOR has no effect, see ComponentsExtensions.MinimumConfidence.
    /// </remarks>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ManagedConfiguration.Register(configSelection, value => value.MinimumProviderConfidence, MINIMUM_CONFIDENCE_FLOOR);

    /// <summary>
    /// Gets or sets whether files in the folder for prompt overrides replace the prompts which come with the lab.
    /// </summary>
    /// <remarks>
    /// The option serves the further development of the prompts, e.g., by the research team behind the lab, not the
    /// preparation of an appearance. An organization may switch it off and lock it.
    /// </remarks>
    public bool AllowPromptOverrides { get; set; } = ManagedConfiguration.Register(configSelection, value => value.AllowPromptOverrides, false);

    /// <summary>
    /// Gets or sets the notes of the organization for public appearances, e.g., to contact the press office before
    /// every public event; empty without any. Markdown, with LF line breaks and without spaces around it.
    /// </summary>
    /// <remarks>
    /// Only a configuration plugin sets them; the options of the lab offer no field. The lab shows them word for
    /// word, and the chats of the steps receive them as information along with the first message, never in the
    /// system prompt, so that the rules of the lab prevail where the notes contradict them.
    /// </remarks>
    public string OrganizationRules { get; set; } = ManagedConfiguration.Register(configSelection, value => value.OrganizationRules, string.Empty);

    /// <summary>
    /// Gets or sets the hash of the notes the person confirmed, see BflNotice; empty while they confirmed none.
    /// </summary>
    /// <remarks>
    /// Only the person can confirm the notes, so an organization cannot manage this setting.
    /// </remarks>
    public string AcceptedNoticeHash { get; set; } = string.Empty;
}