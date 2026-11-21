using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.AIOperations.PluginAudit;

/// <summary>
/// A plugin to audit for security risks.
/// </summary>
/// <param name="Plugin">The assistant plugin to audit.</param>
public sealed record PluginAuditRequest(PluginAssistants Plugin);
