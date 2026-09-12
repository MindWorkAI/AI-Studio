using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Mistral;

/// <summary>
/// Codestral, the Mistral models for writing code.
/// </summary>
/// <remarks>
/// The previous rules never named it. Its name contains neither "mistral" nor any of the other
/// words the Mistral block looked for, so it walked past every rule and reached the answer meant
/// for everything nobody had written one for. That answer happened to describe it correctly, which
/// is why nothing looked wrong -- and is exactly the situation this rebuild is meant to end.
/// </remarks>
public sealed class CodestralFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.MISTRAL_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.mistral.ai/getting-started/models/models_overview/", new DateOnly(2026, 9, 11), "The answer the previous rules gave it through their fallback: text in, text out, tool calling.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("codestral").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);
}