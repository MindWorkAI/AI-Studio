using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Dialogs;

public sealed record AssistantPluginAuditDialogResult(PluginAssistantAudit? Audit, bool ActivatePlugin);