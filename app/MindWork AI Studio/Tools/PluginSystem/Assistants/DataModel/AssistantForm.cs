namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantForm : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.FORM;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();
}