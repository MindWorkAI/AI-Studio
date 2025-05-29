using System.Text;

namespace SharedTools;

public static class LuaTable
{
    public static string Create(ref StringBuilder sb, string tableVariableName, IReadOnlyDictionary<string, string> keyValuePairs, IReadOnlyDictionary<string, string>? commentContent = null, CancellationToken cancellationToken = default)
    {
        //
        // Add the UI_TEXT_CONTENT table:
        //
        sb.AppendLine($$"""{{tableVariableName}} = {}""");
        foreach (var kvp in keyValuePairs.OrderBy(x => x.Key))
        {
            if (cancellationToken.IsCancellationRequested)
                return sb.ToString();
            
            var key = kvp.Key;
            var value = kvp.Value.Replace("\n", " ").Trim();
            var commentValue = commentContent is null ? value : commentContent.GetValueOrDefault(key, value);

            // Remove the "UI_TEXT_CONTENT." prefix from the key:
            const string UI_TEXT_CONTENT = "UI_TEXT_CONTENT.";
            var keyWithoutPrefix = key.StartsWith(UI_TEXT_CONTENT, StringComparison.OrdinalIgnoreCase) ? key[UI_TEXT_CONTENT.Length..] : key;

            // Replace all dots in the key with colons:
            keyWithoutPrefix = keyWithoutPrefix.Replace(".", "::");
            
            // Add a comment with the original text content:
            sb.AppendLine();
            sb.AppendLine($"-- {commentValue}");
            
            // Add the assignment to the UI_TEXT_CONTENT table:
            sb.AppendLine($"""
                           UI_TEXT_CONTENT["{keyWithoutPrefix}"] = "{LuaTools.EscapeLuaString(value)}"
                           """);
        }
        
        return sb.ToString();
    }
}