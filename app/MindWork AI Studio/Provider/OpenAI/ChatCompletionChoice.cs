namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Data model for a choice made by the AI.
/// </summary>
/// <param name="Index">The index of the choice.</param>
/// <param name="Delta">The delta text of the choice.</param>
public record ChatCompletionChoice(int Index, ChatCompletionDelta Delta)
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters:
    //
    public int Index { get; set; } = Index;
    public ChatCompletionDelta Delta { get; set; } = Delta;

    public ChatCompletionChoice() : this(0, new())
    {
    }
}
