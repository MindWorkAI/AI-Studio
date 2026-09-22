using AIStudio.Models.Matching;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.ZAI;

/// <summary>
/// GLM, from Z AI.
/// </summary>
/// <remarks>
/// Two things about these names need saying. Z AI writes the version with a dot, but Mistral serves
/// the same models as "glm-5-2" and "zai-glm-5-2", so each generation is stated in both spellings.
/// And a vision model is marked by a "v" glued to the version number -- glm-4v, glm-4.1v, glm-4.5v
/// -- which is not a name part and therefore not something a pattern can ask about. That is what
/// the refinement below is for.
///
/// Looking for a bare "v" anywhere, which the previous rules started out doing, calls every
/// quantized build a vision model: "nvfp4" carries one, and so does the name of more than one
/// inference provider. The digit in front is what makes it a version marker.
/// </remarks>
public sealed class GlmFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.Z_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/zai-org", new DateOnly(2026, 9, 11), "Ported unchanged from the Z AI block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    public override ModelProfile Refine(in ModelId id, in ModelProfile selected)
    {
        if (!MarksAVisionModel(id.Normalized.AsSpan()))
            return selected;

        return selected with { Capabilities = selected.Capabilities | MULTIPLE_IMAGE_INPUT };
    }

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        // Every other GLM thinks when the request asks it to:
        builder.Rule("glm").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);

        // The 4 line answers straight away:
        builder.Rule("glm-4").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API);

        // 5.2 thinks unless it is told not to:
        builder.Rule("glm-5.2").AsSegment()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);

        builder.Rule("glm-5-2").AsSegment().Inherits();

        // 5.3 thinks whatever it is told: only the effort can be lowered, not the thinking itself.
        builder.Rule("glm-5.3").AsSegment()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("glm-5-3").AsSegment().Inherits();
    }

    /// <summary>
    /// Whether the version number of this name is followed by the vision marker.
    /// </summary>
    /// <param name="modelName">The normalized model name.</param>
    /// <returns>True, when a "v" sits directly behind a digit.</returns>
    private static bool MarksAVisionModel(ReadOnlySpan<char> modelName)
    {
        for (var index = 1; index < modelName.Length; index++)
            if (modelName[index] is 'v' && char.IsAsciiDigit(modelName[index - 1]))
                return true;

        return false;
    }
}