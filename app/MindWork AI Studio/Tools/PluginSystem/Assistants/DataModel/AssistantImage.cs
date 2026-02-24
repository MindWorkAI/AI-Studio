namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantImage : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.IMAGE;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Src
    {
        get => this.Props.TryGetValue(nameof(this.Src), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.Src)] = value;
    }

    public string Alt
    {
        get => this.Props.TryGetValue(nameof(this.Alt), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.Alt)] = value;
    }

    public string Caption
    {
        get => this.Props.TryGetValue(nameof(this.Caption), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.Caption)] = value;
    }
}
