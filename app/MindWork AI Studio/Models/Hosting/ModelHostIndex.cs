using System.Collections.Frozen;

using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting;

/// <summary>
/// Which host answers for which provider, and the unwrapping walk itself.
/// </summary>
/// <remarks>
/// The walk is why this exists rather than a plain dictionary. Wrappings stack, and how deep they
/// go is the host's business, not the caller's: Hugging Face takes off a routing suffix and then an
/// organization, Fireworks takes off three path segments, and most hosts take off nothing at all.
/// Asking a host over and over until it says no covers all three without anybody counting.
/// </remarks>
public sealed class ModelHostIndex
{
    /// <summary>
    /// How often a name may be unwrapped before we stop believing the host.
    /// </summary>
    /// <remarks>
    /// The deepest wrapping we know of is the account path Fireworks puts in front, at three
    /// segments. The limit is not there for that -- it is there so that a host which hands back a
    /// name it never shortened cannot hang the app. A host which needs more than this has gone
    /// wrong, and stopping is a better answer than never returning.
    /// </remarks>
    public const int MAX_UNWRAPPING_STEPS = 8;

    private readonly FrozenDictionary<LLMProviders, IModelHost> byProvider;

    private ModelHostIndex(FrozenDictionary<LLMProviders, IModelHost> byProvider, IReadOnlyList<IModelHost> hosts, IReadOnlyList<LLMProviders> providersWithoutAHost)
    {
        this.byProvider = byProvider;
        this.Hosts = hosts;
        this.ProvidersWithoutAHost = providersWithoutAHost;
    }

    /// <summary>
    /// Every host the index was built from, ordered by provider.
    /// </summary>
    public IReadOnlyList<IModelHost> Hosts { get; }

    /// <summary>
    /// The providers a person can configure for which nobody wrote a host.
    /// </summary>
    /// <remarks>
    /// Not an error at runtime, and that is on purpose: a provider added to the app without a host
    /// still works, its names are simply taken as they are. It is an error the verification run
    /// reports, which is where a missing host should surface -- before the release, not during a
    /// chat.
    /// </remarks>
    public IReadOnlyList<LLMProviders> ProvidersWithoutAHost { get; }

    /// <summary>
    /// Builds an index over a set of hosts.
    /// </summary>
    /// <param name="hosts">The hosts, in any order.</param>
    /// <returns>The index.</returns>
    /// <exception cref="InvalidOperationException">When two hosts answer for the same provider, or a host answers for none.</exception>
    public static ModelHostIndex Build(IEnumerable<IModelHost> hosts)
    {
        var byProvider = new Dictionary<LLMProviders, IModelHost>();
        foreach (var host in hosts)
        {
            if (host.Provider is LLMProviders.NONE)
                throw new InvalidOperationException($"The host {host.GetType().Name} answers for no provider. A host has to name the provider it serves, because that is how anything finds it.");

            if (byProvider.TryGetValue(host.Provider, out var alreadyThere))
                throw new InvalidOperationException($"Both {alreadyThere.GetType().Name} and {host.GetType().Name} answer for {host.Provider}. Only one host can, because there is one way a name arrives from a provider.");

            byProvider[host.Provider] = host;
        }

        var withoutAHost = Enum.GetValues<LLMProviders>()
            .Where(provider => provider is not LLMProviders.NONE && !byProvider.ContainsKey(provider))
            .ToArray();

        var ordered = byProvider.OrderBy(entry => entry.Key).Select(entry => entry.Value).ToArray();
        return new(byProvider.ToFrozenDictionary(), ordered, withoutAHost);
    }

    /// <summary>
    /// The host answering for a provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The host, or nothing when nobody wrote one.</returns>
    public IModelHost? Of(LLMProviders provider) => this.byProvider.GetValueOrDefault(provider);

    /// <summary>
    /// Takes a name apart until the model underneath is visible.
    /// </summary>
    /// <remarks>
    /// The innermost statement about the vendor is the one that counts. A wrapping closer to the
    /// model knows more about it than one further out, and a wrapping which says nothing does not
    /// erase what an outer one said.
    /// </remarks>
    /// <param name="id">The name as the provider reported it.</param>
    /// <param name="provider">Who reported it.</param>
    /// <param name="declaredVendor">Who the wrappings say built the model, when they say so.</param>
    /// <returns>The name with every wrapping taken off.</returns>
    public ModelId Unwrap(in ModelId id, LLMProviders provider, out ModelVendor? declaredVendor)
    {
        declaredVendor = null;

        var host = this.Of(provider);
        if (host is null)
            return id;

        var current = id;
        for (var step = 0; step < MAX_UNWRAPPING_STEPS; step++)
        {
            if (!host.TryUnwrap(current, out var inner, out var stated))
                break;

            // A host handing back what it was given would go round forever:
            if (inner.Equals(current))
                break;

            current = inner;
            if (stated is not null)
                declaredVendor = stated;
        }

        return current;
    }

    /// <summary>
    /// Takes away what a provider cannot offer, whatever the model itself can do.
    /// </summary>
    /// <remarks>
    /// A provider without a host gets the answer every host but one gives: the ordinary chat
    /// completion API. That is the safe direction -- claiming an API which is not there turns into
    /// a failed request, while not claiming one merely means the app does not use it.
    /// </remarks>
    /// <param name="profile">What the model can do.</param>
    /// <param name="provider">Who serves it.</param>
    /// <returns>What it can do through this provider.</returns>
    public ModelProfile ApplyTransport(in ModelProfile profile, LLMProviders provider)
    {
        var host = this.Of(provider);
        return host?.ApplyTransport(profile) ?? ModelHost.ThroughTheOrdinaryApi(profile);
    }
}