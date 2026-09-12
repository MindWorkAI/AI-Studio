using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// Groq, which serves open weights and writes some of their names the way the hub does.
/// </summary>
/// <remarks>
/// Both spellings appear side by side in its catalog: "llama-3.3-70b-versatile" carries no
/// organization, "moonshotai/kimi-k2-instruct" and "openai/gpt-oss-120b" do. Taking one off when
/// there is one settles both without a rule per spelling.
/// </remarks>
public sealed class HostGroq : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.GROQ;

    /// <inheritdoc />
    public override ModelSource Source => new("https://console.groq.com/docs/api-reference", new DateOnly(2026, 9, 11), "Models are named either plainly or as the hub names them, and served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
}