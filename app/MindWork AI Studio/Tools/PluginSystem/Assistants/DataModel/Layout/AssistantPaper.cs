using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel.Layout;

internal sealed class AssistantPaper : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LAYOUT_PAPER;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }
    
    public int Elevation
    {
        get => AssistantComponentPropHelper.ReadInt(this.Props, nameof(this.Elevation), 1);
        set => AssistantComponentPropHelper.WriteInt(this.Props, nameof(this.Elevation), value);
    }
    
    public string Height
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Height));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Height), value);
    }
    
    public string MaxHeight
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.MaxHeight));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.MaxHeight), value);
    }
    
    public string MinHeight
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.MinHeight));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.MinHeight), value);
    }
    
    public string Width
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Width));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Width), value);
    }
    
    public string MaxWidth
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.MaxWidth));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.MaxWidth), value);
    }
    
    public string MinWidth
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.MinWidth));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.MinWidth), value);
    }
    
    public bool IsOutlined
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsOutlined), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsOutlined), value);
    }
    
    public bool IsSquare
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsSquare), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsSquare), value);
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