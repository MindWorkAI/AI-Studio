namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Chat message model.
/// </summary>
/// <param name="Content">The text content of the message.</param>
/// <param name="Role">The role of the message.</param>
public readonly record struct Message(string Content, string Role);