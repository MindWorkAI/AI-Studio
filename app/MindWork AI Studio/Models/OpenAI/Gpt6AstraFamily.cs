using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// GPT-6 Astra.
/// </summary>
/// <remarks>
/// Unlike the 5.5 and 5.6 models it reasons on every request: the effort reaches from low to max,
/// and there is no setting which switches thinking off.
/// </remarks>
public sealed class Gpt6AstraFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://developers.openai.com/api/docs/models", new DateOnly(2026, 9, 12), "Capabilities ported unchanged from ProviderExtensions.OpenAI.cs: reasons on every request, both APIs. The models page states the window as 1.05M tokens.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("gpt-6-astra").AsPrefix()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING | WEB_SEARCH)
            .Apis(RESPONSES_API | CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS)
            .ContextWindow(1_050_000);
}