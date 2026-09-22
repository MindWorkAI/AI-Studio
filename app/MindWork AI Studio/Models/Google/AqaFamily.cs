using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// AQA, which answers a question out of the passages it was handed.
/// </summary>
/// <remarks>
/// Attributed Question Answering, and the one entry in Google's catalog whose whole name is three
/// letters. It answers on generateAnswer rather than on generateContent, together with the semantic
/// retriever, and what comes back is the answer, the passages it rests on, and an estimate of
/// whether the question could be answered from them at all.
///
/// Bound to Google, and it has to be: three letters are three letters, and a rule that short has no
/// business meeting a name from somewhere else. The catalog holds exactly one model it can match.
/// </remarks>
public sealed class AqaFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/semantic_retrieval", new DateOnly(2026, 9, 19), "Reads 7,168 tokens and writes 1,024, which is a size for an answer rather than for a conversation. The route it answers on is generateAnswer, so nothing the app sends over the chat completion API reaches it.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder) =>
        builder.Rule("aqa").AsExact().OnlyOn(LLMProviders.GOOGLE)
            .Capabilities(TEXT_INPUT | TEXT_OUTPUT)
            .Kind(ModelKind.GROUNDED_ANSWERING);
}