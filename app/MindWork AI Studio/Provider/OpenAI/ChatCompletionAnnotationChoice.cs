namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data structure representing a choice in a chat completion annotation response.
/// </summary>
/// <param name="Index">The index of the choice.</param>
/// <param name="Delta">The delta information for the choice.</param>
public record ChatCompletionAnnotationChoice(int Index, ChatCompletionAnnotationDelta Delta);