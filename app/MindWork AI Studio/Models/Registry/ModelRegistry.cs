using System.Collections.Concurrent;
using System.Collections.Frozen;

using AIStudio.Models.Hosting;
using AIStudio.Models.Matching;
using AIStudio.Models.Plugins;
using AIStudio.Provider;

namespace AIStudio.Models.Registry;

/// <summary>
/// Everything the app knows about models, as one question with one answer.
/// </summary>
/// <remarks>
/// A few things happen to a name here, and the order they happen in is the whole design. The host
/// takes off whatever wrapping the provider put around the name, so that a rule can be written once
/// instead of once per provider. What an organization declared about its own models answers first,
/// where it says anything. Otherwise the built-in rules answer the bare name, and the most specific
/// of them wins, computed rather than written down. The family which won may then work something
/// out of the name that no rule can express. And the host says what the way there took away.
///
/// Nothing in here reaches for application state, so a test can build a registry and ask it
/// questions without the app ever having started.
/// </remarks>
public sealed class ModelRegistry
{
    /// <summary>
    /// The registry over everything this assembly declares.
    /// </summary>
    /// <remarks>
    /// Built once, on first use. The families and hosts it is built from were collected while
    /// compiling, so nothing is searched for at startup.
    /// </remarks>
    private static readonly Lazy<ModelRegistry> THE_ONE = new(() => Build(ModelRegistrations.CreateFamilies(), ModelRegistrations.CreateHosts()));

    private readonly FrozenDictionary<string, ModelFamily> familiesByName;

    /// <summary>
    /// What the plugins declare, and the answers worked out while they declared it.
    /// </summary>
    /// <remarks>
    /// The two belong together and are therefore replaced together. A cache which outlived the
    /// declarations it was filled under would keep handing out what the rules said before a
    /// plugin arrived, and a reader holding one half of a swap would mix the two.
    /// </remarks>
    private volatile Answers answers = new(null);

    private ModelRegistry(IReadOnlyList<ModelFamily> families, ModelFamilyIndex rules, ModelHostIndex hosts, FrozenDictionary<string, ModelFamily> familiesByName)
    {
        this.familiesByName = familiesByName;
        this.Families = families;
        this.Rules = rules;
        this.Hosts = hosts;
    }

    /// <summary>
    /// The registry the app uses.
    /// </summary>
    public static ModelRegistry Shared => THE_ONE.Value;

    /// <summary>
    /// Every family, in the order the generated registration lists them.
    /// </summary>
    public IReadOnlyList<ModelFamily> Families { get; }

    /// <summary>
    /// Every rule of every family, indexed by the name parts they mention.
    /// </summary>
    public ModelFamilyIndex Rules { get; }

    /// <summary>
    /// Which host answers for which provider.
    /// </summary>
    public ModelHostIndex Hosts { get; }

    /// <summary>
    /// The rules the running model plugins declare, in the order the index holds them.
    /// </summary>
    public IReadOnlyList<ModelRule> Declared => this.answers.Declared?.Rules ?? [];

    /// <summary>
    /// Takes over what the model plugins declare, replacing whatever they declared before.
    /// </summary>
    /// <remarks>
    /// Replacing rather than adding, because this is called again whenever the plugins are
    /// reloaded: a plugin somebody removed has to stop being heard, and a declaration somebody
    /// corrected must not go on answering alongside its correction.
    ///
    /// The declarations are pushed in rather than fetched. Nothing in here knows that plugins
    /// exist, which is what keeps a registry buildable in a test without the plugin system, the
    /// settings, or the app having started.
    /// </remarks>
    /// <param name="declarations">What the plugins declare, with each pattern claimed by one of them.</param>
    public void Declare(IReadOnlyList<ModelDeclaration> declarations)
    {
        this.answers = new(declarations.Count is 0 ? null : ModelFamilyIndex.Build(declarations.Select(declaration => declaration.ToRule())));
    }

    /// <summary>
    /// Builds a registry over a set of families and hosts.
    /// </summary>
    /// <param name="families">The families, in any order.</param>
    /// <param name="hosts">The hosts, in any order.</param>
    /// <returns>The registry.</returns>
    /// <exception cref="InvalidOperationException">When two families share a name.</exception>
    public static ModelRegistry Build(IEnumerable<ModelFamily> families, IEnumerable<IModelHost> hosts)
    {
        var stated = families.ToArray();
        var byName = new Dictionary<string, ModelFamily>(StringComparer.Ordinal);
        foreach (var family in stated)
        {
            //
            // A family is found again by the name its rules were written under. Two families
            // sharing one -- which two namespaces make possible -- would send the refinement of one
            // to the other, and nothing else would ever say so.
            //
            if (byName.TryGetValue(family.Name, out var alreadyThere))
                throw new InvalidOperationException($"Both {alreadyThere.GetType().FullName} and {family.GetType().FullName} are called {family.Name}. A family is found again by that name, so two of them cannot share it.");

            byName[family.Name] = family;
        }

        var rules = ModelFamilyIndex.Build(stated.SelectMany(family => family.Rules));
        return new(stated, rules, ModelHostIndex.Build(hosts), byName.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>
    /// Says what is known about a model at a provider.
    /// </summary>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID exactly as that provider reports it.</param>
    /// <returns>The profile, which knows nothing when no rule knows the name.</returns>
    public ModelProfile Profile(LLMProviders provider, string modelId)
    {
        if (NothingCanBeSaid(provider, modelId))
            return ModelProfile.UNKNOWN;

        //
        // Read once, then used throughout: the plugins may be reloaded while this is running, and
        // an answer worked out from one set of declarations belongs in the cache of that same set.
        //
        var current = this.answers;
        return current.Cached.GetOrAdd((provider, modelId), static (key, state) => state.Registry.Explain(key.Provider, key.ModelId, state.Answers).Profile, (Registry: this, Answers: current));
    }

    /// <summary>
    /// Says what is known about a model, and how the answer came about.
    /// </summary>
    /// <remarks>
    /// The same answer as the profile, with the rules that produced it. This is what the
    /// verification run reads, and what a test asks when it wants to know why a model came out the
    /// way it did. It is not cached: it allocates, and nobody asks it in a render loop.
    /// </remarks>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID exactly as that provider reports it.</param>
    /// <returns>The resolution, including the profile as the provider serves it.</returns>
    public ModelResolution Explain(LLMProviders provider, string modelId) => this.Explain(provider, modelId, this.answers);

    /// <summary>
    /// Says what is known about a model, against one particular set of plugin declarations.
    /// </summary>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID exactly as that provider reports it.</param>
    /// <param name="current">The declarations to answer against, and the cache belonging to them.</param>
    /// <returns>The resolution, including the profile as the provider serves it.</returns>
    private ModelResolution Explain(LLMProviders provider, string modelId, Answers current)
    {
        if (NothingCanBeSaid(provider, modelId))
            return ModelResolution.NOTHING;

        var id = new ModelId(modelId);
        var bare = this.Hosts.Unwrap(id, provider, out var declaredVendor);
        var vendor = declaredVendor ?? ModelVendor.UNKNOWN;

        //
        // What an organization declared about a model comes before what the built-in rules work out
        // of its name, and it comes instead of it rather than on top of it: a declaration is the
        // whole statement about the models it matches. Letting the built-in rules add to it would
        // mean a modifier nobody was thinking about could overrule what an organization stated --
        // "guard" would still turn their own chat model into a moderation model.
        //
        // What stays is the transport, because that is not a statement about the model at all: a
        // gateway which cannot pass an API through does not pass it through, whoever describes the
        // model behind it.
        //
        if (current.Declared?.Explain(bare, provider, vendor) is { IsKnown: true } declared)
            return declared with { Profile = this.Hosts.ApplyTransport(declared.Profile, provider) };

        var resolution = this.Rules.Explain(bare, provider, vendor);

        //
        // Only the family which chose the model refines it. A modifier adjusts an answer; it does
        // not know which model it is adjusting, so it has nothing to work out of the name.
        //
        var refined = this.FamilyOf(resolution.Selector)?.Refine(bare, resolution.Profile) ?? resolution.Profile;
        return resolution with { Profile = this.Hosts.ApplyTransport(refined, provider) };
    }

    /// <summary>
    /// Whether there is a question here at all.
    /// </summary>
    /// <remarks>
    /// Without a provider there is nothing to reach the model through, so nothing can be said about
    /// how it could be used -- which is also what the rules it replaces answered. An empty ID is
    /// what a provider reports before anybody picked a model.
    /// </remarks>
    /// <param name="provider">Who serves the model.</param>
    /// <param name="modelId">The model ID.</param>
    /// <returns>True, when there is nothing to answer.</returns>
    private static bool NothingCanBeSaid(LLMProviders provider, string modelId) => provider is LLMProviders.NONE || string.IsNullOrWhiteSpace(modelId);

    /// <summary>
    /// The family a rule was written in.
    /// </summary>
    /// <param name="selector">The rule which chose the model.</param>
    /// <returns>The family, or nothing when no rule chose.</returns>
    private ModelFamily? FamilyOf(ModelRule? selector) => selector is null ? null : this.familiesByName.GetValueOrDefault(selector.Origin);

    /// <summary>
    /// What the registry answers with, and what it has answered so far.
    /// </summary>
    /// <remarks>
    /// The cache is the reason the whole rebuild is worth doing at all. The question is asked from
    /// components which re-render on every streamed chunk, and the expert dialog asks it about a
    /// dozen times per render. A profile cannot be changed after it was built, so handing the same
    /// one to every caller is safe -- unlike the old code, which handed out a list and had one
    /// caller quietly change it.
    ///
    /// It sits next to the declarations rather than beside them, so that replacing what the plugins
    /// say throws away exactly the answers which were given while they said something else.
    /// </remarks>
    /// <param name="declared">What the running model plugins declare, or null when they declare nothing.</param>
    private sealed class Answers(ModelFamilyIndex? declared)
    {
        /// <summary>
        /// What the running model plugins declare.
        /// </summary>
        public ModelFamilyIndex? Declared { get; } = declared;

        /// <summary>
        /// The answers already worked out, so that a name is measured against the rules once.
        /// </summary>
        public ConcurrentDictionary<(LLMProviders Provider, string ModelId), ModelProfile> Cached { get; } = new();
    }
}