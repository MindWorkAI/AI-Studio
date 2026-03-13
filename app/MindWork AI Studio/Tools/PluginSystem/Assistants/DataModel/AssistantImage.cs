namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantImage : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.IMAGE;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Src
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Src));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Src), value);
    }

    public string Alt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Alt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Alt), value);
    }

    public string Caption
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Caption));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Caption), value);
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
