using System.Linq.Expressions;

using AIStudio.Assistants.BeyondFactsLab;
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
    /// Gets or sets how the lab addresses the person in languages with a formal and an informal address.
    /// </summary>
    public BflAddressForm AddressForm { get; set; } = ManagedConfiguration.Register(configSelection, value => value.AddressForm, BflAddressForm.FORMAL);

    /// <summary>
    /// Gets or sets the hash of the notes the person confirmed, see BflNotice; empty while they confirmed none.
    /// </summary>
    /// <remarks>
    /// Only the person can confirm the notes, so an organization cannot manage this setting.
    /// </remarks>
    public string AcceptedNoticeHash { get; set; } = string.Empty;
}