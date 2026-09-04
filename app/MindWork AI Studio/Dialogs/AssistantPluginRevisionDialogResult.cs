using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Dialogs;

public sealed record AssistantPluginRevisionDialogResult(Guid PluginId, string PluginName, PluginAssistantAudit? Audit);