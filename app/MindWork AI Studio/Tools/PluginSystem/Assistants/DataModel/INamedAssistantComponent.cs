namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public interface INamedAssistantComponent : IAssistantComponent
{
    string Name { get; }
}
