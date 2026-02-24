using System.Collections.Generic;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantHeading : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.HEADING;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Text
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Text));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Text), value);
    }

    public int Level
    {
        get => this.Props.TryGetValue(nameof(this.Level), out var v) 
               && int.TryParse(v.ToString(), out var i) 
            ? i 
            : 2;
        set => this.Props[nameof(this.Level)] = value;
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
