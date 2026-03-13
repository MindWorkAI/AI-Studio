using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public sealed class AssistantButton : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.BUTTON;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }

    public string Text
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Text));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Text), value);
    }

    public LuaFunction? Action
    {
        get => this.Props.TryGetValue(nameof(this.Action), out var value) && value is LuaFunction action ? action : null;
        set => AssistantComponentPropHelper.WriteObject(this.Props, nameof(this.Action), value);
    }
    
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
    
    public bool IsFullWidth
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsFullWidth), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsFullWidth), value);
    }
    
    public string StartIcon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.StartIcon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.StartIcon), value);
    }
    
    public string EndIcon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.EndIcon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.EndIcon), value);
    }
    
    public string IconColor
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.IconColor));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.IconColor), value);
    }
    
    public string IconSize
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.IconSize));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.IconSize), value);
    }
    
    public string Size
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Size));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Size), value);
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

    public Variant GetButtonVariant() => Enum.TryParse<Variant>(this.Variant, out var variant) ? variant : MudBlazor.Variant.Filled;
    
}
