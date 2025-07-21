namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantDropdownItem
{
    public string Value { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    
    public static AssistantDropdownItem Default() => new() { Value = string.Empty, Display = string.Empty };
}