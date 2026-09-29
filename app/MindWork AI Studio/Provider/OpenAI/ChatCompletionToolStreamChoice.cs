namespace AIStudio.Provider.OpenAI;

/// <summary>
/// One choice of a streamed Chat Completions answer, as the tool calling rounds read it.
/// </summary>
/// <param name="Index">The index of the choice; we only ever work with the first one.</param>
/// <param name="Delta">What this line adds to the choice.</param>
/// <param name="FinishReason">Why the model stopped, set on the last line of the choice.</param>
public sealed record ChatCompletionToolStreamChoice(int Index, ChatCompletionStreamDelta? Delta, string? FinishReason);