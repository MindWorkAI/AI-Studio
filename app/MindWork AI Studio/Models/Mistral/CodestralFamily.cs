using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Codestral, the Mistral models for writing code.
/// </summary>
/// <remarks>
/// The previous rules never named it. Its name contains neither "mistral" nor any of the other
/// words the Mistral block looked for, so it walked past every rule and reached the answer meant
/// for everything nobody had written one for. That answer happened to describe it correctly, which
/// is why nothing looked wrong -- and is exactly the situation this rebuild is meant to end.
///
/// That Mistral serves it to fill in the middle of a file rather than to talk to was the one thing
/// the rebuild left behind: it stayed in the Mistral provider, as a name check which dropped every
/// model whose ID begins with "code". It says something about a model, so it belongs to the model,
/// and it is bound to the provider because it is only true there. Somebody's own server and the
/// gateways serve the same weights to chat with, which is what the unbound rule above keeps saying.
/// </remarks>
public sealed class CodestralFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 19), "The answer the previous rules gave it through their fallback: text in, text out, tool calling. What Mistral's own catalog makes of it was read from the same page, where Codestral is the model behind the FIM endpoint.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("codestral").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        //
        // Bound to Mistral, which makes it the more specific of the two and lets it win there
        // without anybody writing an order. It continues a text instead of answering in a
        // conversation, so it must not stand among the models somebody picks for a chat.
        //
        builder.Rule("codestral").AsSegment().OnlyOn(LLMProviders.MISTRAL)
            .Inherits()
            .Kind(ModelKind.TEXT_COMPLETION);
    }
}