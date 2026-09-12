using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Cohere;

/// <summary>
/// Command, the Cohere line built for tool use.
/// </summary>
/// <remarks>
/// Most of the line calls functions, in one step and in several, so the family states it. Command A
/// Vision is the exception Cohere names outright: tool use is not supported with it.
/// </remarks>
public sealed class CommandFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.COHERE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.cohere.com/docs/models", new DateOnly(2026, 9, 11), "Ported unchanged from the Command block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("command-a").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("command-r").AsSubstring().Inherits();

        // Command A+ sees, and thinks unless the request turns the thinking off:
        builder.Rule("command-a-plus").AsSubstring().Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT)
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("command-a-reasoning").AsSubstring().InheritsFrom("command-r")
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("command-a-vision").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);
    }
}