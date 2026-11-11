using System.Text;

using SharedTools.Text;

namespace SharedTools;

public static class LuaTable
{
    public static string Create(ref StringBuilder sb, string tableVariableName, IReadOnlyDictionary<string, string> keyValuePairs, IReadOnlyDictionary<string, string>? commentContent = null, CancellationToken cancellationToken = default)
    {
        //
        // Add the UI_TEXT_CONTENT table:
        //
        sb.AppendLineLF($$"""{{tableVariableName}} = {}""");
        foreach (var kvp in keyValuePairs.OrderBy(x => x.Key))
        {
            if (cancellationToken.IsCancellationRequested)
                return sb.ToString();
            
            //
            // A text with several lines, such as a notice in Markdown, keeps its line breaks, which the
            // assignment below escapes. Lua ends a comment at the end of its line, though, so the comment
            // gets the text on one line:
            //
            var key = kvp.Key;
            var value = I18NKeyHash.NormalizeLineEndings(kvp.Value).Trim();
            var commentValue = I18NKeyHash.NormalizeLineEndings(commentContent is null ? value : commentContent.GetValueOrDefault(key, value)).Replace("\n", " ");

            // Remove the "UI_TEXT_CONTENT." prefix from the key:
            const string UI_TEXT_CONTENT = "UI_TEXT_CONTENT.";
            var keyWithoutPrefix = key.StartsWith(UI_TEXT_CONTENT, StringComparison.OrdinalIgnoreCase) ? key[UI_TEXT_CONTENT.Length..] : key;

            // Replace all dots in the key with colons:
            keyWithoutPrefix = keyWithoutPrefix.Replace(".", "::");
            
            // Add a comment with the original text content:
            sb.AppendLineLF();
            sb.AppendLineLF($"-- {commentValue}");
            
            // Add the assignment to the UI_TEXT_CONTENT table:
            sb.AppendLineLF($"""
                           UI_TEXT_CONTENT["{keyWithoutPrefix}"] = "{LuaTools.EscapeLuaString(value)}"
                           """);
        }
        
        return sb.ToString();
    }
}