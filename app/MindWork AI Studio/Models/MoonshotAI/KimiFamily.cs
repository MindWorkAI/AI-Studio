using static AIStudio.Provider.Capability;

namespace AIStudio.Models.MoonshotAI;

/// <summary>
/// Kimi, and the older Moonshot line next to it.
/// </summary>
/// <remarks>
/// Moonshot builds these for agentic work, and the K2 model card says it plainly: pass the tools
/// with the request and the model decides on its own when to call them. So the family states tool
/// calling, and the exception has to say otherwise -- which is the vision checkpoint, the one Kimi
/// no vendor lists among the models which call functions.
///
/// The variants are written for the Kimi names only, because that is where Moonshot puts them. The
/// "moonshot" names are the older API line, which answers straight away and has no variants.
/// </remarks>
public sealed class KimiFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MOONSHOT_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/moonshotai", new DateOnly(2026, 9, 11), "Ported unchanged from the Moonshot block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("kimi").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("moonshot").AsSubstring().Inherits();

        // The thinking variants say what they are in their name:
        builder.Rule("kimi").AsSubstring().AlsoContains("thinking").Inherits()
            .Reasoning(ReasoningSupport.ALWAYS);

        // The vision checkpoint thinks as well, and it is the one which calls nothing:
        builder.Rule("kimi-vl").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("kimi-k2.7-code").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);

        // The K3 line watches videos on top:
        builder.Rule("kimi-k3").AsSubstring().Inherits()
            .Capabilities(VIDEO_INPUT);
    }
}