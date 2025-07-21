namespace AIStudio.Tools.PluginSystem;

public abstract class AssistantComponentBase : IAssistantComponent
{
    public abstract AssistantUiCompontentType Type { get; }
    public Dictionary<string, object> Props { get; }
    public List<IAssistantComponent> Children { get; }
}