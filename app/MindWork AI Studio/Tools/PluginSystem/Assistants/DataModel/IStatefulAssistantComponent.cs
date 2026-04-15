namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public interface IStatefulAssistantComponent : INamedAssistantComponent
{
    void InitializeState(AssistantState state);
    string UserPromptFallback(AssistantState state);
    string UserPrompt { get; set; }
}
