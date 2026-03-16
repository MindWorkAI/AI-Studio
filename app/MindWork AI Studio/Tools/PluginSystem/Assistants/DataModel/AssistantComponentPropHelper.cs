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
    public static MudBlazor.Variant GetVariant(string value, Variant fallback) => Enum.TryParse<MudBlazor.Variant>(value, out var variant) ? variant : fallback;
    public static MudBlazor.Adornment GetAdornment(string value, Adornment fallback) => Enum.TryParse<MudBlazor.Adornment>(value, out var adornment) ? adornment : fallback;
    public static string GetIconSvg(string value) => MudBlazorIconRegistry.TryGetSvg(value, out var svg) ? svg : string.Empty;
    public static Size GetComponentSize(string value, Size fallback) => Enum.TryParse<Size>(value, out var size) ? size : fallback;
    public static Justify? GetJustify(string value) => Enum.TryParse<Justify>(value, out var justify) ? justify : null;
    public static AlignItems? GetItemsAlignment(string value) => Enum.TryParse<AlignItems>(value, out var alignment) ? alignment : null;
    public static Align GetAlignment(string value, Align fallback = Align.Inherit) => Enum.TryParse<Align>(value, out var alignment) ? alignment : fallback;
    public static Typo GetTypography(string value, Typo fallback = Typo.body1) => Enum.TryParse<Typo>(value, out var typo) ? typo : fallback;
    public static Wrap? GetWrap(string value) => Enum.TryParse<Wrap>(value, out var wrap) ? wrap : null;
    public static StretchItems? GetStretching(string value) => Enum.TryParse<StretchItems>(value, out var stretch) ? stretch : null;
    public static Breakpoint GetBreakpoint(string value, Breakpoint fallback) => Enum.TryParse<Breakpoint>(value, out var breakpoint) ? breakpoint : fallback;
    public static PickerVariant GetPickerVariant(string pickerValue, PickerVariant fallback) => Enum.TryParse<PickerVariant>(pickerValue, out var variant) ? variant : fallback;
}
