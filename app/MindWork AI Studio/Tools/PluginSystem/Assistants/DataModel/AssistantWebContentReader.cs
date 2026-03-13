namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantWebContentReader : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.WEB_CONTENT_READER;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }

    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
    }

    public bool Preselect
    {
        get => this.Props.TryGetValue(nameof(this.Preselect), out var v) && v is true;
        set => this.Props[nameof(this.Preselect)] = value;
    }

    public bool PreselectContentCleanerAgent
    {
        get => this.Props.TryGetValue(nameof(this.PreselectContentCleanerAgent), out var v) && v is true;
        set => this.Props[nameof(this.PreselectContentCleanerAgent)] = value;
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
