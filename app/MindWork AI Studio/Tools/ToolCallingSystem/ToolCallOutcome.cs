using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// What one tool call leaves behind: the text for the model, the trace for the user, and what the
/// chat has to keep from now on.
/// </summary>
/// <remarks>
/// A call which was invalid, blocked or failed brought nothing in. It demands nothing of the chat
/// and contributes no sources, which is what the defaults say.
/// </remarks>
/// <param name="Content">What the model reads as the result of the call.</param>
/// <param name="Trace">What the user sees of the call.</param>
public sealed record ToolCallOutcome(string Content, ToolInvocationTrace Trace)
{
    /// <summary>
    /// The confidence every provider which continues the chat has to meet, see ChatThread.RequireProviderConfidence.
    /// </summary>
    public ConfidenceLevel RequiredProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The data security the chat has to keep, see ChatThread.RequireDataSecurity.
    /// </summary>
    public DataSourceSecurity RequiredDataSecurity { get; init; } = DataSourceSecurity.NOT_SPECIFIED;

    /// <summary>
    /// Where the chat may still send data, see ChatThread.RequireOutboundDataRestriction.
    /// </summary>
    public OutboundDataRequirement RequiredOutboundDataRestriction { get; init; } = OutboundDataRequirement.NONE;

    /// <summary>
    /// The sources the result contributes to the answer.
    /// </summary>
    public IReadOnlyList<Source> Sources { get; init; } = [];

    /// <summary>
    /// The web addresses in the result, as request keys, except those the model wrote into the call.
    /// </summary>
    /// <remarks>
    /// A chat restricted by a mailbox may read the web pages whose addresses a tool returned, see
    /// ChatThread.IsWebAddressGivenToTheModel. A tool which repeats its arguments would otherwise
    /// turn any address the model makes up into one a tool returned: Semantic Search, for one,
    /// returns its query. Found where the arguments are still at hand in full, because the trace
    /// hides the sensitive ones.
    /// </remarks>
    public IReadOnlyCollection<string> ReturnedWebAddresses { get; init; } = [];
}