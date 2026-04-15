namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantDropdown : StatefulAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.DROPDOWN;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Label
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Label));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Label), value);
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
    
    public bool IsMultiselect
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsMultiselect));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsMultiselect), value);
    }

    public bool HasSelectAll
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.HasSelectAll));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.HasSelectAll), value);
    }

    public string SelectAllText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.SelectAllText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.SelectAllText), value);
    }

    public string HelperText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HelperText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HelperText), value);
    }
    
    public string OpenIcon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.OpenIcon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.OpenIcon), value);
    }
    
    public string CloseIcon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.CloseIcon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.CloseIcon), value);
    }
    
    public string IconColor
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.IconColor));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.IconColor), value);
    }
    
    public string IconPositon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.IconPositon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.IconPositon), value);
    }
    
    public string Variant
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Variant));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Variant), value);
    }

    #region Implementation of IStatefulAssistantComponent

    public override void InitializeState(AssistantState state)
    {
        if (this.IsMultiselect)
        {
            if (!state.MultiSelect.ContainsKey(this.Name))
                state.MultiSelect[this.Name] = string.IsNullOrWhiteSpace(this.Default.Value) ? [] : [this.Default.Value];

            return;
        }

        if (!state.SingleSelect.ContainsKey(this.Name))
            state.SingleSelect[this.Name] = this.Default.Value;
    }

    public override string UserPromptFallback(AssistantState state)
    {
        if (this.IsMultiselect && state.MultiSelect.TryGetValue(this.Name, out var selections))
            return this.BuildAuditPromptBlock(string.Join(Environment.NewLine, selections.OrderBy(static value => value, StringComparer.Ordinal)));

        state.SingleSelect.TryGetValue(this.Name, out var userInput);
        return this.BuildAuditPromptBlock(userInput);
    }

    #endregion

    internal string ResolveDisplayText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return this.Default.Display;

        var item = this.GetRenderedItems().FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.Ordinal));
        return item?.Display ?? value;
    }

    private List<AssistantDropdownItem> GetRenderedItems()
    {
        if (string.IsNullOrWhiteSpace(this.Default.Value))
            return this.Items;

        if (this.Items.Any(item => string.Equals(item.Value, this.Default.Value, StringComparison.Ordinal)))
            return this.Items;

        return [this.Default, .. this.Items];
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
