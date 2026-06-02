using System.Collections.Generic;
using System.Linq;

using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

public static class ToolSelectionRules
{
    public const string WEB_SEARCH_TOOL_ID = "web_search";
    public const string READ_WEB_PAGE_TOOL_ID = "read_web_page";

    public static HashSet<string> NormalizeSelection(IEnumerable<string> selectedToolIds)
    {
        var normalized = selectedToolIds.ToHashSet(StringComparer.Ordinal);
        if (normalized.Contains(WEB_SEARCH_TOOL_ID))
            normalized.Add(READ_WEB_PAGE_TOOL_ID);

        return normalized;
    }

    public static bool IsRequiredBySelectedTools(string toolId, IEnumerable<string> selectedToolIds)
    {
        var normalized = NormalizeSelection(selectedToolIds);
        return toolId == READ_WEB_PAGE_TOOL_ID && normalized.Contains(WEB_SEARCH_TOOL_ID);
    }

    public static ConfidenceLevel GetDefaultMinimumProviderConfidence(string toolId) => toolId switch
    {
        WEB_SEARCH_TOOL_ID => ConfidenceLevel.MEDIUM,
        READ_WEB_PAGE_TOOL_ID => ConfidenceLevel.MEDIUM,
        _ => ConfidenceLevel.NONE,
    };

    public static bool IsProviderConfidenceAllowed(ConfidenceLevel providerConfidence, ConfidenceLevel minimumToolConfidence) =>
        minimumToolConfidence is ConfidenceLevel.NONE || providerConfidence >= minimumToolConfidence;
}
