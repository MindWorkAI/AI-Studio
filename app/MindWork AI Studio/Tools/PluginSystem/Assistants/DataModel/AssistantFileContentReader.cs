namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantFileContentReader : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.FILE_CONTENT_READER;
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
}
