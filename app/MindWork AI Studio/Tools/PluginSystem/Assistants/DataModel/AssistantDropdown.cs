namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantDropdown : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.DROPDOWN;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Name
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Name));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Name), value);
    }

    public string Label
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Label));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Label), value);
    }

    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
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

    public string Class
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Class));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Class), value);
    }

    public string Style
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Style));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Style), value);
    }
}
