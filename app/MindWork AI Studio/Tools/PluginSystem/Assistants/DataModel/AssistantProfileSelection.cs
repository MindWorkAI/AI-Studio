using System.Collections.Generic;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantProfileSelection : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.PROFILE_SELECTION;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string ValidationMessage
    {
        get => this.Props.TryGetValue(nameof(this.ValidationMessage), out var v)
            ? v.ToString() ?? string.Empty
            : string.Empty;
        set => this.Props[nameof(this.ValidationMessage)] = value;
    }
}
