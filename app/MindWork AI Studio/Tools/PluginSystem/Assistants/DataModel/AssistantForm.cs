namespace AIStudio.Tools.PluginSystem;

public class AssistantForm : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.FORM;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();
}