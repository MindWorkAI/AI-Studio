namespace AIStudio.Dialogs;

/// <summary>
/// What the dialog for compressing a chat hands back to the chat.
/// </summary>
/// <param name="Name">The name of the new chat.</param>
/// <param name="Summary">The summary the AI wrote, not filtered for prompt injections yet.</param>
public sealed record ChatCompressionDialogResult(string Name, string Summary);