namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantTextArea : AssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.TEXT_AREA;
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

    public string PrefillText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.PrefillText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.PrefillText), value);
    }

    public bool IsSingleLine
    {
        get => this.Props.TryGetValue(nameof(this.IsSingleLine), out var val) && val is true;
        set => this.Props[nameof(this.IsSingleLine)] = value;
    }

    public bool ReadOnly
    {
        get => this.Props.TryGetValue(nameof(this.ReadOnly), out var val) && val is true;
        set => this.Props[nameof(this.ReadOnly)] = value;
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
