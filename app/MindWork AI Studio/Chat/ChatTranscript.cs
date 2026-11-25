using System.Text;

namespace AIStudio.Chat;

/// <summary>
/// Writes what the user sees of a chat as plain text, for a model which reads the chat, such as a judge.
/// </summary>
/// <remarks>
/// Only the messages the user sees count: hidden requests, the results of tools, and the content of attached files
/// stay out. A call of a tool is one line with its name and its arguments, each shortened, so that a reader learns what
/// the model looked up without reading everything it got back. The thinking of a model stays out as well.
/// </remarks>
public static class ChatTranscript
{
    /// <summary>
    /// How many characters of each argument of a tool call the transcript keeps.
    /// </summary>
    public const int MAX_ARGUMENT_LENGTH = 80;

    /// <summary>
    /// Writes the transcript of a chat.
    /// </summary>
    /// <param name="thread">The chat.</param>
    /// <returns>The messages in their order, each below a line naming who wrote it.</returns>
    public static string Of(ChatThread thread)
    {
        var transcript = new StringBuilder();
        foreach (var block in thread.Blocks)
        {
            if (block is not { HideFromUser: false, Content: ContentText content })
                continue;

            var message = Message(block.Role, content);
            if (message.Length == 0)
                continue;

            if (transcript.Length > 0)
                transcript.AppendLineLF();

            transcript.AppendLineLF(block.Role is ChatRole.USER ? "[User]" : "[Assistant]");
            transcript.AppendLineLF(message);
        }

        return transcript.ToString().TrimEnd();
    }

    /// <summary>
    /// Writes one message: the calls of tools first, since the model made them before it answered, then the text, then
    /// the names of attached files.
    /// </summary>
    private static string Message(ChatRole role, ContentText content)
    {
        var lines = new List<string>();
        if (role is ChatRole.AI)
            lines.AddRange(content.ToolInvocations.OrderBy(call => call.Order).Select(call => $"[Tool call: {call.ToolName}({Arguments(call.Arguments)})]"));

        var text = role is ChatRole.AI ? content.Text.RemoveThinkTags().Trim() : content.Text.Trim();
        if (text.Length > 0)
            lines.Add(text);

        if (content.FileAttachments.Count > 0)
            lines.Add($"[Attached files: {string.Join(", ", content.FileAttachments.Select(attachment => attachment.FileName))}]");

        return string.Join('\n', lines);
    }

    private static string Arguments(IReadOnlyDictionary<string, string> arguments) => string.Join(", ", arguments.Select(argument => $"{argument.Key}={Shortened(argument.Value)}"));

    private static string Shortened(string value)
    {
        var singleLine = value.ReplaceLineEndings(" ").Trim();
        if (singleLine.Length <= MAX_ARGUMENT_LENGTH)
            return singleLine;

        // Cutting between the two halves of a character outside the basic plane would leave half of it:
        var length = char.IsHighSurrogate(singleLine[MAX_ARGUMENT_LENGTH - 1]) ? MAX_ARGUMENT_LENGTH - 1 : MAX_ARGUMENT_LENGTH;
        return singleLine[..length] + "…";
    }
}