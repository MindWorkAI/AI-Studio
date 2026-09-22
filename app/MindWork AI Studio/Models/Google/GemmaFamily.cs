using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// Gemma, the open weights Google publishes next to Gemini.
/// </summary>
/// <remarks>
/// Two generations and one spelling problem. Ollama writes "gemma3:27b" and the hub writes
/// "gemma-3-27b-it", and no normalization turns one into the other, so each statement stands twice.
/// What it buys is that the rules never have to ask who served the model.
///
/// Tool calling is the line between the generations. What Google documents for Gemma 3 is writing
/// the tool descriptions into the prompt by hand, which is a different thing from what the tools
/// field of an OpenAI-compatible request does: the chat template has neither a tool role nor tool
/// tokens, and Ollama refuses a request carrying tools for these models. Gemma 4 is the first with
/// tokens of its own, and the first that thinks -- when the request opens the thinking channel.
/// </remarks>
public sealed class GemmaFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemma/docs/core", new DateOnly(2026, 9, 11), "Ported unchanged from the Gemma block of ProviderExtensions.OpenSource.cs.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        // The early generations take text only and were not built for tools:
        builder.Rule("gemma").AsSubstring()
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        // Gemma 3 reads pictures from the 4B checkpoint upwards:
        builder.Rule("gemma3").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("gemma-3").AsSubstring().Inherits();

        // The 1B checkpoint is the one that does not:
        builder.Rule("gemma3").AsSubstring().AlsoContains("1b")
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("gemma-3").AsSubstring().AlsoContains("1b").Inherits();

        //
        // The 3n checkpoints listen as well. Video is not a modality of any Gemma: the model cards
        // list text, image, and audio, and mention video only as frames somebody else cut it into.
        //
        builder.Rule("gemma3n").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | AUDIO_INPUT | TEXT_OUTPUT)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("gemma-3n").AsSubstring().Inherits();

        // Every checkpoint of Gemma 4 is multimodal; there is no text-only variant of it:
        builder.Rule("gemma4").AsSubstring()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING)
            .Apis(CHAT_COMPLETION_API)
            .Reasoning(ReasoningSupport.OPTIONAL);

        builder.Rule("gemma-4").AsSubstring().Inherits();

        // Three of its checkpoints hear, and they are named one by one because that is all they
        // have in common:
        builder.Rule("gemma4").AsSubstring().AlsoContains("e2b").Inherits().Capabilities(AUDIO_INPUT);
        builder.Rule("gemma-4").AsSubstring().AlsoContains("e2b").Inherits();

        builder.Rule("gemma4").AsSubstring().AlsoContains("e4b").Inherits();
        builder.Rule("gemma-4").AsSubstring().AlsoContains("e4b").Inherits();

        builder.Rule("gemma4").AsSubstring().AlsoContains("12b").Inherits();
        builder.Rule("gemma-4").AsSubstring().AlsoContains("12b").Inherits();
    }
}