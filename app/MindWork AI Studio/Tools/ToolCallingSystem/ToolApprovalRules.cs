using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Which tool calls the user has to confirm before they run.
/// </summary>
/// <remarks>
/// The user chooses how much is asked, see DataTools.ToolApprovalMode: every call, none, or only
/// the critical ones, which is the default. A call is critical when its arguments, which the model
/// wrote, leave for somebody else: a web search engine gets the query, and a web page the model
/// chose gets its address, which can carry data out as well. Tools which stay with AI Studio or with
/// a service configured in it, such as the wiki or the data sources, are not critical. A tool which
/// knows better for a single call says so in IToolImplementation.RequiresApproval, as Read Web Page
/// does for an address the model was given.<br/><br/>
/// The loop asks the user, see ToolCallingLoopContext.ApproveToolCallAsync. Where nobody could
/// answer, such as in a request an assistant runs out of sight, a call runs only when the user
/// allowed its collection beforehand, see ChatThread.RuntimeApprovedToolCollectionIds.
/// </remarks>
public static class ToolApprovalRules
{
    /// <summary>
    /// What the model reads instead of a result when the user declined a call.
    /// </summary>
    /// <remarks>
    /// The wording is tuned with care: the model has to go on with whatever else the user asked
    /// for, and must not look up the declined subject again in other words. Small changes, such as
    /// "do not call this tool again", make smaller models give up the rest of the task.
    /// </remarks>
    public const string DECLINED_BY_USER = "The user declined this tool call, so it did not run. Do not look up what it asked for again, also not in other words. Everything else the user asked for is still wanted: if parts of the request are still open, continue with them now and call the tools they need. Only when nothing is left, answer, and tell the user what is missing because of the declined call.";

    /// <summary>
    /// What the model reads instead of a result when nobody could confirm a call.
    /// </summary>
    public const string NOBODY_TO_CONFIRM = "This tool call needs the confirmation of the user, but the user cannot confirm calls in this context, so it did not run. Do not call this tool again, since it cannot run here. If parts of the user request are still open, continue with them now, with the other tools where they help. Only when nothing is left, answer, and tell the user what is missing because of this.";

    /// <summary>
    /// Whether the user has to confirm this call before it runs.
    /// </summary>
    /// <param name="mode">How much the user wants to be asked.</param>
    /// <param name="implementation">The tool.</param>
    /// <param name="arguments">The arguments the model passed.</param>
    /// <param name="chatThread">The chat the call belongs to.</param>
    /// <returns>True when the user has to confirm the call.</returns>
    public static bool RequiresApproval(ToolApprovalMode mode, IToolImplementation implementation, JsonElement arguments, ChatThread chatThread) => mode switch
    {
        ToolApprovalMode.ASK_FOR_EVERY_TOOL => true,
        ToolApprovalMode.NEVER_ASK => false,
        _ => implementation.RequiresApproval(arguments, chatThread),
    };

    /// <summary>
    /// Whether calls of a tool with this kind of outbound data are critical, unless the tool knows better for a call.
    /// </summary>
    /// <param name="outboundData">Where the arguments of the tool go.</param>
    /// <returns>True when its calls need confirmation.</returns>
    public static bool RequiresApprovalByDefault(ToolOutboundData outboundData) => outboundData is ToolOutboundData.THIRD_PARTY_QUERIES or ToolOutboundData.MODEL_CHOSEN_ADDRESSES;

    /// <summary>
    /// Whether some calls of this tool may need confirmation.
    /// </summary>
    /// <remarks>
    /// For a run which cannot ask for each call and asks once beforehand instead, see
    /// AssistantBase.ApproveToolsForPrivateRunAsync.
    /// </remarks>
    public static bool MayRequireApproval(ToolApprovalMode mode, IToolImplementation implementation) => mode switch
    {
        ToolApprovalMode.ASK_FOR_EVERY_TOOL => true,
        ToolApprovalMode.NEVER_ASK => false,
        _ => RequiresApprovalByDefault(implementation.OutboundData),
    };

    /// <summary>
    /// How much the user wants to be asked, see DataTools.ToolApprovalMode.
    /// </summary>
    public static ToolApprovalMode GetMode(SettingsManager settingsManager) => settingsManager.ConfigurationData.Tools.ToolApprovalMode;

    /// <summary>
    /// Whether the user may allow a tool for a whole chat, or for a whole run of an assistant.
    /// </summary>
    public static bool MayApproveForWholeChat(SettingsManager settingsManager) => settingsManager.ConfigurationData.Tools.AllowApprovingToolsForWholeChat;
}