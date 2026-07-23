using System.Collections.Generic;
using System.Linq;

using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

public static class ToolSelectionRules
{
    public const int MAX_TOOL_CALLS = 15;
    public const int MAX_TOOL_RESULT_CHARACTERS = 300_000;
    public const string WEB_SEARCH_TOOL_ID = "web_search";
    public const string READ_WEB_PAGE_TOOL_ID = "read_web_page";

    public static HashSet<string> NormalizeSelection(IEnumerable<string> selectedToolIds)
        => selectedToolIds.ToHashSet(StringComparer.Ordinal);

    public static ConfidenceLevel GetDefaultMinimumProviderConfidence(string toolId) => toolId switch
    {
        WEB_SEARCH_TOOL_ID => ConfidenceLevel.MEDIUM,
        READ_WEB_PAGE_TOOL_ID => ConfidenceLevel.MEDIUM,
        _ => ConfidenceLevel.NONE,
    };

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
            .Select(x => (ToolName: x.Function.Name, PolicyLines: x.SystemPromptInstructions?.Trim()))
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
