using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Microsoft;

/// <summary>
/// Phi, the small Microsoft models, of which the fourth generation is the one with rules.
/// </summary>
/// <remarks>
/// What a Phi 4 checkpoint can do is written in its name, and two of those words can stand in the
/// same one. "Phi-4-mini-reasoning" is both, and the previous rules had to look for the thinking
/// first so the mini check would not claim it and state the opposite. Here the mini rule says out
/// loud that it does not speak for the thinking checkpoints, which is the same statement without an
/// order behind it.
///
/// Tool calling follows the chat template rather than the size: the mini and multimodal checkpoints
/// carry tool tokens, the 14B model has no tool role at all, and neither do the thinking ones.
/// </remarks>
public sealed class PhiFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MICROSOFT;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/microsoft", new DateOnly(2026, 9, 11), "Ported unchanged from the Phi block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        // The 14B model answers in text and has nothing to call a function with:
        builder.Rule("phi4").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("phi-4").AsSubstring().Inherits();

        // The mini checkpoints call functions, and they are not the thinking ones:
        builder.Rule("phi4").AsSubstring().AlsoContains("mini").NotContains("reasoning").Inherits()
            .Capabilities(FUNCTION_CALLING);

        builder.Rule("phi-4").AsSubstring().AlsoContains("mini").NotContains("reasoning").Inherits();

        // The multimodal one reads pictures and listens:
        builder.Rule("phi4").AsSubstring().AlsoContains("multimodal").Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT | AUDIO_INPUT);

        builder.Rule("phi-4").AsSubstring().AlsoContains("multimodal").Inherits();

        // The thinking checkpoints always think, and they call nothing:
        builder.Rule("phi4").AsSubstring().AlsoContains("reasoning")
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("phi-4").AsSubstring().AlsoContains("reasoning").Inherits();

        // One of them looks at pictures while it does:
        builder.Rule("phi4").AsSubstring().AlsoContains("reasoning", "vision").Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT);

        builder.Rule("phi-4").AsSubstring().AlsoContains("reasoning", "vision").Inherits();
    }
}