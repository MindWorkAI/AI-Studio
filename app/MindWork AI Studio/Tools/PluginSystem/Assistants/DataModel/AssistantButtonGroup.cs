namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public sealed class AssistantButtonGroup : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.BUTTON_GROUP;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Variant
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Variant));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Variant), value);
    }

    public string Color
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Color));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Color), value);
    }

    public string Size
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Size));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Size), value);
    }

    public bool OverrideStyles
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.OverrideStyles), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.OverrideStyles), value);
    }

    public bool Vertical
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.Vertical), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.Vertical), value);
    }

    public bool DropShadow
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.DropShadow), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.DropShadow), value);
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

    public Variant GetVariant() => Enum.TryParse<Variant>(this.Variant, out var variant) ? variant : MudBlazor.Variant.Filled;
}
