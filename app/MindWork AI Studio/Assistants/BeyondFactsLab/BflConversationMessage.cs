namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// A message of a conversation in the lab as the person saw it, e.g., for the appendix of the reflection folder.
/// </summary>
/// <param name="IsFromPerson">Whether the person wrote the message; otherwise, the lab did.</param>
/// <param name="Text">The text of the message, in Markdown.</param>
public sealed record BflConversationMessage(bool IsFromPerson, string Text);