using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// OpenRouter, which serves other people's models and says whose they are.
/// </summary>
/// <remarks>
/// The vendor prefix is the reason the old rules delegated between vendors in circles: a name such
/// as "anthropic/claude-opus-5" had to be handed to whoever knew Claude, and the same for every
/// other vendor. Here the prefix is simply taken off, and the vendor stated, and one set of rules
/// answers the bare name -- no matter which provider it arrived from.
/// </remarks>
public sealed class HostOpenRouter : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.OPEN_ROUTER;

    /// <inheritdoc />
    public override ModelSource Source => new("https://openrouter.ai/docs/api-reference/overview", new DateOnly(2026, 9, 11), "Models are named \"vendor/model\", and every one of them is served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
}