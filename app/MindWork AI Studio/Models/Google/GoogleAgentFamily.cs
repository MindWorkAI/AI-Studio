using AIStudio.Provider;

using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Google;

/// <summary>
/// The Google models which are handed a job rather than a message.
/// </summary>
/// <remarks>
/// Both of these answer on the Interactions API alone, never on generateContent: one request starts
/// an autonomous loop which plans, runs code, manages files and searches the web, and a research
/// run takes minutes rather than seconds. A chat request does not time out against them -- it never
/// arrives.
///
/// Deep Research is the reason these rules are bound to Google instead of standing among the kinds.
/// Perplexity sells something under that name too, and sonar-deep-research is an ordinary chat
/// model with web search bolted on, stated in its own family. The same two words, two different
/// things, and only the provider tells them apart. Antigravity needs no such guard -- nobody else
/// names a model that -- but it is a statement about Google's catalog all the same, so it stands
/// where the other one stands.
/// </remarks>
public sealed class GoogleAgentFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/deep-research", new DateOnly(2026, 9, 19), "Deep Research runs only through the Interactions API and only in the background, because a single run takes five to twenty minutes. The Antigravity agent is documented at https://ai.google.dev/gemini-api/docs/antigravity-agent and works the same way, on a sandbox Google hosts.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("deep-research").AsPrefix().OnlyOn(LLMProviders.GOOGLE)
            .Capabilities(TEXT_INPUT)
            .Kind(ModelKind.AGENT);

        builder.Rule("antigravity").AsSegment().OnlyOn(LLMProviders.GOOGLE)
            .Capabilities(TEXT_INPUT)
            .Kind(ModelKind.AGENT);
    }
}