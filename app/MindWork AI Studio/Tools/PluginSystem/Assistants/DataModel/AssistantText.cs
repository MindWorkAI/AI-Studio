namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantText : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.TEXT;
    
    public Dictionary<string, object> Props { get; set; } = new();
    
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Content
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Content));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Content), value);
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
