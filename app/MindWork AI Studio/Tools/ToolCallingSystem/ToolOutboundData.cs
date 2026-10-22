namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// Where a tool sends data when it runs, beyond AI Studio and the provider of the model.
/// </summary>
/// <remarks>
/// What counts is where the arguments of the model go: every argument may carry content the chat
/// read before, a mail for instance. A chat which read from a mailbox keeps the tools whose data
/// goes too far from running, see OutboundDataRestriction and ToolSelectionRules.IsOutboundDataAllowed.
/// The members run from the most contained to the most open.
/// </remarks>
public enum ToolOutboundData
{
    /// <summary>
    /// Nothing leaves AI Studio.
    /// </summary>
    NONE,

    /// <summary>
    /// Only services configured in AI Studio get the data, such as the wiki of the organization,
    /// an ERI server, or the embedding provider of a data source.
    /// </summary>
    CONFIGURED_SERVICE,

    /// <summary>
    /// A service somebody else runs gets queries the model writes, such as a web search engine.
    /// </summary>
    THIRD_PARTY_QUERIES,

    /// <summary>
    /// The tool contacts addresses the model chooses, such as the web page it wants to read. The
    /// address alone can carry data out.
    /// </summary>
    MODEL_CHOSEN_ADDRESSES,
}