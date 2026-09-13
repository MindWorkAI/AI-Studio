using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Fireworks AI, which puts a whole account path in front of every model.
/// </summary>
/// <remarks>
/// "accounts/fireworks/models/llama-v3p1-405b-instruct" is three segments of path and then the
/// model. Nothing here counts them: the same taking-off-one-segment the gateways use is asked
/// again until there is no path left. None of the three segments names a vendor we know, so none
/// of them claims to.
/// </remarks>
public sealed class HostFireworks : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.FIREWORKS;

    /// <inheritdoc />
    public override ModelSource Source => new("https://fireworks.ai/models?show=Serverless", new DateOnly(2026, 9, 11), "Models are named \"accounts/<account>/models/<model>\", served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
}