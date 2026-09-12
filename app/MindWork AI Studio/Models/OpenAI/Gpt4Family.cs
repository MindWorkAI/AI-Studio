using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// GPT-4 and GPT-4 Turbo.
/// </summary>
/// <remarks>
/// GPT-4o is not one of these, which the name hides and the matching does not: a rule bound to the
/// start of a name only answers where a name part ends, and in "gpt-4o" the part goes on. The
/// previous rules had to say that twice, once as an exact comparison and once as a prefix.
/// </remarks>
public sealed class Gpt4Family : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://developers.openai.com/api/docs/models/gpt-4-turbo", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.OpenAI.cs: GPT-4 is text only, Turbo adds images and tool calling. The windows are the documented 8,192 tokens of GPT-4 and the 128,000 Turbo raised it to.");

    /// <inheritdoc />
    public override IReadOnlyList<ModelSource> FurtherSources =>
    [
        new("https://github.com/openai/tiktoken/blob/main/tiktoken/model.py", new DateOnly(2026, 9, 12), "OpenAI's own mapping from model names to encodings. It maps \"gpt-4\" and the prefix \"gpt-4-\" to cl100k_base, so Turbo uses it too -- the newer o200k_base begins with the 4o line, which is a family of its own here.")
    ];

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("gpt-4").AsPrefix()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(RESPONSES_API)
            .ContextWindow(8_192)
            .Tokenizer(TokenizerKind.TIKTOKEN, "cl100k_base");

        builder.Rule("gpt-4-turbo").AsPrefix().Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT | FUNCTION_CALLING)
            .ContextWindow(128_000);
    }
}