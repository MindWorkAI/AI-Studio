using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// GPT-3.5, which answers with text and does nothing else.
/// </summary>
public sealed class Gpt35Family : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.OpenAI.cs: text in, text out, no tools and no images.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("gpt-3.5").AsPrefix()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        //
        // The odd one out, and kept odd on purpose: the previous rules put this one model on the
        // Responses API and every other GPT-3.5 on the chat completion API. It reads like an
        // oversight, but what the app answers today is what the snapshot pins, and correcting it is
        // a decision of its own rather than something to slip into a port.
        //
        builder.Rule("gpt-3.5-turbo").AsExact()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(RESPONSES_API);
    }
}