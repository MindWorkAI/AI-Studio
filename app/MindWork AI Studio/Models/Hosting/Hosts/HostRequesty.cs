using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Requesty, a gateway which serves other people's models and says whose they are.
/// </summary>
/// <remarks>
/// Most names come as "vendor/model", the same shape OpenRouter uses, so the prefix is taken off
/// and the vendor stated. Requesty also lists managed routing policies such as "gpt-5-mini@eu",
/// which carry no prefix at all. Those have nothing to take off and go to the rules as they are.
/// </remarks>
public sealed class HostRequesty : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.REQUESTY;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.requesty.ai/features/managed-policies", new DateOnly(2026, 9, 25), "Models are named \"vendor/model\", apart from managed policies such as \"gpt-5-mini@eu\", and all of them are served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
}