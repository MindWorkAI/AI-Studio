namespace AIStudio.Tools.PluginSystem;

public interface IAssistantComponent
{
    AssistantUiCompontentType Type { get; }
    Dictionary<string, object> Props { get; }
    List<IAssistantComponent> Children { get; }
}