using AIStudio.Assistants.Dynamic;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantFileContentReader : StatefulAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.FILE_CONTENT_READER;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

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
        if (!state.FileContent.ContainsKey(this.Name))
            state.FileContent[this.Name] = new FileContentState();
    }

    public override string UserPromptFallback(AssistantState state)
    {
        state.FileContent.TryGetValue(this.Name, out var fileState);
        return this.BuildAuditPromptBlock(fileState?.Content);
    }

    #endregion
}
