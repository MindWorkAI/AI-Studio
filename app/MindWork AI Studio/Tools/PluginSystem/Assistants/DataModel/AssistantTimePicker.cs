namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantTimePicker : StatefulAssistantComponentBase
{
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
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.AmPm), false);
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
        var userInput = string.Empty;
        
        var promptFragment = $"context:{Environment.NewLine}{this.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
        if (state.Times.TryGetValue(this.Name, out userInput) && !string.IsNullOrWhiteSpace(userInput))
            promptFragment += $"user prompt:{Environment.NewLine}{userInput}";

        return promptFragment;
    }

    #endregion

    public string GetTimeFormat()
    {
        if (!string.IsNullOrWhiteSpace(this.TimeFormat))
            return this.TimeFormat;

        return this.AmPm ? "hh:mm tt" : "HH:mm";
    }
}
