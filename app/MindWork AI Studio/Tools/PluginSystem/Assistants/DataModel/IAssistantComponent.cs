namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public interface IAssistantComponent
{
    AssistantComponentType Type { get; }
    Dictionary<string, object> Props { get; }
    List<IAssistantComponent> Children { get; }
}