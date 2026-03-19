namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public abstract class StatefulAssistantComponentBase : NamedAssistantComponentBase, IStatefulAssistantComponent
{
    public abstract void InitializeState(AssistantState state);
    public abstract string UserPromptFallback(AssistantState state);
    
    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
    }
}
