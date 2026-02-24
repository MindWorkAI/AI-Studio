using System.Collections.Generic;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantList : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.LIST;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public List<AssistantListItem> Items
    {
        get => this.Props.TryGetValue(nameof(this.Items), out var v) && v is List<AssistantListItem> list
            ? list
            : [];
        set => this.Props[nameof(this.Items)] = value;
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
