using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting;

/// <summary>
/// One place a model can be reached from, and what reaching it that way does to the answer.
/// </summary>
/// <remarks>
/// This is the routing graph, written down instead of grown into the rules. The old code solved
/// gateways and resellers by having one vendor's rules call another's, which turned into mutual
/// recursion -- Mistral into the open weights, the open weights back into Anthropic, Google, and
/// OpenAI -- and nobody could say from reading it which way a name would travel.
///
/// A host does two things, and only these two. It unwraps a name until the model underneath is
/// visible, and it says what the transport takes away. Unwrapping is iterative on purpose, because
/// the wrappings stack: Hugging Face first drops the routing suffix, then the organization prefix.
/// A host which serves other people's models under their plain names unwraps nothing and only
/// trims the transport, which is the same mechanism rather than a special case.
/// </remarks>
public interface IModelHost
{
    /// <summary>
    /// The provider this host answers for.
    /// </summary>
    LLMProviders Provider { get; }

    /// <summary>
    /// Where the statements about this host were read, and when.
    /// </summary>
    ModelSource Source { get; }

    /// <summary>
    /// Takes one wrapping off a name, if there is one.
    /// </summary>
    /// <remarks>
    /// Called again with whatever comes out, until it says no. A host which declares who built the
    /// model saves the rules from having to guess it from the name.
    /// </remarks>
    /// <param name="id">The name as it arrived.</param>
    /// <param name="inner">The name with one wrapping removed.</param>
    /// <param name="declaredVendor">Who the wrapping says built the model, when it says so.</param>
    /// <returns>True, when a wrapping was removed.</returns>
    bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor);

    /// <summary>
    /// Takes away what this host cannot offer, whatever the model itself can do.
    /// </summary>
    /// <remarks>
    /// A provider reselling somebody else's model speaks its own dialect, not the vendor's: the
    /// model may well be able to answer through a vendor specific API, but not here.
    /// </remarks>
    /// <param name="profile">What the model can do.</param>
    /// <returns>What it can do through this host.</returns>
    ModelProfile ApplyTransport(in ModelProfile profile);
}