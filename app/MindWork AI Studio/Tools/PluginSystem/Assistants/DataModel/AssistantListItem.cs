namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantListItem
{
    public string Type { get; set; } = "TEXT";
    public string Text { get; set; } = string.Empty;
    public string? Href { get; set; }
}