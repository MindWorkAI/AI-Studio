using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel.Layout;

internal sealed class AssistantAccordion : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LAYOUT_ACCORDION;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }
    
    public bool AllowMultiSelection
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.AllowMultiSelection), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.AllowMultiSelection), value);
    }
    
    public bool IsDense
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsDense), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsDense), value);
    }
    
    public bool HasOutline
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.HasOutline), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.HasOutline), value);
    }
    
    public bool IsSquare
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsSquare), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsSquare), value);
    }
    
    public int Elevation
    {
        get => AssistantComponentPropHelper.ReadInt(this.Props, nameof(this.Elevation), 0);
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
