namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantButton : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.BUTTON;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }
    public string Text
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Text));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Text), value);
    }
    
    public string Action
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Action));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Action), value);
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
}
