namespace AIStudio.Provider.Mistral;

/// <summary>
/// Regulat chat message model.
/// </summary>
/// <param name="Content">The text content of the message.</param>
/// <param name="Role">The role of the message.</param>
public readonly record struct RegularMessage(string Content, string Role);