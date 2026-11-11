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
        
        builder.AppendLineLF($"[{fieldName}]");
        builder.Append("name: ").AppendLineLF(this.Name);
        builder.AppendLineLF("context:");
        builder.AppendLineLF(!string.IsNullOrEmpty(this.UserPrompt) ? this.UserPrompt : "<not provided>");
        builder.AppendLineLF("value:");
        builder.AppendLineLF(!string.IsNullOrEmpty(value) ? value : "<empty>");
        builder.Append($"[/{fieldName}]").AppendLineLF().AppendLineLF();
        return builder.ToString();
    }
}
