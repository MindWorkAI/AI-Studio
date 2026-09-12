using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Meta;

/// <summary>
/// Llama, from the text-only generations to the natively multimodal 4 line.
/// </summary>
/// <remarks>
/// Every rule here is written as a substring, which no other family needs and this one cannot do
/// without. The same checkpoint arrives as "llama3.1", as "meta-llama-3.1", and as "llama-v3p1",
/// because Fireworks writes a version with a "p" where the dot belongs. There is no name part all
/// three share to anchor a rule to, so the three spellings are stated as three rules.
///
/// What decides is the generation: 3.1 was the first Llama trained to call functions, which is why
/// the rules carrying the dot are the ones stating it. "llama3" without a dot is Llama 3.0 and does
/// not get it -- the dot in the pattern is what keeps the two apart.
/// </remarks>
public sealed class LlamaFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.META;

    /// <inheritdoc />
    public override ModelSource Source => new("https://www.llama.com/docs/model-cards-and-prompt-formats/", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from the Llama block of ProviderExtensions.OpenSource.cs. The model cards give the 3.x generations a 128k window; the 4 line is not stated here, because Scout and Maverick differ by an order of magnitude and the name alone does not say which one it is.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        // Whatever else a Llama is, it reads and writes text:
        builder.Rule("llama").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        //
        // The 3.2 vision checkpoints look at pictures and were never trained for tools. The word
        // sits wherever the provider puts it -- "llama3.2-vision:11b" on Ollama, but
        // "Llama-3.2-11B-Vision-Instruct" on the hub -- so there is nothing to anchor to here
        // either, and the generations below have to step aside for it by name.
        //
        builder.Rule("llama").AsSubstring().AlsoContains("vision")
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        //
        // From 3.1 on, Llama calls functions and reads 128k tokens. Three spellings, one statement.
        // What an operator actually serves is another matter: Ollama ships with a far smaller window
        // until somebody raises num_ctx, which is why the window of a self-hosted model is a ceiling
        // rather than a promise.
        //
        builder.Rule("llama3.").AsSubstring().NotContains("vision")
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .ContextWindow(131_072);

        builder.Rule("llama-3.").AsSubstring().NotContains("vision").Inherits();

        builder.Rule("llama-v3p").AsSubstring().NotContains("vision").Inherits();

        // The 4 line was trained on text and images together, so every one of them sees:
        builder.Rule("llama4").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("llama-4").AsSubstring().Inherits();

        builder.Rule("llama-v4").AsSubstring().Inherits();
    }
}