using System.Text;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public abstract class StatefulAssistantComponentBase : NamedAssistantComponentBase, IStatefulAssistantComponent
{
    public abstract void InitializeState(AssistantState state);
    public abstract string UserPromptFallback(AssistantState state);
    
    public string UserPrompt
    {
        get => AssistantComponentPropHelper.ReadString(this.Props, nameof(this.UserPrompt));
        set => AssistantComponentPropHelper.WriteString(this.Props, nameof(this.UserPrompt), value);
    }

    protected string BuildAuditPromptBlock(string? value)
    {
        var builder = new StringBuilder();
        var fieldName = this.Type.ToString().ToLowerInvariant();
        
        builder.AppendLine($"[{fieldName}]");
        builder.Append("name: ").AppendLine(this.Name);
        builder.AppendLine("context:");
        builder.AppendLine(!string.IsNullOrEmpty(this.UserPrompt) ? this.UserPrompt : "<not provided>");
        builder.AppendLine("value:");
        builder.AppendLine(!string.IsNullOrEmpty(value) ? value : "<empty>");
        builder.Append($"[/{fieldName}]").AppendLine().AppendLine();
        return builder.ToString();
    }
}
