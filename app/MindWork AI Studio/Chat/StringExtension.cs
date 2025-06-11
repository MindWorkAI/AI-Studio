namespace AIStudio.Chat;

public static class StringExtensions
{
    public static string RemoveThinkTags(this string input)
    {
        const string OPEN_TAG = "<think>";
        const string CLOSE_TAG = "</think>";
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith(OPEN_TAG, StringComparison.Ordinal))
            return input;
        
        var endIndex = input.IndexOf(CLOSE_TAG, StringComparison.Ordinal);
        if (endIndex == -1)
            return string.Empty;
        
        return input[(endIndex + CLOSE_TAG.Length)..];
    }
}