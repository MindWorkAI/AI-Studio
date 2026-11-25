using System.Globalization;

using AIStudio.Tools.PluginSystem.Assistants.Icons;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal static class AssistantComponentPropHelper
{
    /// <summary>
    /// The prefix with which an assistant plugin names a Lucide icon.
    /// </summary>
    private const string LUCIDE_PREFIX = "lucide:";

    /// <summary>
    /// Reads a prop as text. A number, which Lua hands over as a double, becomes the same text on every computer,
    /// the one Lua itself writes: 12.5 stays "12.5", also where the region settings use a decimal comma.
    /// </summary>
    public static string ReadString(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return string.Empty;

        if (value is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    public static void WriteString(Dictionary<string, object> props, string key, string value) => props[key] = value;

    public static int ReadInt(Dictionary<string, object> props, string key, int fallback = 0) => TryReadInt(props, key, out var number) ? number : fallback;

    public static void WriteInt(Dictionary<string, object> props, string key, int value) => props[key] = value;

    public static int? ReadNullableInt(Dictionary<string, object> props, string key) => TryReadInt(props, key, out var number) ? number : null;

    /// <summary>
    /// Reads a whole number, independent of the region settings of the computer.
    /// </summary>
    /// <remarks>
    /// Lua hands over every number as a double, and the setters of the components store an int. A plugin may also
    /// write the number as text. A number with a fraction, or one beyond the range of an int, counts as missing.
    /// </remarks>
    private static bool TryReadInt(Dictionary<string, object> props, string key, out int number)
    {
        number = 0;
        if (!props.TryGetValue(key, out var value))
            return false;

        switch (value)
        {
            case int whole:
                number = whole;
                return true;

            case double real when double.IsInteger(real) && real is >= int.MinValue and <= int.MaxValue:
                number = (int)real;
                return true;

            case string text:
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

            default:
                return false;
        }
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

    public static void WriteBool(Dictionary<string, object> props, string key, bool value) => props[key] = value;

    public static void WriteObject(Dictionary<string, object> props, string key, object? value)
    {
        if (value is null)
            props.Remove(key);
        else
            props[key] = value;
    }
    
    public static Color GetColor(string value, Color fallback) => Enum.TryParse<Color>(value, out var color) ? color : fallback;
    public static Variant GetVariant(string value, Variant fallback) => Enum.TryParse<Variant>(value, out var variant) ? variant : fallback;
    public static Adornment GetAdornment(string value, Adornment fallback) => Enum.TryParse<Adornment>(value, out var adornment) ? adornment : fallback;
    /// <summary>
    /// Finds the icon an assistant plugin names: a Lucide icon as "lucide:" followed by its name on lucide.dev,
    /// such as "lucide:house", or an icon of MudBlazor, such as "Icons.Material.Filled.Home".
    /// </summary>
    /// <param name="value">The icon as the plugin names it.</param>
    /// <returns>The icon in the form MudBlazor renders, or an empty string for an unknown name.</returns>
    public static string GetIcon(string value)
    {
        var name = value.TrimStart('@');
        if (name.StartsWith(LUCIDE_PREFIX, StringComparison.Ordinal))
            return LucideIconRegistry.TryGet(name[LUCIDE_PREFIX.Length..], out var lucideIcon) ? lucideIcon : string.Empty;

        return MudBlazorIconRegistry.TryGetSvg(name, out var svg) ? svg : string.Empty;
    }


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
