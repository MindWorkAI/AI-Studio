namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantSwitch : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.SWITCH;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

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

    public bool Value
    {
        get => this.Props.TryGetValue(nameof(this.Value), out var val) && val is true;
        set => this.Props[nameof(this.Value)] = value;
    }

    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
    }

    public string LabelOn
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.LabelOn));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.LabelOn), value);
    }

    public string LabelOff
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.LabelOff));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.LabelOff), value);
    }

}
