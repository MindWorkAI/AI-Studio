using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Cohere;

/// <summary>
/// Aya, which comes from Cohere as well and was not built for tools.
/// </summary>
/// <remarks>
/// Their documentation says it in as many words, which is why these are a family of their own
/// rather than a variant of Command: everything the Command rules state would be wrong here.
/// </remarks>
public sealed class AyaFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.COHERE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.cohere.com/docs/aya", new DateOnly(2026, 9, 11), "Ported unchanged from the Aya block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("aya-expanse").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("aya-vision").AsSubstring().Inherits()
            .Capabilities(MULTIPLE_IMAGE_INPUT);
    }
}