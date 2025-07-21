namespace AIStudio.Tools.PluginSystem;

public class AssistantButton : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.BUTTON;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => this.Props.TryGetValue(nameof(this.Name), out var v) ? v.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Name)] = value;
    }
    public string Text
    {
        get => this.Props.TryGetValue(nameof(this.Text), out var v) ? v.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Text)] = value;
    }
    public string Action
    {
        get => this.Props.TryGetValue(nameof(this.Action), out var v) ? v.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Action)] = value;
    }
}