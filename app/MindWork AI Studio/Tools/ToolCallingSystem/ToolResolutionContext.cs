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
}