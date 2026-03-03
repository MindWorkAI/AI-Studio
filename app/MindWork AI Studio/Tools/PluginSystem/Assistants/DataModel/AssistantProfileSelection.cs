namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantProfileSelection : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.PROFILE_SELECTION;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string ValidationMessage
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.ValidationMessage));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.ValidationMessage), value);
    }

    public string Class
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Class));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Class), value);
    }

    public string Style
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Style));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Style), value);
    }
}
