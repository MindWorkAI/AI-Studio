using System.Globalization;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantTimePicker : StatefulAssistantComponentBase
{
    private static readonly CultureInfo INVARIANT_CULTURE = CultureInfo.InvariantCulture;
    private static readonly string[] FALLBACK_TIME_FORMATS = ["HH:mm", "HH:mm:ss", "hh:mm tt", "h:mm tt"];

    public override AssistantComponentType Type => AssistantComponentType.TIME_PICKER;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Label
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Label));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Label), value);
    }

    public string Value
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Value));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Value), value);
    }

    public string Placeholder
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Placeholder));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Placeholder), value);
    }

    public string HelperText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HelperText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HelperText), value);
    }
    
    public string Color
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Color));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Color), value);
    }

    public string TimeFormat
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.TimeFormat));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.TimeFormat), value);
    }

    public bool AmPm
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.AmPm));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.AmPm), value);
    }

    public string PickerVariant
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.PickerVariant));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.PickerVariant), value);
    }
    
    public int Elevation
    {
        get => AssistantComponentPropHelper.ReadInt(this.Props, nameof(this.Elevation), 6);
        set => AssistantComponentPropHelper.WriteInt(this.Props, nameof(this.Elevation), value);
    }

    public string Class
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Class));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Class), value);
    }

    public string Style
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Style));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Style), value);
    }

    #region Implementation of IStatefulAssistantComponent

    public override void InitializeState(AssistantState state)
    {
        if (!state.Times.ContainsKey(this.Name))
            state.Times[this.Name] = this.Value;
    }

    public override string UserPromptFallback(AssistantState state)
    {
        state.Times.TryGetValue(this.Name, out var userInput);
        return this.BuildAuditPromptBlock(userInput);
    }

    #endregion

    public string GetTimeFormat()
    {
        if (!string.IsNullOrWhiteSpace(this.TimeFormat))
            return this.TimeFormat;

        return this.AmPm ? "hh:mm tt" : "HH:mm";
    }

    public TimeSpan? ParseValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return TryParseTime(value, this.GetTimeFormat(), out var parsedTime) ? parsedTime : null;
    }

    public string FormatValue(TimeSpan? value) => value.HasValue ? FormatTime(value.Value, this.GetTimeFormat()) : string.Empty;

    private static bool TryParseTime(string value, string? format, out TimeSpan parsedTime)
    {
        if ((!string.IsNullOrWhiteSpace(format) &&
             DateTime.TryParseExact(value, format, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out var dateTime)) ||
            DateTime.TryParseExact(value, FALLBACK_TIME_FORMATS, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out dateTime))
        {
            parsedTime = dateTime.TimeOfDay;
            return true;
        }

        if (TimeSpan.TryParse(value, INVARIANT_CULTURE, out parsedTime))
            return true;

        parsedTime = TimeSpan.Zero;
        return false;
    }

    private static string FormatTime(TimeSpan value, string? format)
    {
        var dateTime = DateTime.Today.Add(value);

        try
        {
            return dateTime.ToString(string.IsNullOrWhiteSpace(format) ? FALLBACK_TIME_FORMATS[0] : format, INVARIANT_CULTURE);
        }
        catch (FormatException)
        {
            return dateTime.ToString(FALLBACK_TIME_FORMATS[0], INVARIANT_CULTURE);
        }
    }
}
