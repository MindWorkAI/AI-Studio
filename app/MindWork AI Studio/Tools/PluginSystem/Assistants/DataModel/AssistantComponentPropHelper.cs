using AIStudio.Tools.PluginSystem.Assistants.Icons;

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

    public static int ReadInt(Dictionary<string, object> props, string key, int fallback = 0)
    {
        return props.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var i) ? i : fallback;
    }
    
    public static void WriteInt(Dictionary<string, object> props, string key, int value)
    {
        props[key] = value;
    }
    
    public static int? ReadNullableInt(Dictionary<string, object> props, string key)
    {
        return props.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var i) ? i : null;
    }

    public static void WriteNullableInt(Dictionary<string, object> props, string key, int? value)
    {
        if (value.HasValue)
            props[key] = value.Value;
        else
            props.Remove(key);
    }

    public static bool ReadBool(Dictionary<string, object> props, string key, bool fallback = false)
    {
        return props.TryGetValue(key, out var value) && bool.TryParse(value.ToString(), out var b) ? b : fallback;
    }

    public static void WriteBool(Dictionary<string, object> props, string key, bool value)
    {
        props[key] = value;
    }

    public static void WriteObject(Dictionary<string, object> props, string key, object? value)
    {
        if (value is null)
            props.Remove(key);
        else
            props[key] = value;
    }
    
    public static MudBlazor.Color GetColor(string value, Color fallback) => Enum.TryParse<MudBlazor.Color>(value, out var color) ? color : fallback;
    public static string GetIconSvg(string value) => MudBlazorIconRegistry.TryGetSvg(value, out var svg) ? svg : string.Empty;
    public static Size GetComponentSize(string value, Size fallback) => Enum.TryParse<Size>(value, out var size) ? size : fallback;
}
