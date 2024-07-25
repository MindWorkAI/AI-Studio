namespace AIStudio.Provider.Fireworks;

/// <summary>
/// Data model for a line in the response stream, for streaming completions.
/// </summary>
/// <param name="Id">The id of the response.</param>
/// <param name="Object">The object describing the response.</param>
/// <param name="Created">The timestamp of the response.</param>
/// <param name="Model">The model used for the response.</param>
/// <param name="Choices">The choices made by the AI.</param>
public readonly record struct ResponseStreamLine(string Id, string Object, uint Created, string Model, IList<Choice> Choices);

/// <summary>
/// Data model for a choice made by the AI.
/// </summary>
/// <param name="Index">The index of the choice.</param>
/// <param name="Delta">The delta text of the choice.</param>
public readonly record struct Choice(int Index, Delta Delta);

/// <summary>
/// The delta text of a choice.
/// </summary>
/// <param name="Content">The content of the delta text.</param>
public readonly record struct Delta(string Content);