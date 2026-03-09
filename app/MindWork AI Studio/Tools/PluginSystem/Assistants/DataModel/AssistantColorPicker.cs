namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantColorPicker : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.COLOR_PICKER;
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
    
    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
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

    public PickerVariant GetPickerVariant() => Enum.TryParse<PickerVariant>(this.PickerVariant, out var variant) ? variant : MudBlazor.PickerVariant.Static;
}
