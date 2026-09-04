using System.Text;
using System.Text.Json;

namespace AIStudio.Provider.OpenAI;

internal static class ChatCompletionContent
{
    public static string? GetText(JsonElement? content)
    {
        if (content is not { } value)
            return null;

        if (value.ValueKind is JsonValueKind.String)
            return value.GetString();

        if (value.ValueKind is not JsonValueKind.Array)
            return null;

        var text = new StringBuilder();
        foreach (var chunk in value.EnumerateArray())
        {
            if (chunk.ValueKind is JsonValueKind.String)
            {
                text.Append(chunk.GetString());
                continue;
            }

            if (chunk.ValueKind is not JsonValueKind.Object ||
                !chunk.TryGetProperty("type", out var type) ||
                type.ValueKind is not JsonValueKind.String ||
                !string.Equals(type.GetString(), "text", StringComparison.Ordinal) ||
                !chunk.TryGetProperty("text", out var textElement) ||
                textElement.ValueKind is not JsonValueKind.String)
                continue;

            text.Append(textElement.GetString());
        }

        return text.Length == 0 ? null : text.ToString();
    }
}
