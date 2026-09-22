using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Somebody's own engine: Ollama, LM Studio, vLLM, llama.cpp, or a proxy in front of them.
/// </summary>
/// <remarks>
/// vLLM serves whatever it was pointed at, and what it was pointed at is usually a hub repository:
/// "meta-llama/Llama-3.3-70B-Instruct", "01-ai/yi-large". So the organization comes off here too.
///
/// The colon does not. Ollama writes the variant after it -- "qwen3.8:27b-mlx" -- and taking that
/// off would leave a name which no longer says which build of the model is running. Only the host
/// which actually has a router treats a colon as routing.
///
/// Whatever the engine can do beyond this, only the engine knows: how large a context window the
/// operator configured, how many images it accepts. Those come from the model list of the running
/// installation, not from a rule written here.
/// </remarks>
public sealed class HostSelfHosted : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.SELF_HOSTED;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.vllm.ai/en/latest/serving/openai_compatible_server.html", new DateOnly(2026, 9, 11), "Models are named as the operator loaded them, often as a hub repository, and served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
}