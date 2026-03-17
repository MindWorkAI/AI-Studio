namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantDatePicker : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.DATE_PICKER;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }

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

    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
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

    public string GetDateFormat() => string.IsNullOrWhiteSpace(this.DateFormat) ? "yyyy-MM-dd" : this.DateFormat;
}
