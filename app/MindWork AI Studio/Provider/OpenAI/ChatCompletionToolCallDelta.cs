namespace AIStudio.Provider.OpenAI;

/// <summary>
/// One fragment of a tool call in a streamed Chat Completions answer.
/// </summary>
/// <remarks>
/// A tool call arrives in pieces: the ID and the name usually with the first fragment, the
/// arguments spread over as many as the model needs. The index is what ties the pieces of one
/// call together while another call is being written at the same time.
/// </remarks>
/// <param name="Index">Which call this fragment belongs to; null when the provider omits it.</param>
/// <param name="Id">The ID of the call, sent once by most providers and repeated by some.</param>
/// <param name="Type">The kind of call, which is "function" for everything we offer.</param>
/// <param name="Function">The name and the arguments fragment of the call.</param>
public sealed record ChatCompletionToolCallDelta(int? Index, string? Id, string? Type, ChatCompletionToolFunction? Function);