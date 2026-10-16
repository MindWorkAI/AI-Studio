namespace SharedTools;

public static class LuaTools
{
    public static string EscapeLuaString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Replace backslashes with double backslashes and escape double quotes:
        return value
            .Replace("\\", @"\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    public static string ToLuaStringLiteral(string? value, bool forceLongString = false, int longStringLengthThreshold = 80)
    {
        value ??= string.Empty;

        //
        // Lua reads every kind of line break in a long string as \n. A carriage return therefore
        // survives only in a quoted string, where it is escaped, even when a long string was asked for:
        //
        if (value.Contains('\r') || (!forceLongString &&
                                     value.Length <= longStringLengthThreshold &&
                                     !value.Contains('\n')))
            return $"\"{EscapeLuaString(value)}\"";

        //
        // Lua also drops a line break right after the opening bracket, so a text which starts with
        // one gets another one in front:
        //
        var skippedLineBreak = value.StartsWith('\n') ? "\n" : string.Empty;
        return $"{CreateLongStringOpeningDelimiter(value)}{skippedLineBreak}{value}{CreateLongStringClosingDelimiter(value)}";
    }

    private static string CreateLongStringOpeningDelimiter(string value)
    {
        var equals = CreateLongStringEquals(value);
        return $"[{equals}[";
    }

    private static string CreateLongStringClosingDelimiter(string value)
    {
        var equals = CreateLongStringEquals(value);
        return $"]{equals}]";
    }

    private static string CreateLongStringEquals(string value)
    {
        //
        // The closing bracket must neither occur in the text nor be completed by its end. A text
        // which ends with "]===" would otherwise form "]===]" with the first character of the
        // closing bracket, and Lua ends the string right there:
        //
        var equalsCount = 3;
        while (value.Contains($"]{new string('=', equalsCount)}]") || value.EndsWith($"]{new string('=', equalsCount)}", StringComparison.Ordinal))
            equalsCount++;

        return new string('=', equalsCount);
    }
}