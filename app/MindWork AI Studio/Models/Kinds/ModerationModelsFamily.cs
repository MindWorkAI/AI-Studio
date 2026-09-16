using AIStudio.Provider;

namespace AIStudio.Models.Kinds;

/// <summary>
/// The models which judge content instead of writing it.
/// </summary>
/// <remarks>
/// The guard models are the reason this is stated as a plain substring rather than as a name part:
/// Meta writes Llama-Guard-3-8B, where the word stands on its own, but Alibaba writes Qwen3Guard-Gen-8B,
/// where it is glued to the version. A name part would see the first and miss the second.
///
/// Being a modifier is what makes that harmless. Llama-Guard keeps everything the Llama rules say
/// about it and is merely not offered as something to chat with -- which is also why this does not
/// collide with the family it belongs to, although both are substrings of the same length.
/// </remarks>
public sealed class ModerationModelsFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://platform.openai.com/docs/guides/moderation", new DateOnly(2026, 9, 12), "Ported unchanged from the moderation markers of Provider/ModelKindExtensions.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Modifier("moderation").AsSubstring().Kind(ModelKind.MODERATION);

        builder.Modifier("guard").AsSubstring().Inherits();
    }
}