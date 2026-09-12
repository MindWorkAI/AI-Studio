using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// The IONOS AI Model Hub, which keeps the hub spelling of the models it serves.
/// </summary>
/// <remarks>
/// Its catalog reads like the hub's: "meta-llama/Llama-3.3-70B-Instruct",
/// "mistralai/Mistral-Small-24B-Instruct". So the organization comes off, and with it comes the
/// vendor -- stated rather than guessed from the name.
/// </remarks>
public sealed class HostIONOS : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.IONOS;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.ionos.com/cloud/ai/ai-model-hub", new DateOnly(2026, 9, 11), "Open weights named as the hub names them, served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
}