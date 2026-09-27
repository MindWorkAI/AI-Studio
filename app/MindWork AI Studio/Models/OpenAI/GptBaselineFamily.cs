using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.OpenAI;

/// <summary>
/// Every GPT model at OpenAI which no rule of its generation knows yet.
/// </summary>
/// <remarks>
/// OpenAI releases faster than anybody writes rules, and somebody always reaches a new model on the
/// day it appears. Unlike the global assumption, which has to stand for a hundred thousand models
/// of every kind, this one stands for a single line in its vendor's own cloud, and that line is
/// uniform: every GPT chat model OpenAI lists calls tools, looks at images, searches the web,
/// reasons, and answers through the Responses API. Left to the global assumption instead, a new
/// model went to the chat completion API without any of that, and a Responses API parameter the
/// person had set was turned down -- which is what happened to GPT-6 Sol and Luna.
///
/// How it reasons is the one guess in here, and on by default is the guess which can be taken back:
/// the person switches it off in the expert settings, whereas a model stated to always reason
/// could not be told otherwise.
///
/// The rule is written as a name part on purpose. Specificity weighs how a pattern matches before
/// it weighs how long it is, so a prefix rule for "gpt" would outrank the name part "gpt-oss" and
/// answer for the open weights wherever they are served. As a name part, every longer rule beats
/// it. Binding it to OpenAI keeps it away from the other models named that way, such as a
/// self-hosted gpt-j. What the model is made for stays with the modifiers: an image, realtime, or
/// transcription model is recognized by its name as before.
/// </remarks>
public sealed class GptBaselineFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.OPEN_AI;

    /// <inheritdoc />
    public override ModelSource Source => new("https://developers.openai.com/api/docs/models", new DateOnly(2026, 9, 27), "Every GPT chat model the page lists supports function calling, image input, web search, reasoning, and the Responses API. The models which are no chat models carry image, realtime, live, transcribe, or tts in their names.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("gpt").AsSegment().OnlyOn(LLMProviders.OPEN_AI)
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | FUNCTION_CALLING | WEB_SEARCH)
            .Apis(RESPONSES_API)
            .Reasoning(ReasoningSupport.ON_BY_DEFAULT);
}