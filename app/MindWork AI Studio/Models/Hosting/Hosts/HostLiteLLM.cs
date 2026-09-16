using AIStudio.Models.Matching;
using AIStudio.Provider;

namespace AIStudio.Models.Hosting.Hosts;

/// <summary>
/// A LiteLLM proxy, which somebody operates themselves and names as they please.
/// </summary>
/// <remarks>
/// Aliases here are whatever the operator wrote in their configuration. Many of them keep the
/// "vendor/model" shape, some name the cloud instead of the vendor ("azure/gpt-5.6"), and some are
/// a word ("the-fast-one"). Taking off a prefix costs nothing in the last case and helps in the
/// first two, and a prefix nobody recognizes states no vendor -- so a name the operator invented
/// is left for the rules to make what they can of.
///
/// This is also the host where a person is most likely to correct us by hand, which is what the
/// expert settings are for: an alias only its operator can decipher is not something rules will
/// ever get right.
/// </remarks>
public sealed class HostLiteLLM : ModelHost
{
    /// <inheritdoc />
    public override LLMProviders Provider => LLMProviders.LITE_LLM;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.litellm.ai/docs/proxy/user_keys", new DateOnly(2026, 9, 11), "Models are whatever the operator named them, served through the OpenAI-compatible chat completion API.");

    /// <inheritdoc />
    public override bool TryUnwrap(in ModelId id, out ModelId inner, out ModelVendor? declaredVendor) => HostNaming.TrySplitOrganization(id, out inner, out declaredVendor);
}