namespace AIStudio.Tools.PluginSystem.Assistants.DataModel.Layout;

internal sealed class AssistantAccordionSection : NamedAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LAYOUT_ACCORDION_SECTION;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public bool KeepContentAlive = true;

    public string HeaderText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HeaderText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HeaderText), value);
    }
    
    public string HeaderColor
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HeaderColor));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HeaderColor), value);
    }
    
    public string HeaderIcon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HeaderIcon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HeaderIcon), value);
    }
    
    public string HeaderTypo
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HeaderTypo));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HeaderTypo), value);
    }
    
    public string HeaderAlign
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HeaderAlign));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HeaderAlign), value);
    }
    
    public bool IsDisabled
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsDisabled));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsDisabled), value);
    }
    
    public bool IsExpanded
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsExpanded));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsExpanded), value);
    }
    
    public bool IsDense
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsDense));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsDense), value);
    }
    
    public bool HasInnerPadding
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.HasInnerPadding), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.HasInnerPadding), value);
    }
    
    public bool HideIcon
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.HideIcon));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.HideIcon), value);
    }

    public int? MaxHeight
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.MaxHeight));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.MaxHeight), value);
    }

    public string ExpandIcon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.ExpandIcon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.ExpandIcon), value);
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
