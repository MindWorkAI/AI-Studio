namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public class AssistantDropdown : AssistantComponentBase
{
    public override AssistantUiCompontentType Type => AssistantUiCompontentType.DROPDOWN;
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
    
    public string UserPrompt
    {
        get => this.Props.TryGetValue(nameof(this.UserPrompt), out var v) 
            ? v.ToString() ?? string.Empty 
            : string.Empty;
        set => this.Props[nameof(this.UserPrompt)] = value;
    }
    
    public AssistantDropdownItem Default
    {
        get
        {
            if (this.Props.TryGetValue(nameof(this.Default), out var v) && v is AssistantDropdownItem adi)
                return adi;

            return this.Items.Count > 0 ? this.Items[0] : AssistantDropdownItem.Default();
        }
        set => this.Props[nameof(this.Default)] = value;
    }
    
    public List<AssistantDropdownItem> Items
    {
        get => this.Props.TryGetValue(nameof(this.Items), out var v) && v is List<AssistantDropdownItem> list 
            ? list 
            : [];
        set => this.Props[nameof(this.Items)] = value;
    }
    
    public string ValueType
    {
        get => this.Props.TryGetValue(nameof(this.ValueType), out var v) 
            ? v.ToString() ?? "string" 
            : "string";
        set => this.Props[nameof(this.ValueType)] = value;
    }
    
    public IEnumerable<object> GetParsedDropdownValues()
    {
        foreach (var item in this.Items)
        {
            switch (this.ValueType.ToLowerInvariant())
            {
                case "int":
                    if (int.TryParse(item.Value, out var i)) yield return i;
                    break;
                case "double":
                    if (double.TryParse(item.Value, out var d)) yield return d;
                    break;
                case "bool":
                    if (bool.TryParse(item.Value, out var b)) yield return b;
                    break;
                default:
                    yield return item.Value;
                    break;
            }
        }
    }
}