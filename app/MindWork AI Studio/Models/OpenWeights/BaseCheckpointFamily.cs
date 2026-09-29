using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenWeights;

/// <summary>
/// Base checkpoints, whatever family they come from.
/// </summary>
/// <remarks>
/// A base checkpoint is the model before anybody taught it to answer: it continues a text, it knows
/// no chat template, and there is nothing in it that a tool definition could reach. Which family it
/// belongs to changes none of that, which is why this states a modifier rather than a rule of its
/// own -- the family says what the model is, and this takes away what the instruction tuning would
/// have added.
///
/// Reading pictures goes with it. The vision tower may well be there, but without a template there
/// is no way to hand an image to it, so promising the chat that it can send one would be a promise
/// nobody can keep.
///
/// The name part has to be exactly "base", so that a model whose name merely carries the word, as
/// in "based", is left alone.
/// </remarks>
public sealed class BaseCheckpointFamily : ModelFamily
{
    private const Capability WHAT_THE_INSTRUCTION_TUNING_WOULD_HAVE_ADDED =
        SINGLE_IMAGE_INPUT | MULTIPLE_IMAGE_INPUT | AUDIO_INPUT | SPEECH_INPUT | VIDEO_INPUT |
        AUDIO_OUTPUT | IMAGE_OUTPUT | SPEECH_OUTPUT | VIDEO_OUTPUT |
        FUNCTION_CALLING | WEB_SEARCH;

    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.UNKNOWN;

    /// <inheritdoc />
    public override ModelSource Source => new("https://huggingface.co/docs/transformers/en/chat_templating", new DateOnly(2026, 9, 11), "Ported unchanged from the base checkpoint check of ProviderExtensions.OpenSource.cs, which answers before any family is asked.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Modifier("base").AsSegment()
            .Removes(WHAT_THE_INSTRUCTION_TUNING_WOULD_HAVE_ADDED)
            .Reasoning(ReasoningSupport.NONE);
}