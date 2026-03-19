using AIStudio.Tools.PluginSystem.Assistants.Icons;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantTextArea : StatefulAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.TEXT_AREA;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Label
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Label));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Label), value);
    }
    
    public string HelperText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.HelperText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.HelperText), value);
    }
    
    public bool HelperTextOnFocus
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.HelperTextOnFocus), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.HelperTextOnFocus), value);
    }
    
    public string Adornment
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Adornment));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Adornment), value);
    }
    
    public string AdornmentIcon
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.AdornmentIcon));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.AdornmentIcon), value);
    }
    
    public string AdornmentText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.AdornmentText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.AdornmentText), value);
    }
    
    public string AdornmentColor
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.AdornmentColor));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.AdornmentColor), value);
    }

    public string PrefillText
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.PrefillText));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.PrefillText), value);
    }

    public int? Counter
    {
        get => AssistantComponentPropHelper.ReadNullableInt(this.Props, nameof(this.Counter));
        set => AssistantComponentPropHelper.WriteNullableInt(this.Props, nameof(this.Counter), value);
    }

    public int MaxLength
    {
        get => AssistantComponentPropHelper.ReadInt(this.Props, nameof(this.MaxLength), PluginAssistants.TEXT_AREA_MAX_VALUE);
        set => AssistantComponentPropHelper.WriteInt(this.Props, nameof(this.MaxLength), value);
    }

    public bool IsImmediate
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsImmediate));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsImmediate), value);
    }

    public bool IsSingleLine
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.IsSingleLine), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.IsSingleLine), value);
    }

    public bool ReadOnly
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.ReadOnly), false);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.ReadOnly), value);
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

    #region Implementation of IStatefulAssistantComponent

    public override void InitializeState(AssistantState state)
    {
        if (!state.Text.ContainsKey(this.Name))
            state.Text[this.Name] = this.PrefillText;
    }

    public override string UserPromptFallback(AssistantState state)
    {
        var userInput = string.Empty;
        
        var promptFragment = $"context:{Environment.NewLine}{this.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
        if (state.Text.TryGetValue(this.Name, out userInput) && !string.IsNullOrWhiteSpace(userInput))
            promptFragment += $"user prompt:{Environment.NewLine}{userInput}";

        return promptFragment;
    }

    #endregion

    public Adornment GetAdornmentPos() => Enum.TryParse<MudBlazor.Adornment>(this.Adornment, out var position) ? position : MudBlazor.Adornment.Start;
    
    public Color GetAdornmentColor() => Enum.TryParse<Color>(this.AdornmentColor, out var color) ? color : Color.Default;

    public string GetIconSvg() => MudBlazorIconRegistry.TryGetSvg(this.AdornmentIcon, out var svg) ? svg : string.Empty;
}
