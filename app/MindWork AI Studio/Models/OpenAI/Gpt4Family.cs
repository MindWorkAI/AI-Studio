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
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("gpt-4").AsPrefix()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(RESPONSES_API)
            .ContextWindow(8_192);

        builder.Rule("gpt-4-turbo").AsPrefix().Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT | FUNCTION_CALLING)
            .ContextWindow(128_000);
    }
}