using static AIStudio.Provider.Capability;

namespace AIStudio.Models.Perplexity;

/// <summary>
/// Sonar, the Perplexity models which search the web before they answer.
/// </summary>
/// <remarks>
/// Searching is what they are, not something they can be asked to do, so every one of them states
/// it. What differs is only whether the model thinks as well.
///
/// No Sonar writes images. What looks like it does is the option to have the answer come with
/// pictures: those are images the search found on the pages it read, handed back as links, and
/// reporting that as an output modality would have the chat wait for pictures which never arrive.
/// </remarks>
public sealed class SonarFamily : ModelFamily
{
    /// <inheritdoc />
    public override ModelVendor Vendor => ModelVendor.PERPLEXITY;

    /// <inheritdoc />
    public override ModelSource Source => new("https://docs.perplexity.ai/getting-started/models", new DateOnly(2026, 9, 11), "Ported unchanged from the rules in ProviderExtensions.Perplexity.cs: images in, web search always, thinking for the reasoning and research models.");

    /// <inheritdoc />
    protected override void Declare(ModelFamilyBuilder builder)
    {
        builder.Rule("sonar").AsSegment()
            .Capabilities(TEXT_INPUT | MULTIPLE_IMAGE_INPUT | TEXT_OUTPUT | WEB_SEARCH)
            .Apis(CHAT_COMPLETION_API);

        builder.Rule("sonar").AsSegment().AlsoContains("reasoning").Inherits()
            .Reasoning(ReasoningSupport.ALWAYS);

        builder.Rule("sonar").AsSegment().AlsoContains("deep-research").Inherits();
    }
}