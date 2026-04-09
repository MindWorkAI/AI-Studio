namespace AIStudio.Tools.PluginSystem.Assistants.DataModel.Layout;

internal sealed class AssistantStack : NamedAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LAYOUT_STACK;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public bool IsRow
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsRow));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsRow), value);
    }
    
    public bool IsReverse
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsReverse));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsReverse), value);
    }
    
    public string Breakpoint
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Breakpoint));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Breakpoint), value);
    }

    public string Align
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Align));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Align), value);
    }
    
    public string Justify
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Justify));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Justify), value);
    }
    
    public string Stretch
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Stretch));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Stretch), value);
    }
    
    public string Wrap
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Wrap));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Wrap), value);
    }
    
    public int Spacing
    {
        get => AssistantComponentPropHelper.ReadInt(this.Props, nameof(this.Spacing), 3);
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
