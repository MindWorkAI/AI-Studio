using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// The Hugging Face router, whose names carry two wrappings rather than one.
/// </summary>
/// <remarks>
/// "google/gemma-4-31B-it:novita" says three things at once: who published the weights, which model
/// it is, and which inference provider should answer. The suffix goes first, because it is the
/// outermost and because it says nothing about the model -- a request routed to Novita and one
/// routed to Together AI reach the same weights.
///
/// This is the case the whole walk was written for. A host which took both off at once would work
/// here and nowhere else; taking one off at a time is what also covers the account path Fireworks
/// puts in front, without either host knowing about the other.
/// </remarks>
public sealed class HostHuggingFace : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.HUGGINGFACE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/docs/inference-providers/index", new DateOnly(2026, 9, 11), "Models are named as the hub names them, \"organization/model\", optionally followed by a colon and the inference provider to route to.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
    {
        declaredVendor = null;
        if (HostNaming.TryStripRoutingSuffix(id, out inner))
            return true;

        return HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
    }
}