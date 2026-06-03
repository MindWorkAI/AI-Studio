namespace AIStudio.Tools.PluginSystem.Assistants.DataModel.Layout;

internal sealed class AssistantGrid : NamedAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LAYOUT_GRID;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Justify
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Justify));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Justify), value);
    }
    
    public int Spacing
    {
        get => AssistantComponentPropHelper.ReadInt(this.Props, nameof(this.Spacing), 6);
        set => AssistantComponentPropHelper.WriteInt(this.Props, nameof(this.Spacing), value);
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
