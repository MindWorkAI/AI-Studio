using static AIStudio.Provider.Capability;

namespace AIStudio.Models.MiniMax;

/// <summary>
/// MiniMax, whose M line thinks while it works.
/// </summary>
/// <remarks>
/// What MiniMax calls interleaved thinking is reasoning between the tool calls: it is part of the
/// answer rather than something the request switches on, so the M models always think. The older
/// Text-01 answers directly.
/// </remarks>
public sealed class MiniMaxFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MINIMAX;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/MiniMaxAI", new DateOnly(2026, 9, 11), "Ported unchanged from the MiniMax block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("minimax").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("minimax-m").AsSubstring().Inherits()
            .Reasoning(ReasoningSupport.ALWAYS);
    }
}