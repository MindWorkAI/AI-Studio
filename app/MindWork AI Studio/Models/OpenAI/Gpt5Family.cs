using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// The whole GPT-5 line, from GPT-5 to GPT-5.6.
/// </summary>
/// <remarks>
/// One family rather than six, because the generations differ in one sentence each and stating that
/// sentence is the entire content: GPT-5 reasons always and answers only through the Responses API,
/// GPT-5.1 reasons on request and answers through both, GPT-5.5 reasons unless told not to.
///
/// The dot is what keeps the generations apart. A rule bound to the start of a name ends at a name
/// part, and a dot does not end one, so "gpt-5" does not answer for "gpt-5.1" -- which is exactly
/// what the previous rules spelled out one comparison at a time.
///
/// None of these models writes images itself. They can ask for one through the image generation
/// tool, which is a tool call producing a picture from a separate model, and reporting that as an
/// output modality would have the chat offer to receive images which never arrive.
/// </remarks>
public sealed class Gpt5Family : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://developers.openai.com/api/docs/models", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.OpenAI.cs, one rule per generation, except that the chat alias no longer inherits the reasoning it is named for not having. Context windows read per generation from the model pages below that URL.");

    /// <inheritdoc />
    public override IReadOnlyList<ModelSource> FurtherSources =>
    [
        new("https://github.com/openai/tiktoken/blob/main/tiktoken/model.py", new DateOnly(2026, 9, 12), "OpenAI's own mapping from model names to encodings. It maps the prefix \"gpt-5\" to o200k_base, which covers every model of this line.")
    ];

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        //
        // The window grows once in this line, between 5.3 and 5.4: everything up to 5.2 is
        // documented at 400,000 tokens and everything from 5.4 on at 1,050,000. Both numbers are
        // the whole window, input and output together, which is how OpenAI states them.
        //
        builder.Rule("gpt-5").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING | WEB_SEARCH)
            .Apis(RESPONSES_API)
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(400_000)
            .Tokenizer(TokenizerKind.TIKTOKEN, "o200k_base");

        //
        // The alias for the model of this generation which does not reason. The previous rules had
        // it swallowed by the prefix above and told it that it always reasons, which is the one
        // thing its name rules out. Here the longer pattern simply wins.
        //
        builder.Rule("gpt-5-chat").AsPrefix().Inherits()
            .Reasoning(ReasoningSupport.NONE);

        builder.Rule("gpt-5.1").AsPrefix().InheritsFrom("gpt-5")
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("gpt-5.2").AsPrefix().InheritsFrom("gpt-5.1");

        //
        // The one generation OpenAI documents nothing about: there is no model page for it, so the
        // rule exists to keep a 5.3 answering like the rest of the line if one ever appears. What it
        // must not do is carry 5.1's window as if somebody had looked it up.
        //
        builder.Rule("gpt-5.3").AsPrefix().InheritsFrom("gpt-5.1")
            .WithoutContextWindow();

        builder.Rule("gpt-5.4").AsPrefix().InheritsFrom("gpt-5.1")
            .ContextWindow(1_050_000);

        builder.Rule("gpt-5.5").AsPrefix().InheritsFrom("gpt-5.4")
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("gpt-5.6").AsPrefix().InheritsFrom("gpt-5.5");
    }
}