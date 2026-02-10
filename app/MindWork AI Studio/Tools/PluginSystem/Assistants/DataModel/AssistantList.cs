namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantList : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.LIST;

    public Dictionary<string, object> Props { get; set; } = new();
    
    public List<IAssistantComponent> Children { get; set; } = new();
    
    public List<AssistantListItem> Items
    {
        get => this.Props.TryGetValue(nameof(this.Items), out var v) && v is List<AssistantListItem> list 
            ? list 
            : [];
        set => this.Props[nameof(this.Items)] = value;
    }
}