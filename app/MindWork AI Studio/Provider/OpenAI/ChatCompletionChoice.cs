namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data model for a choice made by the AI.
/// </summary>
/// <param name="Index">The index of the choice.</param>
/// <param name="Delta">The delta text of the choice.</param>
public record ChatCompletionChoice(int Index, ChatCompletionDelta Delta)
{
    public ChatCompletionChoice() : this(0, new (string.Empty))
    {
    }
}