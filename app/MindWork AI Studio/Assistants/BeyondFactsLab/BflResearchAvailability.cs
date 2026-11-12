using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Whether the research of the BeyondFacts Lab can search the web with a provider, and if not, why.
/// </summary>
/// <remarks>
/// The research needs the tool Web Search; Read Web Page lets it read the pages it found, but it gets by without. Both
/// answers come from ToolRegistry.GetOfferBlockReasonAsync, which asks what the request asks, so that the lab never
/// offers a research which the request would then refuse.
/// </remarks>
/// <param name="WebSearch">What keeps the tool Web Search from being offered, or NONE.</param>
/// <param name="ReadWebPage">What keeps the tool Read Web Page from being offered, or NONE.</param>
public sealed record BflResearchAvailability(ToolOfferBlockReason WebSearch, ToolOfferBlockReason ReadWebPage)
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(BflResearchAvailability).Namespace, nameof(BflResearchAvailability));

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(BflResearchAvailability).Namespace, nameof(BflResearchAvailability));

    /// <summary>
    /// Gets whether the research can search the web.
    /// </summary>
    public bool CanResearch => this.WebSearch is ToolOfferBlockReason.NONE;

    /// <summary>
    /// Gets why the research cannot search the web, for the person; empty while it can.
    /// </summary>
    public string Message => MessageFor(this.WebSearch);

    /// <summary>
    /// Checks whether the research can search the web with a provider.
    /// </summary>
    /// <param name="toolRegistry">The registry of the tools.</param>
    /// <param name="provider">The provider of the preparation, which runs the research.</param>
    /// <returns>What keeps each tool of the research from being offered.</returns>
    public static async Task<BflResearchAvailability> CheckAsync(ToolRegistry toolRegistry, AIStudio.Settings.Provider provider) => new(
        await toolRegistry.GetOfferBlockReasonAsync(ToolSelectionRules.WEB_SEARCH_TOOL_ID, provider, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT),
        await toolRegistry.GetOfferBlockReasonAsync(ToolSelectionRules.READ_WEB_PAGE_TOOL_ID, provider, Tools.Components.BEYOND_FACTS_LAB_ASSISTANT));

    /// <summary>
    /// Tells the person why the research cannot search the web, and what they can do about it, if anything.
    /// </summary>
    /// <param name="reason">What keeps the tool Web Search from being offered.</param>
    /// <returns>The text, empty when nothing is in the way.</returns>
    public static string MessageFor(ToolOfferBlockReason reason) => reason switch
    {
        ToolOfferBlockReason.NONE => string.Empty,
        ToolOfferBlockReason.TOOLS_SWITCHED_OFF => TA("Your organization has switched tools off. Therefore, the lab cannot research the event on the web."),
        ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS => TA("The selected model cannot use tools. Therefore, the lab cannot research the event on the web. To let the lab research, select a provider whose model can use tools."),
        ToolOfferBlockReason.NOT_AVAILABLE_HERE => TB("The tool 'Web Search' is not available in the BeyondFacts Lab. Therefore, the lab cannot research the event on the web."),
        ToolOfferBlockReason.TOOL_SWITCHED_OFF => TA("Your organization has switched the tool 'Web Search' off. Therefore, the lab cannot research the event on the web."),
        ToolOfferBlockReason.TOOL_SWITCHED_OFF_BY_USER => TA("You switched the tool 'Web Search' off in the tool settings. Therefore, the lab cannot research the event on the web. To let the lab research, switch the tool on again there."),
        ToolOfferBlockReason.NOT_CONFIGURED => TA("The tool 'Web Search' is not set up yet. Therefore, the lab cannot research the event on the web. To let the lab research, set the tool up in the tool settings."),
        ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW => TB("The selected provider is not trusted enough for the tool 'Web Search'. Therefore, the lab cannot research the event on the web."),
        ToolOfferBlockReason.OUTBOUND_DATA_RESTRICTED => TB("The tool 'Web Search' may not send data from here. Therefore, the lab cannot research the event on the web."),

        // A reason this version does not know:
        _ => TB("The tool 'Web Search' cannot be used here. Therefore, the lab cannot research the event on the web."),
    };
}