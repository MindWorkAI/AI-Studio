using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting;

/// <summary>
/// The ordinary host: it serves models under the names they are known by, through the ordinary API.
/// </summary>
/// <remarks>
/// Most hosts differ from each other in one sentence, and this is what carries the rest. A host
/// which wraps its names says how to unwrap one; a host which speaks an API the others do not says
/// so; everything else is stated here once.
///
/// What a source means for a host: the page names where the behaviour is documented, so that a
/// person can re-check it in a minute. The statements themselves were read off the app's own
/// provider implementations and the model corpus, both of which are in this repository -- the
/// pages are where somebody looks when they doubt them.
/// </remarks>
public abstract class ModelHost : IModelHost
{
    /// <summary>
    /// The two capabilities which say through which API a model is reached.
    /// </summary>
    private const Capability THE_APIS = Capability.CHAT_COMPLETION_API | Capability.RESPONSES_API;

    /// <inheritdoc />
    public abstract LLMProviders Provider { get; }

    /// <inheritdoc />
    public abstract ModelSource Source { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Nothing is wrapped here: this host serves models under the names they are known by.
    /// </remarks>
    public virtual bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
    {
        inner = id;
        declaredVendor = null;
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The Responses API is OpenAI's own, and the app speaks it in exactly one place, its OpenAI
    /// provider. Wherever else a model is reached, it is reached through the ordinary chat
    /// completion API -- whatever the model itself could do at its vendor.
    /// </remarks>
    public virtual ModelProfile ApplyTransport(in ModelProfile profile) => ThroughTheOrdinaryApi(profile);

    /// <summary>
    /// Puts a profile on the ordinary chat completion API.
    /// </summary>
    /// <remarks>
    /// A profile which says nothing about APIs is left alone. An embedding model is reached through
    /// neither of the two, and answering that it speaks the chat completion API would be a claim
    /// nobody made.
    /// </remarks>
    /// <param name="profile">What the model can do.</param>
    /// <returns>What it can do when reached through the ordinary API.</returns>
    public static ModelProfile ThroughTheOrdinaryApi(in ModelProfile profile)
    {
        if (!profile.HasAny(THE_APIS))
            return profile;

        return profile with { Capabilities = (profile.Capabilities & ~Capability.RESPONSES_API) | Capability.CHAT_COMPLETION_API };
    }
}