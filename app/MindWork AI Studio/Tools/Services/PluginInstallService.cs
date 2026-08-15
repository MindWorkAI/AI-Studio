using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools.Services;

/// <summary>
/// Installs, updates, and removes the plugins AI Studio manages locally.
/// </summary>
/// <remarks>
/// The implementation is split across several files:<br/>
/// - <c>PluginInstallService.AssistantBuilder.cs</c>: installing generated assistant plugin code<br/>
/// - <c>PluginInstallService.Editing.cs</c>: editing an installed assistant plugin<br/>
/// - <c>PluginInstallService.Import.cs</c>: importing plugin archives<br/>
/// - <c>PluginInstallService.Delete.cs</c>: removing installed plugins<br/>
/// - <c>PluginInstallService.Installation.cs</c>: the shared validation and installation steps<br/>
/// - <c>PluginInstallService.FileSystem.cs</c>: the shared path and directory helpers
/// </remarks>
public sealed partial class PluginInstallService
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginInstallService).Namespace, nameof(PluginInstallService));

    private const string PLUGIN_FILE_NAME = "plugin.lua";
    private const string ASSISTANT_BUILDER_DIRECTORY_PREFIX = "assistant-builder";
    private const string PLUGIN_IMPORT_DIRECTORY_PREFIX = "plugin-import";
    private const string DELETE_BACKUP_DIRECTORY = ".plugin-delete-backups";
    private const string INSTALL_BACKUP_DIRECTORY = ".plugin-install-backups";
    private const string STAGING_DIRECTORY = ".plugin-staging";
    private const int STAGING_RETENTION_HOURS = 24;
    private const int DIRECTORY_PREFIX_MAX_LEN = 80;

    private readonly ILogger<PluginInstallService> logger;
    private readonly SettingsManager settingsManager;
    private readonly AssistantSessionService assistantSessionService;
    private readonly MediaTranscriptionService mediaTranscriptionService;
    private readonly SemaphoreSlim installSemaphore = new(1, 1);

    private static AssistantPluginInstallResult Error(string issue) => new(false, Guid.Empty, string.Empty, string.Empty, false, issue);

    private static AssistantPluginInstallResult CancelledByUser() => new(false, Guid.Empty, string.Empty, string.Empty, false, string.Empty, true);

    private static AssistantPluginCheckResult CheckError(string issue) => new(false, Guid.Empty, string.Empty, issue);

    private static PluginDeleteResult DeleteError(IPluginMetadata plugin, string pluginDirectory, string issue) => new(false, plugin.Id, plugin.Name, pluginDirectory, issue);

    private static AssistantPluginUpdateResult UpdateError(IPluginMetadata plugin, string pluginDirectory, string issue) => new(false, plugin.Id, plugin.Name, pluginDirectory, issue);

    public PluginInstallService(ILogger<PluginInstallService> logger, SettingsManager settingsManager, AssistantSessionService assistantSessionService, MediaTranscriptionService mediaTranscriptionService)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.assistantSessionService = assistantSessionService;
        this.mediaTranscriptionService = mediaTranscriptionService;
        this.logger.LogInformation("The plugin install service has been initialized.");
    }

    private sealed record PluginValidationResult(bool Success, string StagingDirectory, PluginBase? Plugin, string Issue)
    {
        public static PluginValidationResult Failure(string issue) => new(false, string.Empty, null, issue);

        /// <summary>
        /// The validated plugin as an assistant plugin, or null when it has another type.
        /// </summary>
        public PluginAssistants? AssistantPlugin => this.Plugin as PluginAssistants;
    }
}