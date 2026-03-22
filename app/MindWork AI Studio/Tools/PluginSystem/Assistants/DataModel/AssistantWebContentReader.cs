using System.Text;
using AIStudio.Assistants.Dynamic;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantWebContentReader : StatefulAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.WEB_CONTENT_READER;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public bool Preselect
    {
        get => this.Props.TryGetValue(nameof(this.Preselect), out var v) && v is true;
        set => this.Props[nameof(this.Preselect)] = value;
    }

    public bool PreselectContentCleanerAgent
    {
        get => this.Props.TryGetValue(nameof(this.PreselectContentCleanerAgent), out var v) && v is true;
        set => this.Props[nameof(this.PreselectContentCleanerAgent)] = value;
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

    #region Implemention of StatefulAssistantComponent

    public override void InitializeState(AssistantState state)
    {
        if (!state.WebContent.ContainsKey(this.Name))
        {
            state.WebContent[this.Name] = new WebContentState
            {
                Preselect = this.Preselect,
                PreselectContentCleanerAgent = this.PreselectContentCleanerAgent,
            };
        }
    }

    public override string UserPromptFallback(AssistantState state)
    {
        var promptFragment = new StringBuilder();
        
        if (state.WebContent.TryGetValue(this.Name, out var webState))
        {
            if (!string.IsNullOrWhiteSpace(this.UserPrompt))
                promptFragment.Append($"context:{Environment.NewLine}{this.UserPrompt}{Environment.NewLine}---{Environment.NewLine}");

            if (!string.IsNullOrWhiteSpace(webState.Content))
                promptFragment.Append($"user prompt:{Environment.NewLine}{webState.Content}");
        }

        return promptFragment.ToString();
    }

    #endregion
}
