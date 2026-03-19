namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantDatePicker : StatefulAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.DATE_PICKER;
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
        if (!state.Dates.ContainsKey(this.Name))
            state.Dates[this.Name] = this.Value;
    }

    public override string UserPromptFallback(AssistantState state)
    {
        var userInput = string.Empty;
        
        var promptFragment = $"context:{Environment.NewLine}{this.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
        if (state.Dates.TryGetValue(this.Name, out userInput) && !string.IsNullOrWhiteSpace(userInput))
            promptFragment += $"user prompt:{Environment.NewLine}{userInput}";

        return promptFragment;
    }

    #endregion

    public string GetDateFormat() => string.IsNullOrWhiteSpace(this.DateFormat) ? "yyyy-MM-dd" : this.DateFormat;
}
