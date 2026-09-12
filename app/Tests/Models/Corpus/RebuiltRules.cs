using AIStudio.Models;
using AIStudio.Models.Registry;
using AIStudio.Provider;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// Asks the rebuilt rules about a corpus entry, in the words the old ones answered in.
/// </summary>
/// <remarks>
/// The two systems say the same things in different shapes: the old one hands out a list of
/// capabilities, the new one a profile whose reasoning is a field of its own rather than one of
/// three flags. Comparing them at all needs one of the two translated, and translating the new one
/// into the old vocabulary is the direction which loses nothing -- the profile knows more, and
/// everything the old answer could say has a place in it.
/// </remarks>
public static class RebuiltRules
{
    /// <summary>
    /// Asks the rebuilt rules about one corpus entry.
    /// </summary>
    /// <param name="entry">The entry to ask about.</param>
    /// <returns>The capabilities, in the vocabulary the old rules answered in.</returns>
    public static IReadOnlyList<Capability> Ask(CorpusEntry entry) => AsCapabilities(ModelRegistry.Shared.Profile(entry.Provider, entry.ModelId));

    /// <summary>
    /// Writes a profile as the list of capabilities the old rules would have answered with.
    /// </summary>
    /// <remarks>
    /// The reasoning field turns back into the flag which stands for it. That mapping is the whole
    /// reason the flags stay in the vocabulary: a person writing an override still says
    /// ALWAYS_REASONING, and the expert dialog still shows those five choices.
    /// </remarks>
    /// <param name="profile">The profile to write out.</param>
    /// <returns>The capabilities.</returns>
    public static IReadOnlyList<Capability> AsCapabilities(in ModelProfile profile)
    {
        // A profile handed in by reference cannot be reached from inside a query, and copying one
        // costs nothing:
        var answered = profile;
        var stated = Enum.GetValues<Capability>()
            .Where(capability => capability is not Capability.NONE && answered.Has(capability))
            .ToList();

        var reasoning = ReasoningAsCapability(profile.Reasoning);
        if (reasoning is not Capability.NONE)
            stated.Add(reasoning);

        return stated;
    }

    private static Capability ReasoningAsCapability(ReasoningSupport reasoning) => reasoning switch
    {
        ReasoningSupport.OPTIONAL => Capability.OPTIONAL_REASONING,
        ReasoningSupport.ON_BY_DEFAULT => Capability.REASONING_BY_DEFAULT,
        ReasoningSupport.ALWAYS => Capability.ALWAYS_REASONING,

        _ => Capability.NONE,
    };
}