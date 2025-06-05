namespace AIStudio.Chat;

public static class StringExtensions
{
    public static string RemoveThinkTags(this string input)
    {
        if (string.IsNullOrEmpty(input) || !input.StartsWith("<think>"))
            return input;
        
        int endIndex = input.IndexOf("</think>");
        if (endIndex == -1)
            return input;
        
        return input.Substring(endIndex + "</think>".Length);
    }
}