namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantTextArea : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.TEXT_AREA;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();
    
    public string Name
    {
        get => this.Props.TryGetValue(nameof(this.Name), out var val) ? val.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Name)] = value;
    }
    
    public string Label
    {
        get => this.Props.TryGetValue(nameof(this.Label), out var val) ? val.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Label)] = value;
    }
    
    public bool ReadOnly
    {
        get => this.Props.TryGetValue(nameof(this.ReadOnly), out var val) && val is true;
        set => this.Props[nameof(this.ReadOnly)] = value;
    }
}