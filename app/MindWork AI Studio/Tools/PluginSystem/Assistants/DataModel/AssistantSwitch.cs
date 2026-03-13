using AIStudio.Tools.PluginSystem.Assistants.Icons;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantSwitch : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.SWITCH;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }

    public string Label
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Label));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Label), value);
    }

    public bool Value
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.Value), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.Value), value);
    }
    
    public bool Disabled
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.Disabled), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.Disabled), value);
    }

    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
    }

    public string LabelOn
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.LabelOn));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.LabelOn), value);
    }

    public string LabelOff
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.LabelOff));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.LabelOff), value);
    }
    
    public string LabelPlacement
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.LabelPlacement));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.LabelPlacement), value);
    }

    public string CheckedColor
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.CheckedColor));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.CheckedColor), value);
    }
    
    public string UncheckedColor
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UncheckedColor));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UncheckedColor), value);
    }
    
    public string Icon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Icon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Icon), value);
    }
    
    public string IconColor
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.IconColor));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.IconColor), value);
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

    public MudBlazor.Color GetColor(string colorString) => Enum.TryParse<Color>(colorString, out var color) ? color : MudBlazor.Color.Inherit;
    public Placement GetLabelPlacement() => Enum.TryParse<Placement>(this.LabelPlacement, out var placement) ? placement : Placement.Right;
    public string GetIconSvg() => MudBlazorIconRegistry.TryGetSvg(this.Icon, out var svg) ? svg : string.Empty;
}
