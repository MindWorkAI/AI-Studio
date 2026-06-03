using System.Globalization;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantDateRangePicker : StatefulAssistantComponentBase
{
    private static readonly CultureInfo INVARIANT_CULTURE = CultureInfo.InvariantCulture;
    private static readonly string[] FALLBACK_DATE_FORMATS = ["dd.MM.yyyy", "yyyy-MM-dd" , "MM/dd/yyyy"];

    public override AssistantComponentType Type => AssistantComponentType.DATE_RANGE_PICKER;
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
    
    public string Color
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Color));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Color), value);
    }

    public string PlaceholderStart
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.PlaceholderStart));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.PlaceholderStart), value);
    }

    public string PlaceholderEnd
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.PlaceholderEnd));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.PlaceholderEnd), value);
    }

    public string HelperText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HelperText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HelperText), value);
    }

    public string DateFormat
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.DateFormat));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.DateFormat), value);
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
        if (!state.DateRanges.ContainsKey(this.Name))
            state.DateRanges[this.Name] = this.Value;
    }

    public override string UserPromptFallback(AssistantState state)
    {
        state.DateRanges.TryGetValue(this.Name, out var userInput);
        return this.BuildAuditPromptBlock(userInput);
    }

    #endregion

    public string GetDateFormat() => string.IsNullOrWhiteSpace(this.DateFormat) ? "yyyy-MM-dd" : this.DateFormat;

    public DateRange? ParseValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var format = this.GetDateFormat();
        var parts = value.Split(" - ", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return null;

        if (!TryParseDate(parts[0], format, out var start) || !TryParseDate(parts[1], format, out var end))
            return null;

        return new DateRange(start, end);
    }

    public string FormatValue(DateRange? value)
    {
        if (value?.Start is null || value.End is null)
            return string.Empty;

        var format = this.GetDateFormat();
        return $"{FormatDate(value.Start.Value, format)} - {FormatDate(value.End.Value, format)}";
    }

    private static bool TryParseDate(string value, string? format, out DateTime parsedDate)
    {
        if (!string.IsNullOrWhiteSpace(format) &&
            DateTime.TryParseExact(value, format, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out parsedDate))
        {
            return true;
        }

        return DateTime.TryParseExact(value, FALLBACK_DATE_FORMATS, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out parsedDate) ||
               DateTime.TryParse(value, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out parsedDate);
    }

    private static string FormatDate(DateTime value, string? format)
    {
        try
        {
            return value.ToString(string.IsNullOrWhiteSpace(format) ? FALLBACK_DATE_FORMATS[0] : format, INVARIANT_CULTURE);
        }
        catch (FormatException)
        {
            return value.ToString(FALLBACK_DATE_FORMATS[0], INVARIANT_CULTURE);
        }
    }
}
