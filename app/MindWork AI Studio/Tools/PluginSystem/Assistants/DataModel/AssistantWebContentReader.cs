namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantWebContentReader : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.WEB_CONTENT_READER;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => this.Props.TryGetValue(nameof(this.Name), out var v)
            ? v.ToString() ?? string.Empty
            : string.Empty;
        set => this.Props[nameof(this.Name)] = value;
    }

    public string UserPrompt
    {
        get => this.Props.TryGetValue(nameof(this.UserPrompt), out var v)
            ? v.ToString() ?? string.Empty
            : string.Empty;
        set => this.Props[nameof(this.UserPrompt)] = value;
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
}
