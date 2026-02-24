namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantForm : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.FORM;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();
}