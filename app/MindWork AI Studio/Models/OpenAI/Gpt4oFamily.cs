using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// GPT-4o, including its mini and its audio preview.
/// </summary>
/// <remarks>
/// The previous rules never named this family. Its models reached the last line of the OpenAI
/// function, the one which answers for everything nobody wrote a rule for, and that line happened
/// to describe GPT-4o exactly. Writing it down changes no answer and takes the family out of the
/// fallback, where a wrong answer looks like no answer.
/// </remarks>
public sealed class Gpt4oFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/models/gpt-4o", new DateOnly(2026, 9, 11), "The answer the previous rules gave these models through their fallback: images, tool calling, and web search on the Responses API.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("gpt-4o").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING | WEB_SEARCH)
            .Apis(RESPONSES_API);

        //
        // The search previews are the same generation and almost nothing like it: they search the
        // web and do nothing else, no images and no tools, and they answer only through the chat
        // completion API. Stated in full rather than inherited, because there is barely anything of
        // the family left in them.
        //
        builder.Rule("gpt-4o-search-preview").AsExact()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | WEB_SEARCH)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("gpt-4o-mini-search-preview").AsExact()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | WEB_SEARCH)
            .Apis(CHAT_COMPLETION_API);
    }
}