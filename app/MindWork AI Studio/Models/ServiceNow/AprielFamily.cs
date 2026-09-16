using static AIStudio.Provider.Capability;

namespace AIStudio.Models.ServiceNow;

/// <summary>
/// Apriel, from ServiceNow.
/// </summary>
/// <remarks>
/// The Thinker models see, and they always reason: their default chat template opens the thinking
/// channel, so there is nothing to switch on and nothing to switch off.
///
/// This family exists although the line is a small one, and the reason is the tool tokens. They
/// arrived with 1.6; 1.5 has none. Left to the assumption, 1.5 would be offered tools it cannot
/// use -- which is the one direction the switch-over must not take, because nobody decided it and
/// nothing would show it until a request comes back as an error.
/// </remarks>
public sealed class AprielFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.SERVICE_NOW;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/ServiceNow-AI/Apriel-1.5-15b-Thinker", new DateOnly(2026, 9, 12), "Ported unchanged from the Apriel block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("apriel").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("apriel-1.5").AsSubstring().Inherits()
            .Removes(FUNCTION_CALLING);
    }
}