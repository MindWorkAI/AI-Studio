using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Dialogs;

public sealed record DirectChatLauncherSettingsDialogResult(Guid PluginId, string PluginName, PluginAssistantAudit? Audit);