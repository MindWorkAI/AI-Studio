namespace AIStudio.Tools.PluginSystem.Assistants.DataModel.Layout;

internal sealed class AssistantAccordion : NamedAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LAYOUT_ACCORDION;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public bool AllowMultiSelection
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.AllowMultiSelection));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.AllowMultiSelection), value);
    }
    
    public bool IsDense
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsDense));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsDense), value);
    }
    
    public bool HasOutline
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.HasOutline), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.HasOutline), value);
    }
    
    public bool IsSquare
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsSquare));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsSquare), value);
    }
    
    public int Elevation
    {
        get => AssistantComponentPropHelper.ReadInt(this.Props, nameof(this.Elevation));
        set => AssistantComponentPropHelper.WriteInt(this.Props, nameof(this.Elevation), value);
    }
    
    public bool HasSectionPaddings
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.HasSectionPaddings), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.HasSectionPaddings), value);
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
