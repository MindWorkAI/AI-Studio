using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

public static class ToolSelectionRules
{
    public const int MAX_TOOL_CALLS = 15;
    public const int MAX_TOOL_RESULT_CHARACTERS = 300_000;
    public const string WEB_SEARCH_TOOL_ID = "web_search";
    public const string READ_WEB_PAGE_TOOL_ID = "read_web_page";
    public const string SEARCH_CONFLUENCE_TOOL_ID = "search_confluence";
    public const string SEMANTIC_SEARCH_TOOL_ID = "semantic_search";

    /// <summary>
    /// Turns a set of selected tool IDs into the set which actually runs.
    /// </summary>
    /// <remarks>
    /// Removes duplicates and adds the tools another one depends on: Search Confluence only finds
    /// pages, so it brings Read Web Page along to open them. An added tool keeps its own rules.
    /// ToolRegistry still drops it when it is switched off or the provider's confidence is too
    /// low, and Read Web Page reaches a wiki on a private or VPN address only when its host is
    /// allowed there.<br/><br/>
    /// It also removes the tools nobody selects. Semantic Search offers itself whenever the data
    /// sources of a chat call for it, see ToolActivation.CONTEXT; kept in a selection, it would
    /// appear on the security card of a plugin and in its audit without the selection having any
    /// say in whether it runs.<br/><br/>
    /// Every place which shows or stores a selection normalizes it, the tool selection fields
    /// included. That way a chat, a template, a policy, or an assistant plugin shows the tools
    /// which will actually run, and the audit of a plugin judges exactly those.
    /// </remarks>
    public static HashSet<string> NormalizeSelection(IEnumerable<string> selectedToolIds)
    {
        var normalized = selectedToolIds.ToHashSet(StringComparer.Ordinal);
        if (normalized.Contains(SEARCH_CONFLUENCE_TOOL_ID))
            normalized.Add(READ_WEB_PAGE_TOOL_ID);

        normalized.Remove(SEMANTIC_SEARCH_TOOL_ID);
        return normalized;
    }

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
}
