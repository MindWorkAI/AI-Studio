using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Requesty, a gateway which serves other people's models and says who runs them.
/// </summary>
/// <remarks>
/// A name comes as "provider/model", and the provider is whoever runs the model: the vendor's own
/// API, as in "openai/gpt-6-sol", or a cloud serving somebody else's model, as in
/// "bedrock/claude-opus-5" or "vertex/gemini-3.5-flash". A cloud which serves weights from the hub
/// keeps the hub's organization as a second segment, as in "novita/qwen/qwen3.8-max". The walk
/// takes off one segment at a time, and the innermost one which names a vendor is the one that
/// counts.
///
/// Where the provider is also a vendor, its name states that vendor. That is right whenever a
/// vendor serves its own models, which is nearly always. It is wrong where the cloud of a vendor
/// resells other models, as in "tencent/kimi-k3": on the day we checked, about one in twenty of the
/// stated vendors was wrong this way. The price is small, because no built-in rule insists on a
/// vendor -- only a model plugin which does would miss such a name and leave it to the built-in
/// rules.
///
/// Behind the model, a name may carry one more thing which does not change the model: the region it
/// runs in after an at sign, as in "@eu-central-1" or "@eu", or the service tier or a preset
/// reasoning effort after a colon, as in ":flex" or ":high". These come off first. Managed
/// policies, which Requesty curates and routes across several providers, carry no prefix at all,
/// as in "claude-opus-5@eu", so they only lose their region.
/// </remarks>
public sealed class HostRequesty : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.REQUESTY;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.requesty.ai/features/eu-routing", new DateOnly(2026, 10, 12), "Models are named \"provider/model\", where the provider is whoever runs the model, optionally followed by a region after an at sign; the model list adds a service tier or a reasoning effort after a colon, and managed policies without any prefix. All of them are served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor)
    {
        declaredVendor = null;
        if (HostNaming.TryStripRegionSuffix(id, out inner))
            return true;

        if (HostNaming.TryStripRoutingSuffix(id, out inner))
            return true;

        return HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
    }
}