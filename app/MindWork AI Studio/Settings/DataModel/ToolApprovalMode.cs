namespace AIStudio.Settings.DataModel;

/// <summary>
/// Which tool calls the user confirms before they run, see ToolApprovalRules.
/// </summary>
/// <remarks>
/// The outbound data restriction of a chat applies in every mode: a tool which may not run in a
/// chat holding internal content does not run, whatever the user would confirm.
/// </remarks>
public enum ToolApprovalMode
{
    /// <summary>
    /// Only calls whose arguments leave for third parties, such as a web search.
    /// </summary>
    ASK_FOR_CRITICAL_TOOLS,

    /// <summary>
    /// Every call of every tool, also those which stay with AI Studio or the organization.
    /// </summary>
    ASK_FOR_EVERY_TOOL,

    /// <summary>
    /// No call at all.
    /// </summary>
    NEVER_ASK,
}