namespace AIStudio.Tools.PluginSystem;

public class AssistantDropdown : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.DROPDOWN;
    public Dictionary<string, object> Props { get; set; } = new();
    public List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => this.Props.TryGetValue(nameof(this.Name), out var v) ? v.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Name)] = value;
    }
    public string Label
    {
        get => this.Props.TryGetValue(nameof(this.Label), out var v) ? v.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Label)] = value;
    }
    public string Default
    {
        get => this.Props.TryGetValue(nameof(this.Default), out var v) ? v.ToString() ?? string.Empty : string.Empty;
        set => this.Props[nameof(this.Default)] = value;
    }
    public List<AssistantDropdownItem> Items
    {
        get => this.Props.TryGetValue(nameof(this.Items), out var v) && v is List<AssistantDropdownItem> list ? list : [];
        set => this.Props[nameof(this.Items)] = value;
    }
}