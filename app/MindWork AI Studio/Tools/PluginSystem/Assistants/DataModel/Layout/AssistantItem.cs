using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel.Layout;

public sealed class AssistantItem : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LAYOUT_ITEM;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }
    
    public int? Xs
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.Xs));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.Xs), value);
    }
    
    public int? Sm
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.Sm));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.Sm), value);
    }
    
    public int? Md
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.Md));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.Md), value);
    }
    
    public int? Lg
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.Lg));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.Lg), value);
    }
    
    public int? Xl
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.Xl));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.Xl), value);
    }
    
    public int? Xxl
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.Xxl));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.Xxl), value);
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
