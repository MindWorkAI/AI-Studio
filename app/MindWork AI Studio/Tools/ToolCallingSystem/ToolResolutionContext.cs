using AIStudio.Chat;
using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// The request a tool is being prepared for.
/// </summary>
/// <remarks>
/// What a tool may look at when it tailors its function to a request, see
/// IToolImplementation.ResolveFunctionAsync. Semantic Search, for instance, reads the data sources
/// of the chat and describes exactly those which this provider may search.
/// </remarks>
public sealed class ToolResolutionContext
{
    /// <summary>
    /// The provider the request goes to, with the expert settings of the user.
    /// </summary>
    public required AIStudio.Settings.Provider Provider { get; init; }

    /// <summary>
    /// The part of the app the request comes from.
    /// </summary>
    public required Components Component { get; init; }

    /// <summary>
    /// How much the provider is trusted.
    /// </summary>
    public required ConfidenceLevel ProviderConfidence { get; init; }

    /// <summary>
    /// The chat the request continues.
    /// </summary>
    public required ChatThread ChatThread { get; init; }

    /// <summary>
    /// The tools which passed every check for this request and are asked what they offer, or null
    /// when whoever asks does not know them.
    /// </summary>
    /// <remarks>
    /// Lets a tool word its instructions after the other tools of the request: a rule about a tool
    /// the model does not get would only confuse it. Filled by ToolRegistry.GetRunnableToolsAsync.
    /// A candidate may still offer nothing in the end, see IToolImplementation.ResolveFunctionAsync.
    /// </remarks>
    public IReadOnlySet<string>? CandidateToolIds { get; init; }

    /// <summary>
    /// Whether this request may offer the given tool as well.
    /// </summary>
    /// <remarks>
    /// Without the candidates, every tool may be there, so a rule about it is kept rather than lost.
    /// </remarks>
    /// <param name="toolId">The ID of the tool.</param>
    /// <returns>True when the tool is a candidate of this request, or when the candidates are unknown.</returns>
    public bool MayOffer(string toolId) => this.CandidateToolIds?.Contains(toolId) ?? true;
}