using AIStudio.Settings;
using AIStudio.Tools.DataBackup;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Shows the data and configuration folders of AI Studio, and which of their contents belong into a backup.
/// </summary>
/// <remarks>
/// For users who back up their computer with a tool of their own. Everything shown here comes from
/// the same list the backups of AI Studio follow. It stays complete even when the organization does not
/// allow backups within AI Studio: that decides about the backups AI Studio makes, not about which
/// folders a user may copy.
/// </remarks>
public partial class DataFolders : MSGComponentBase
{
    private static readonly DataBackupLocation[] LOCATIONS = [DataBackupLocation.DATA_DIRECTORY, DataBackupLocation.CONFIG_DIRECTORY];

    private static readonly IReadOnlyList<DataBackupEntry> INCLUDED_ENTRIES = DataBackupContents.ENTRIES.Where(entry => entry.IsIncluded).ToList();

    private static readonly IReadOnlyList<DataBackupEntry> EXCLUDED_ENTRIES = DataBackupContents.ENTRIES.Where(entry => !entry.IsIncluded).ToList();

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private ILogger<DataFolders> Logger { get; init; } = null!;

    /// <summary>
    /// Tells everybody but experts where to go instead.
    /// </summary>
    /// <remarks>
    /// Pointing to the backups of AI Studio makes sense only while the organization allows them.
    /// </remarks>
    private string ExpertNotice
    {
        get
        {
            var notice = T("The following is meant for experts who back up their computer with a tool of their own.");
            return this.SettingsManager.ConfigurationData.App.AllowUserToBackUpData
                ? $"{notice} {T("To back up your data with AI Studio itself, or to move it to another computer, use Data Backup & Restore on this page or in the settings instead.")}"
                : notice;
        }
    }

    private static bool IsDataFolderInsideConfigFolder =>!string.IsNullOrWhiteSpace(SettingsManager.ConfigDirectory)
                                                          && !string.IsNullOrWhiteSpace(SettingsManager.DataDirectory)
                                                          && PathTools.IsInsideDirectory(SettingsManager.ConfigDirectory, SettingsManager.DataDirectory);

    /// <summary>
    /// The plugins which the organization deployed among the user's own ones, as AI Studio found them when it loaded the plugins last.
    /// </summary>
    private static IReadOnlyList<string> OrganizationPluginFolders => string.IsNullOrWhiteSpace(SettingsManager.DataDirectory)
        ? []
        : DataBackupContents.FindOrganizationPluginsAmongUserPlugins(PluginFactory.AvailablePlugins.OfType<IAvailablePlugin>(), SettingsManager.DataDirectory);

    private static string FolderPath(DataBackupLocation location) => location switch
    {
        DataBackupLocation.DATA_DIRECTORY => SettingsManager.DataDirectory ?? string.Empty,
        DataBackupLocation.CONFIG_DIRECTORY => SettingsManager.ConfigDirectory ?? string.Empty,
        _ => string.Empty,
    };

    private string FolderName(DataBackupLocation location) => location switch
    {
        DataBackupLocation.DATA_DIRECTORY => T("Data folder"),
        DataBackupLocation.CONFIG_DIRECTORY => T("Configuration folder"),
        _ => string.Empty,
    };

    private string Describe(DataBackupEntryKind kind) => kind switch
    {
        DataBackupEntryKind.SETTINGS => T("Your settings."),
        DataBackupEntryKind.WORKSPACES => T("Your chats in workspaces, along with their transcripts."),
        DataBackupEntryKind.TEMPORARY_CHATS => T("Your temporary chats, along with their transcripts."),
        DataBackupEntryKind.CHAT_PROVENANCE => T("What the AI received in your chats besides your messages, such as the content of attached files, the results of tools, and passages of your data sources. It is part of your chats."),
        DataBackupEntryKind.PLUGINS => T("Your plugins, such as the assistants you built, apart from the folders listed below."),
        DataBackupEntryKind.TOKENIZERS => T("The tokenizers you chose for your providers."),
        DataBackupEntryKind.VISUAL_BRIEFINGS => T("Your projects in the Visual Briefing Assistant."),
        DataBackupEntryKind.DATA_SOURCE_INDEX => T("The index of your local data sources: which files AI Studio embedded, and their text."),
        DataBackupEntryKind.VECTOR_STORES => T("The embeddings of your local data sources."),
        DataBackupEntryKind.MEDIA_TRANSCRIPTS => T("The transcripts of the audio and video files in your local data sources."),

        DataBackupEntryKind.SETTINGS_OF_OTHER_VERSIONS => T("Copies of your settings, which every version of AI Studio keeps for itself. Your current settings are all a backup needs."),
        DataBackupEntryKind.BUILT_IN_PLUGINS => T("Plugins which come with AI Studio, such as the language packs. AI Studio brings them back by itself."),
        DataBackupEntryKind.ORGANIZATION_PLUGINS => T("Plugins of your organization. Your organization brings them back."),
        DataBackupEntryKind.TEMPORARY_FILES => T("Files which AI Studio needs only for a moment and deletes again."),
        DataBackupEntryKind.DOWNLOADED_TOOLS => T("Tools which AI Studio downloaded for this OS, such as Pandoc. On another computer, AI Studio downloads them again."),
        DataBackupEntryKind.INTERFACE_KEYS => T("Keys with which AI Studio protects data in its user interface. Only the Flatpak keeps them here, and on another computer, AI Studio creates new ones."),
        DataBackupEntryKind.LOGS => T("Log files. They help when something goes wrong, but they hold none of your data."),

        _ => string.Empty,
    };

    private async Task CopyPath(string path) => await this.RustService.CopyText2Clipboard(path);

    private async Task OpenInFileManager(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Folder, T("The folder is not known yet.")));
            return;
        }

        OpenPathResponse response;
        try
        {
            response = await this.RustService.TryOpenPathInRuntimeFileManager(path);
        }
        catch (Exception e)
        {
            this.Logger.LogWarning(e, "Could not open the folder '{Path}' in the file manager.", path);
            await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, T("Could not open the folder.")));
            return;
        }

        if (response.Success)
            return;

        var issue = string.IsNullOrWhiteSpace(response.Issue) ? T("Unknown error") : response.Issue;
        await this.MessageBus.SendError(new(Icons.Material.Filled.Folder, string.Format(T("Could not open the folder: {0}"), issue)));
    }
}