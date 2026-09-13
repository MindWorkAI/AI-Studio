using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace AIStudio.Models.Live;

/// <summary>
/// What the configured providers last said about the models they serve.
/// </summary>
/// <remarks>
/// One snapshot per configured provider instance, and reporting replaces the snapshot rather than
/// adding to it. That is the same reason the registry replaces what the plugins declare: a model an
/// installation no longer serves has to stop answering, and a window somebody halved by restarting
/// their engine must not go on being reported alongside its correction.
///
/// Nothing here is written to disk. These are statements about a machine as it is running right
/// now, and the app asks that machine again before every chat round anyway. An instance somebody
/// deleted keeps its snapshot until the app is closed -- a few dozen kilobytes at the very worst,
/// which is not worth a second mechanism to watch the settings for.
/// </remarks>
public sealed class ListedModels
{
    /// <summary>
    /// The one the app reports into and asks.
    /// </summary>
    public static ListedModels Shared { get; } = new();

    /// <summary>
    /// Per configured provider instance, what its model list said about each model.
    /// </summary>
    /// <remarks>
    /// Both keys ignore case. The IDs come back from the same list they were stored under, so
    /// ordinal would do -- but a model an organization wrote into a configuration plugin by hand
    /// was typed by a person, and the availability check already treats such a name as the same
    /// model regardless of case. Being stricter here would leave exactly those people without the
    /// numbers.
    /// </remarks>
    private readonly ConcurrentDictionary<string, FrozenDictionary<string, ModelListing>> byProvider = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Takes over what one provider instance said about its models, replacing what it said before.
    /// </summary>
    /// <remarks>
    /// Only ever call this with a whole list in hand. Reporting a filtered part of one would tell
    /// this instance that everything left out has stopped existing.
    /// </remarks>
    /// <param name="configuredProviderId">The instance that was asked. Nothing happens without one.</param>
    /// <param name="listings">What its list stated, with the models it stated nothing about left in or out as convenient.</param>
    public void Report(string configuredProviderId, IEnumerable<ModelListing> listings)
    {
        //
        // A provider instance nobody has configured yet is not a machine we could ask again later,
        // so there is nothing to remember it by. The provider dialog is not such a case: it works
        // on a fully built instance from the moment it opens, ID included.
        //
        if (string.IsNullOrWhiteSpace(configuredProviderId))
            return;

        var stated = new Dictionary<string, ModelListing>(StringComparer.OrdinalIgnoreCase);
        foreach (var listing in listings)
        {
            if (string.IsNullOrWhiteSpace(listing.ModelId) || !listing.IsKnown)
                continue;

            stated[listing.ModelId] = listing;
        }

        this.byProvider[configuredProviderId] = stated.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What one provider instance said about one of its models.
    /// </summary>
    /// <param name="configuredProviderId">The instance serving the model.</param>
    /// <param name="modelId">The model, named the way that instance names it.</param>
    /// <returns>What it stated, which is nothing when it was never asked or said nothing.</returns>
    public ModelListing Of(string configuredProviderId, string modelId)
    {
        if (string.IsNullOrWhiteSpace(configuredProviderId) || string.IsNullOrWhiteSpace(modelId))
            return ModelListing.NOTHING;

        if (!this.byProvider.TryGetValue(configuredProviderId, out var stated))
            return ModelListing.NOTHING;

        return stated.TryGetValue(modelId, out var listing) ? listing : ModelListing.NOTHING;
    }
}