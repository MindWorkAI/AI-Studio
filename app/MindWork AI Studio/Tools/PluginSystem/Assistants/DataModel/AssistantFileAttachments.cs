using System.Text;
using AIStudio.Assistants.Dynamic;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal sealed class AssistantFileAttachment : StatefulAssistantComponentBase
{
    public override AssistantComponentType Type => AssistantComponentType.FILE_ATTACHMENTS;
    public override Dictionary<string, object> Props { get; set; } = new();
    public override List<IAssistantComponent> Children { get; set; } = new();

    public string Heading
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.Heading));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.Heading), value);
    }

    public bool CatchAllDocuments
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.CatchAllDocuments), true);
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.CatchAllDocuments), value);
    }

    public bool UseSmallForm
    {
        get => AssistantComponentPropHelper.ReadBool(this.Props, nameof(this.UseSmallForm));
        set => AssistantComponentPropHelper.WriteBool(this.Props, nameof(this.UseSmallForm), value);
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
        if (!state.FileAttachments.ContainsKey(this.Name))
            state.FileAttachments[this.Name] = new FileAttachmentState();
    }

    public override string UserPromptFallback(AssistantState state)
    {
        state.FileAttachments.TryGetValue(this.Name, out var fileState);

        if (fileState == null || fileState.DocumentPaths.Count == 0)
            return this.BuildAuditPromptBlock(null);

        var builder = new StringBuilder();

        foreach (var attachment in fileState.DocumentPaths.OrderBy(static attachment => attachment.FilePath, StringComparer.Ordinal))
            builder.AppendLine(attachment.FilePath);

        return this.BuildAuditPromptBlock(builder.ToString());
    }

    #endregion
}
