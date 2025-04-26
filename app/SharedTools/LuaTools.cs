namespace SharedTools;

public static class LuaTools
{
    public static string EscapeLuaString(string value)
    {
        // Replace backslashes with double backslashes and escape double quotes:
        return value.Replace("\\", @"\\").Replace("\"", "\\\"");
    }
}