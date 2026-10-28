namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// The rule which tells the model to search the web first, for a tool which may end web search.
/// </summary>
/// <remarks>
/// Once internal content enters a chat, it is no longer offered web search, see
/// ToolSelectionRules.IsOutboundDataAllowed. A model which does not know that reads the internal
/// content first and finds the search gone when it needs it. Only the tool which brings the
/// content in knows when it does, so each one adds the rule to its own instructions, and this
/// class words it once for all of them.<br/><br/>
/// The rule belongs only into a request which offers web search: a rule about a tool the model
/// does not get would only confuse it. Once the search is gone, nothing repeats it. A call of the
/// search in the same request is refused with a text which names the source and says that a new
/// chat without this content can search again, see OutboundDataRequirementExtensions.GetToolBlockedMessage, and the
/// tool selection shows the user the same text.
/// </remarks>
internal static class WebSearchFirstRule
{
    /// <summary>
    /// Whether the request may offer web search, so the rule has a search to protect.
    /// </summary>
    /// <param name="context">The request, or null when the registered instructions are worded, which assume web search.</param>
    /// <returns>True when the rule belongs into the instructions.</returns>
    internal static bool AppliesTo(ToolResolutionContext? context) => context?.MayOffer(ToolSelectionRules.WEB_SEARCH_TOOL_ID) ?? true;

    /// <summary>
    /// Words the rule as a further line of the instructions of a tool.
    /// </summary>
    /// <param name="cause">What takes web search away, up to its verb, such as "A search makes".</param>
    /// <param name="action">What the model does with the tool, such as "search the wiki".</param>
    /// <returns>The line, starting with a line break.</returns>
    internal static string Build(string cause, string action) => $"""

        - {cause} `web_search` unavailable for the rest of this chat. When a task needs `web_search` as well, search the web before you {action}.
        """;
}