namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// One tool call a model requested.
/// </summary>
/// <remarks>
/// Invalid calls are carried through rather than dropped: the model has to learn that its call
/// was rejected, otherwise it waits for a result that never arrives.
/// </remarks>
/// <param name="CallId">
/// The ID correlating this call with its result. Empty when the provider did not supply one and
/// the adapter cannot invent one, which makes the call unanswerable.
/// </param>
/// <param name="ToolName">The name of the tool the model asked for.</param>
/// <param name="ArgumentsJson">The arguments as the model wrote them, to be treated as untrusted input.</param>
/// <param name="IsValid">Whether name and arguments are usable at all.</param>
public sealed record ToolCallingRequestedCall(string CallId, string ToolName, string ArgumentsJson, bool IsValid);