namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantColorPicker : StatefulAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.COLOR_PICKER;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Label
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Label));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Label), value);
    }
    
    public string Placeholder
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Placeholder));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Placeholder), value);
    }
    
    public bool ShowAlpha
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.ShowAlpha), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.ShowAlpha), value);
    }
    
    public bool ShowToolbar
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.ShowToolbar), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.ShowToolbar), value);
    }
    
    public bool ShowModeSwitch
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.ShowModeSwitch), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.ShowModeSwitch), value);
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

    #region Implementation of IStatefuleAssistantComponent

    public override void InitializeState(AssistantState state)
    {
        if (!state.Colors.ContainsKey(this.Name))
            state.Colors[this.Name] = this.Placeholder;
    }

    public override string UserPromptFallback(AssistantState state)
    {
        var promptFragment = $"context:{Environment.NewLine}{this.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
        if (state.Colors.TryGetValue(this.Name, out var userInput) && !string.IsNullOrWhiteSpace(userInput))
            promptFragment += $"user prompt:{Environment.NewLine}{userInput}";

        return promptFragment;
    }

    #endregion

    public PickerVariant GetPickerVariant() => Enum.TryParse<PickerVariant>(this.PickerVariant, out var variant) ? variant : MudBlazor.PickerVariant.Static;
}
