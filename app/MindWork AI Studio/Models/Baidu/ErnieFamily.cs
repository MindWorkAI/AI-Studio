using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Baidu;

/// <summary>
/// ERNIE, from Baidu.
/// </summary>
/// <remarks>
/// The line calls functions and the thinking checkpoints keep the channel open whatever the request
/// says. The vision checkpoints are the exception, and the reason this family is written down at
/// all: they run in a thinking and a non-thinking mode, and tool calling is not documented for them.
/// Left to the assumption they would be offered tools nobody has said they can use.
///
/// The vision rule wins over the thinking rule by the latter stepping aside rather than by being
/// less specific: ERNIE ships a checkpoint which is both, and two rules claiming it with the same
/// right would be a coin toss.
/// </remarks>
public sealed class ErnieFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.BAIDU;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ernie.baidu.com/blog/", new DateOnly(2026, 9, 12), "Ported unchanged from the ERNIE block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("ernie").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("ernie").AsSubstring().AlsoContains("thinking").NotContains("vl").Inherits()
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("ernie").AsSubstring().AlsoContains("vl")
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);
    }
}