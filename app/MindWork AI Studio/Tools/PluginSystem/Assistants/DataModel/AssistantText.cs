namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantText : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.TEXT;
    
    public Dictionary<string, object> Props { get; set; } = new();
    
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Content
    {
        get => this.Props.TryGetValue(nameof(this.Content), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.Content)] = value;
    }
}