namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public abstract class NamedAssistantComponentBase : AssistantComponentBase, INamedAssistantComponent
{
    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }
}