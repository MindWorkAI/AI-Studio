namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantProviderSelection : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.PROVIDER_SELECTION;
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
}