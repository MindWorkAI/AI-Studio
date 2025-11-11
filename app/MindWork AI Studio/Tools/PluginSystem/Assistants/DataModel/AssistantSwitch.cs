namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantSwitch : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.SWITCH;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => this.Props.TryGetValue(nameof(this.Name), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.Name)] = value;
    }
    
    public string Label
    {
        get => this.Props.TryGetValue(nameof(this.Label), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.Label)] = value;
    }
    
    public bool Value
    {
        get => this.Props.TryGetValue(nameof(this.Value), out var val) && val is true;
        set => this.Props[nameof(this.Value)] = value;
    }
    
    public string UserPrompt
    {
        get => this.Props.TryGetValue(nameof(this.UserPrompt), out var val) 
            ? val.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.UserPrompt)] = value;
    }
    
    public string LabelOn
    {
        get => this.Props.TryGetValue(nameof(this.LabelOn), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.LabelOn)] = value;
    }
    
    public string LabelOff
    {
        get => this.Props.TryGetValue(nameof(this.LabelOff), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.LabelOff)] = value;
    }
}