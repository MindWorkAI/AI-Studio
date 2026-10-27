using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;

public static class ToolSelectionRules
{
    public const int MAX_TOOL_CALLS = 15;
    public const int MAX_TOOL_RESULT_CHARACTERS = 300_000;
    public const string WEB_SEARCH_TOOL_ID = "web_search";
    public const string READ_WEB_PAGE_TOOL_ID = "read_web_page";
    public const string SEARCH_CONFLUENCE_TOOL_ID = "search_confluence";
    public const string SEMANTIC_SEARCH_TOOL_ID = "semantic_search";
    public const string SEARCH_MAILS_TOOL_ID = "search_mails";
    public const string READ_MAIL_TOOL_ID = "read_mail";
    public const string COUNT_MAILS_TOOL_ID = "count_mails";
    public const string MAILBOXES_COLLECTION_ID = "mailboxes";

    public static string GetMaxToolCallsFinalResponseInstruction() => $"The maximum of {MAX_TOOL_CALLS} tool calls has been reached. No more tools are available. Provide the best possible final answer to the user based on the tool results already available.";

    public static string GetMaxToolResultCharactersFinalResponseInstruction() => $"The maximum total of {MAX_TOOL_RESULT_CHARACTERS} characters across tool call results has been exceeded. Do not make any more tool calls. Provide the best possible final answer to the user based on the tool results already available.";

    public static string? GetToolCallsUnavailableInstruction(int toolCallCount, long toolResultCharacterCount)
    {
        if (toolResultCharacterCount > MAX_TOOL_RESULT_CHARACTERS)
            return GetMaxToolResultCharactersFinalResponseInstruction();

        return toolCallCount >= MAX_TOOL_CALLS
            ? GetMaxToolCallsFinalResponseInstruction()
            : null;
    }

    public static string BuildToolPolicyPrompt(IEnumerable<ToolDefinition> definitions)
    {
        var policySections = definitions
            .Select(x => (ToolName: x.Function.Name, PolicyLines: x.SystemPromptInstructions.Trim()))
            .Where(x => !string.IsNullOrWhiteSpace(x.PolicyLines))
            .Select(x => $"## Tool `{x.ToolName}`{Environment.NewLine}{x.PolicyLines}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (policySections.Count == 0)
            return string.Empty;

        var toolPolicyPrompt = $"""
                            # Tool usage instructions:
                            You have multiple tools available. Each tool has a different purpose and usage policy. Choose wisely and if you are not sure, always ask the user for clarification. You must follow the usage policy of each tool to ensure accurate and reliable results. Here are the usage policies for each tool:

                            {string.Join(Environment.NewLine+Environment.NewLine, policySections)}
                            """;

        return toolPolicyPrompt;
    }

    public static bool IsProviderConfidenceAllowed(ConfidenceLevel providerConfidence, ConfidenceLevel minimumToolConfidence) =>
        minimumToolConfidence is ConfidenceLevel.NONE || providerConfidence >= minimumToolConfidence;

    /// <summary>
    /// Whether a tool may run in a chat with this outbound data restriction.
    /// </summary>
    /// <remarks>
    /// Services configured in AI Studio stay allowed on every level. Below UNRESTRICTED, both
    /// queries to third parties and addresses the model chooses are kept back, unless the tool
    /// keeps to the restriction itself. That is why both stricter levels decide alike here: they
    /// differ only in what such a tool lets through. A level this version does not know is treated
    /// like a strict one.
    /// </remarks>
    /// <param name="restriction">Where the chat may still send data.</param>
    /// <param name="implementation">The tool.</param>
    /// <returns>True when the tool may run.</returns>
    public static bool IsOutboundDataAllowed(OutboundDataRestriction restriction, IToolImplementation implementation) =>
        restriction is OutboundDataRestriction.UNRESTRICTED ||
        implementation.OutboundData is ToolOutboundData.NONE or ToolOutboundData.CONFIGURED_SERVICE ||
        implementation.EnforcesOutboundDataRestriction;

    /// <summary>
    /// Whether an entry of the catalog has a tool left which may run in a chat with this outbound data restriction.
    /// </summary>
    /// <remarks>
    /// Only when the restriction keeps every tool of a collection back is there nothing left to
    /// select. Should it keep back some of them, the others still run, and the request leaves out
    /// the rest on its own.
    /// </remarks>
    /// <param name="restriction">Where the chat may still send data.</param>
    /// <param name="item">The entry of the catalog.</param>
    /// <returns>True when at least one tool of the entry may run.</returns>
    public static bool IsOutboundDataAllowed(OutboundDataRestriction restriction, ToolCatalogItem item) =>
        item.Tools.Any(tool => IsOutboundDataAllowed(restriction, tool.Implementation));

    /// <summary>
    /// Whether the user can select an entry of the catalog right now.
    /// </summary>
    /// <remarks>
    /// The tool selection shows the entries this denies apart from the others, together with the
    /// reason, so the answer has to cover everything that lies with the entry itself: it must not be
    /// switched off, its settings must be complete, the provider must have the confidence it needs,
    /// and the outbound data restriction of the chat must leave one of its tools. What applies to all
    /// entries alike, such as a running response or a model without tools, stays with the caller.
    /// </remarks>
    /// <param name="item">The entry of the catalog.</param>
    /// <param name="providerConfidence">The confidence of the selected provider.</param>
    /// <param name="restriction">Where the chat may still send data.</param>
    /// <returns>True when the entry can be selected.</returns>
    public static bool IsSelectable(ToolCatalogItem item, ConfidenceLevel providerConfidence, OutboundDataRestriction restriction) =>
        item.IsActive &&
        item.ConfigurationState.IsConfigured &&
        IsProviderConfidenceAllowed(providerConfidence, item.MinimumProviderConfidence) &&
        IsOutboundDataAllowed(restriction, item);
}
