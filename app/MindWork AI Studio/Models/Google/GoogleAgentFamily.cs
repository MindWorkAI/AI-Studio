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
/// Perplexity and OpenAI both sell something under that name, and both of those answer over the
/// chat completion API like any other model: sonar-deep-research states it in its own family, and
/// o3-deep-research is held in the corpus. The same two words, three different things, and only the
/// provider tells them apart.
///
/// Written as a prefix on top of that, because Google puts the words at the front of the name while
/// the other two hang them onto a model they already had. Either guard alone would do; together
/// they also cover whatever Google names this way next.
///
/// Antigravity needs no such guard -- nobody else names a model that -- but it is a statement about
/// Google's catalog all the same, so it stands where the other one stands.
/// </remarks>
public sealed class GoogleAgentFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.GOOGLE;

    /// <inheritdoc />
    public override ModelSource Source => new("https://ai.google.dev/gemini-api/docs/deep-research", new DateOnly(2026, 9, 19), "The page states it for the two 04-2026 models: Deep Research runs only through the Interactions API, never through generateContent, and only in the background, because a single run takes five to twenty minutes. The catalog also serves deep-research-pro-preview-12-2025, which the page no longer lists; that it works the same way is read off the naming line rather than off a source. The Antigravity agent is documented at https://ai.google.dev/gemini-api/docs/antigravity-agent and runs on a sandbox Google hosts.");

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