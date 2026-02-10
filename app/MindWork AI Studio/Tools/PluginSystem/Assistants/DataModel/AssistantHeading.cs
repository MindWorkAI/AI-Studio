namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantHeading : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.HEADING;

    public Dictionary<string, object> Props { get; set; } = new();
    
    public List<IAssistantComponent> Children { get; set; } = new();
    
    public string Text
    {
        get => this.Props.TryGetValue(nameof(this.Text), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.Text)] = value;
    }

    public int Level
    {
        get => this.Props.TryGetValue(nameof(this.Level), out var v) 
               && int.TryParse(v.ToString(), out var i) 
            ? i 
            : 2; 
        set => this.Props[nameof(this.Level)] = value;
    }
}