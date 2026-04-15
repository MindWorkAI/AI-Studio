namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public abstract class AssistantComponentBase : IAssistantComponent
{
    public abstract AssistantComponentType Type { get; }
    public abstract Dictionary<string, object> Props { get; set; }
    public abstract List<IAssistantComponent> Children { get; set; }
}
