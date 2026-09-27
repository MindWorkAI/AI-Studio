using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// The whole GPT-6 line: Astra, Sol, and Luna.
/// </summary>
/// <remarks>
/// One family for the generation rather than one per model, because the three differ in a single
/// sentence: Astra reasons on every request -- its effort reaches from low to max and nothing
/// switches thinking off -- while Sol and Luna reason unless the effort is set to none. Everything
/// else, the window included, they share.
///
/// The rule for the generation is also what answers for a GPT-6 model OpenAI releases after this
/// was written. That is on purpose: this family used to know Astra alone, and Sol and Luna went to
/// the global assumption, which sent them to the chat completion API without images, web search,
/// or reasoning.
///
/// Sol's page adds that calling functions through the chat completion API needs the effort set to
/// none. That concerns only the gateways which reach these models through that API; OpenAI itself
/// is asked through the Responses API.
/// </remarks>
public sealed class Gpt6Family : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://developers.openai.com/api/docs/models", new DateOnly(2026, 9, 27), "The models page lists gpt-6-astra, gpt-6-sol, and gpt-6-luna as the GPT-6 line.");

    /// <inheritdoc />
    public override IReadOnlyList<ModelSource> FurtherSources =>
    [
        new("https://developers.openai.com/api/docs/models/gpt-6-astra", new DateOnly(2026, 9, 27), "Text and image input, text output, both APIs, function calling, and web search. A window of 1,050,000 tokens. The effort reaches from low to max, with no way to turn reasoning off."),
        new("https://developers.openai.com/api/docs/models/gpt-6-sol", new DateOnly(2026, 9, 27), "The same abilities and window as Astra. The effort reaches from none to max and defaults to medium."),
        new("https://developers.openai.com/api/docs/models/gpt-6-luna", new DateOnly(2026, 9, 27), "The same abilities and window as Astra. The effort reaches from none to max and defaults to medium.")
    ];

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("gpt-6").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING | WEB_SEARCH)
            .Apis(RESPONSES_API | CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT)
            .ContextWindow(1_050_000)
            .SystemPromptRole(SystemPromptRole.DEVELOPER);

        builder.Rule("gpt-6-astra").AsPrefix().InheritsFrom("gpt-6")
            .Reasoning(ReasoningSupport.ALWAYS);
    }
}