namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal static class AssistantComponentPropHelper
{
    public static string ReadString(Dictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    public static void WriteString(Dictionary<string, object> props, string key, string value)
    {
        props[key] = value ?? string.Empty;
    }
}
