using AIStudio.Components;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Dialogs;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Pages;

public partial class Plugins : MSGComponentBase
{
    private const string GROUP_ENABLED = "Enabled";
    private const string GROUP_DISABLED = "Disabled";
    private const string GROUP_INTERNAL = "Internal";
    private bool isAutoAuditing;
    private bool isImportingAssistantPlugin;

    private DataAssistantPluginAudit AssistantPluginAuditSettings => this.SettingsManager.ConfigurationData.AssistantPluginAudit;
    
    private TableGroupDefinition<IPluginMetadata> groupConfig = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private AssistantPluginAuditService AssistantPluginAuditService { get; init; } = null!;

    [Inject] 
    private PluginShareService PluginShareService { get; init; } = null!;

    [Inject]
    private RustService RustService { get; init; } = null!;

    [Inject]
    private AssistantPluginInstallService AssistantPluginInstallService { get; init; } = null!;

    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger(nameof(Plugins));
    
    private bool isSharingPlugin;

    /// <summary>
    /// Number of active drop areas above this page. While there is any, another component owns the
    /// dropped files and this page must not catch them.
    /// </summary>
    private uint numDropAreasAboveThis;

    private bool isDraggingOverPage;

    private const string IMPORT_ICON =
        @"<svg class=""mud-icon-root mud-svg-icon mud-dark-text mud-icon-size-medium"" focusable=""false"" viewBox=""0 0 24 24"" aria-hidden=""true"" role=""img"">
    <path d=""M0 0h24v24H0V0z"" fill=""none""></path>
    <path d=""M16 5l-1.42 1.42-1.59-1.59V16h-1.98V4.83L9.42 6.42 8 5l4-4 4 4z"" transform=""rotate(180 12 10)""></path>
    <path d=""M20 10v11c0 1.1-.9 2-2 2H6c-1.11 0-2-.9-2-2V10c0-1.11.89-2 2-2h3v2H6v11h12V10h-3V8h3c1.1 0 2 .89 2 2z""></path>
  </svg>";

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.PLUGINS_RELOADED, Event.CONFIGURATION_CHANGED, Event.TAURI_EVENT_RECEIVED, Event.REGISTER_FILE_DROP_AREA, Event.UNREGISTER_FILE_DROP_AREA ]);

        // Register the whole page as a drop area, so users can drop a plugin archive anywhere on it:
        await this.MessageBus.SendMessage(this, Event.REGISTER_FILE_DROP_AREA, DropLayers.PAGES);

        this.groupConfig = new TableGroupDefinition<IPluginMetadata>
        {
            Expandable = true,
            IsInitiallyExpanded = true,
            Selector = pluginMeta =>
            {
                if (pluginMeta.IsInternal)
                    return GROUP_INTERNAL;
                
                return this.SettingsManager.IsPluginEnabled(pluginMeta)
                    ? GROUP_ENABLED
                    : GROUP_DISABLED;
            }
        };
        
        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await this.TryAutoAuditAssistantsAsync();
    }

    protected override void DisposeResources()
    {
        // Release the drop area again, so lower layers can catch dropped files:
        _ = this.MessageBus.SendMessage(this, Event.UNREGISTER_FILE_DROP_AREA, DropLayers.PAGES);
        base.DisposeResources();
    }

    #endregion

    private async Task PluginActivationStateChanged(IPluginMetadata pluginMeta)
    {
        if (this.SettingsManager.IsPluginEnabled(pluginMeta))
        {
            this.SettingsManager.ConfigurationData.EnabledPlugins.Remove(pluginMeta.Id);
            await this.SettingsManager.StoreSettings();
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
            return;
        }

        if (pluginMeta.Type is not PluginType.ASSISTANT)
        {
            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginMeta.Id);
            await this.SettingsManager.StoreSettings();
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
            return;
        }

        var assistantPlugin = PluginFactory.RunningPlugins.OfType<PluginAssistants>().FirstOrDefault(x => x.Id == pluginMeta.Id);
        if (assistantPlugin is null)
            return;

        var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, assistantPlugin);
        if (securityState.RequiresAudit)
        {
            await this.OpenAssistantAuditDialogAsync(pluginMeta.Id);
            return;
        }

        if (securityState is { IsBelowMinimum: true, IsBlocked: true })
        {
            var blockedAudit = securityState.Audit;
            if (blockedAudit is not null)
                await this.DialogService.ShowMessageBox(this.T("Assistant Audit"), $"{blockedAudit.Level.GetName()}: {blockedAudit.Summary}", this.T("Close"));
            return;
        }

        if (securityState is { IsBelowMinimum: true, CanOverride: true } &&
            !await this.ConfirmActivationBelowMinimumAsync(pluginMeta.Name, securityState.Audit!.Level))
        {
            return;
        }

        this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginMeta.Id);
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task OpenAssistantAuditDialogAsync(Guid pluginId)
    {
        var parameters = new DialogParameters<AssistantPluginAuditDialog>
        {
            { x => x.PluginId, pluginId },
        };
        var dialog = await this.DialogService.ShowAsync<AssistantPluginAuditDialog>(this.T("Assistant Audit"), parameters, DialogOptions.FULLSCREEN);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not AssistantPluginAuditDialogResult auditResult)
            return;

        if (auditResult.Audit is not null)
            this.UpsertAuditCard(auditResult.Audit);

        if (auditResult.ActivatePlugin)
            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginId);

        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }

    private async Task<bool> ConfirmActivationBelowMinimumAsync(string pluginName, AssistantAuditLevel actualLevel)
    {
        var dialogParameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.Message,
                string.Format(
                    this.T("The assistant plugin '{0}' was audited with the level '{1}', which is below the required minimum level \"{2}\". Your current settings allow activation anyway, but this may be potentially dangerous. Do you really want to enable this plugin?"),
                    pluginName,
                    actualLevel.GetName(),
                    this.AssistantPluginAuditSettings.MinimumLevel.GetName())
            },
        };

        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(this.T("Potentially Dangerous Plugin"), dialogParameters,
            DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        return dialogResult is not null && !dialogResult.Canceled;
    }
    
    private bool IsActivationSwitchDisabled(IPluginMetadata pluginMeta, bool isEnabled)
    {
        if (isEnabled || pluginMeta.Type is not PluginType.ASSISTANT)
            return false;

        var assistantPlugin = this.TryGetAssistantPlugin(pluginMeta.Id);
        if (assistantPlugin is null)
            return false;

        var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, assistantPlugin);
        return securityState is { IsBlocked: true, RequiresAudit: false };
    }

    private string GetActivationTooltip(IPluginMetadata pluginMeta, bool isEnabled)
    {
        if (isEnabled)
            return this.T("Disable plugin");

        if (pluginMeta.Type is not PluginType.ASSISTANT)
            return this.T("Enable plugin");

        var assistantPlugin = this.TryGetAssistantPlugin(pluginMeta.Id);
        if (assistantPlugin is null)
            return this.T("Enable plugin");

        var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, assistantPlugin);
        if (securityState.RequiresAudit)
            return securityState.ActionLabel;

        return securityState.IsBlocked
            ? securityState.Description
            : this.T("Enable plugin");
    }

    //
    // These methods decide whether an action exists for a plugin at all. They must not depend on
    // transient state like an ongoing share: they gate the markup, so a transient value would make
    // the action buttons disappear and reappear. Transient state belongs into the buttons' Disabled.
    //
    private static bool CanEditAssistantPlugin(IAvailablePlugin plugin) => plugin is { IsInternal: false, Type: PluginType.ASSISTANT } && !string.IsNullOrWhiteSpace(plugin.LocalPath);

    private static bool CanReviseAssistantPlugin(IAvailablePlugin plugin)
    {
        var assistantPlugin = PluginFactory.RunningPlugins.OfType<PluginAssistants>().FirstOrDefault(x => x.Id == plugin.Id);
        return plugin is { IsInternal: false, IsManagedByConfigServer: false, Type: PluginType.ASSISTANT } && !string.IsNullOrWhiteSpace(plugin.LocalPath) && assistantPlugin?.IsManagedByConfigServer is false;
    }

    /// <summary>
    /// Sharing is limited to assistant plugins because the import accepts only those. Otherwise,
    /// users would create archives nobody can install. Widen this once the import supports more
    /// plugin types.
    /// </summary>
    private static bool CanSharePlugin(IAvailablePlugin plugin) => plugin is { IsInternal: false, IsManagedByConfigServer: false, Type: PluginType.ASSISTANT } && !string.IsNullOrWhiteSpace(plugin.LocalPath);

    /// <summary>
    /// Highlights the plugin table while the user drags a file over the page, so it is visible
    /// where the file would land.
    /// </summary>
    private string PluginTableClass => this.isDraggingOverPage
        ? "border-dashed border rounded-lg mud-border-primary border-4"
        : "border-dashed border rounded-lg";

    /// <summary>
    /// Organizations may disable importing plugin archives by using a configuration plugin.
    /// </summary>
    private bool AllowPluginImport => this.SettingsManager.ConfigurationData.App.AllowUserToImportPlugins;

    /// <summary>
    /// Organizations may disable sharing and exporting plugins by using a configuration plugin.
    /// </summary>
    private bool AllowPluginSharing => this.SettingsManager.ConfigurationData.App.AllowUserToSharePlugins;

    /// <summary>
    /// Linux has no native share sheet, hence the plugin archive is exported to a location of the
    /// user's choice there. The action must be labeled accordingly.
    /// </summary>
    private static string SharePluginIcon => OperatingSystem.IsLinux() ? Icons.Material.Filled.FileDownload : Icons.Material.Filled.IosShare;

    private string SharePluginTooltip => OperatingSystem.IsLinux() ? this.T("Export plugin archive") : this.T("Share plugin archive");

    private string SharePluginLockText => OperatingSystem.IsLinux() ? this.T("Your organization has disabled exporting plugins.") : this.T("Your organization has disabled sharing plugins.");

    private async Task OpenAssistantPluginEditorDialogAsync(IAvailablePlugin plugin)
    {
        var parameters = new DialogParameters<AssistantPluginEditorDialog>
        {
            { x => x.PluginId, plugin.Id },
            { x => x.PluginLocalPath, plugin.LocalPath },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<AssistantPluginEditorDialog>(this.T("Edit Assistant Plugin"), parameters, DialogOptions.BLOCKING_FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not AssistantPluginEditorDialogResult result)
            return;

        await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Save, string.Format(this.T("The assistant plugin '{0}' has been successfully saved."), result.PluginName)));
        LOG.LogInformation($"The assistant plugin '{result.PluginName}' ({result.PluginId}) has been successfully updated.");
        await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task OpenAssistantPluginRevisionDialogAsync(IAvailablePlugin plugin)
    {
        var parameters = new DialogParameters<AssistantPluginRevisionDialog>
        {
            { x => x.PluginId, plugin.Id },
            { x => x.PluginLocalPath, plugin.LocalPath },
        };

        var dialogReference = await this.DialogService.ShowAsync<AssistantPluginRevisionDialog>(this.T("Revise Assistant Plugin"), parameters, DialogOptions.BLOCKING_FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not AssistantPluginRevisionDialogResult result)
            return;

        await this.MessageBus.SendSuccess(new(Icons.Material.Filled.AutoFixHigh, string.Format(this.T("The assistant plugin '{0}' has been successfully revised."), result.PluginName)));
        LOG.LogInformation($"The assistant plugin '{result.PluginName}' ({result.PluginId}) has been successfully revised.");
        await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task SharePluginAsync(IAvailablePlugin plugin)
    {
        if (this.isSharingPlugin)
            return;

        this.isSharingPlugin = true;
        // invoke a state change right away to guard action buttons
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var shareResult = await this.PluginShareService.ShareAsync(plugin, CancellationToken.None);
            if (shareResult.Cancelled)
                return;

            if (!shareResult.Success)
            {
                LOG.LogError($"Sharing the plugin '{shareResult.PluginName}' from archive '{shareResult.ArchivePath}' failed with Issue: '{shareResult.Issue}'.");
                await this.MessageBus.SendError(new(Icons.Material.Filled.ReportProblem, OperatingSystem.IsLinux() ? T("An error occurred while exporting the plugin.") : T("An error occurred while sharing the plugin.")));
                return;
            }

            // On Linux, the user chose the target location, so we confirm where the archive was stored:
            if (OperatingSystem.IsLinux())
                await this.MessageBus.SendSuccess(new(Icons.Material.Filled.FileDownload, string.Format(T("The plugin archive was exported to '{0}'."), shareResult.ArchivePath)));
        }
        finally
        {
            this.isSharingPlugin = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private async Task ImportAssistantPluginAsync()
    {
        if (this.isImportingAssistantPlugin)
            return;

        if (!this.AllowPluginImport)
            return;

        var selection = await this.RustService.SelectFile(this.T("Import assistant plugin"), [FileTypes.PLUGIN_ARCHIVE]);
        if (selection.UserCancelled)
            return;

        await this.ImportPluginArchiveAsync(selection.SelectedFilePath);
    }

    /// <summary>
    /// Installs a plugin archive, no matter whether the user picked it through the import button or
    /// dropped it onto the page.
    /// </summary>
    /// <param name="archivePath">The local plugin archive to install.</param>
    private async Task ImportPluginArchiveAsync(string archivePath)
    {
        if (this.isImportingAssistantPlugin)
            return;

        if (!this.AllowPluginImport)
            return;

        this.isImportingAssistantPlugin = true;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var result = await this.AssistantPluginInstallService.InstallArchiveAsync(archivePath, this.ConfirmPluginImportAsync, CancellationToken.None);
            if (result.Cancelled)
                return;

            if (!result.Success)
            {
                LOG.LogError("Failed to import assistant plugin archive '{ArchivePath}': {Issue}", archivePath, result.Issue);

                // The user actively started this import, so we report the reason in a dialog
                // instead of a snackbar. Refused imports must not be missed:
                await this.ShowImportRefusedDialogAsync(result.Issue);
                return;
            }

            var message = result.ReplacedExisting
                ? this.T("Assistant updated.")
                : this.T("Assistant installed.");
            await this.MessageBus.SendSuccess(new(Icons.Material.Filled.Extension, message));
            await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        }
        finally
        {
            this.isImportingAssistantPlugin = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    /// <summary>
    /// Shows the metadata of a validated plugin archive and asks whether it may be installed.
    /// </summary>
    /// <param name="preview">The metadata the archive declares about itself.</param>
    /// <returns>True when the user confirmed the installation.</returns>
    private async Task<bool> ConfirmPluginImportAsync(PluginImportPreview preview)
    {
        var dialogParameters = new DialogParameters<PluginImportDialog>
        {
            { x => x.Preview, preview },
        };

        var dialogReference = await this.DialogService.ShowAsync<PluginImportDialog>(this.T("Install Plugin"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        return dialogResult is { Canceled: false };
    }

    private async Task ShowImportRefusedDialogAsync(string issue)
    {
        var dialogParameters = new DialogParameters<InformationDialog>
        {
            { x => x.Message, string.Format(this.T("The plugin could not be imported: {0}"), issue) },
            { x => x.Icon, Icons.Material.Filled.ReportProblem },
            { x => x.IconColor, Color.Error },
        };

        var dialogReference = await this.DialogService.ShowAsync<InformationDialog>(this.T("Import not possible"), dialogParameters, DialogOptions.FULLSCREEN);
        await dialogReference.Result;
    }

    private static bool IsSendingMail(string sourceUrl) => sourceUrl.TrimStart().StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    private PluginAssistants? TryGetAssistantPlugin(Guid pluginId) => PluginFactory.RunningPlugins.OfType<PluginAssistants>().FirstOrDefault(x => x.Id == pluginId);

    private async Task TryAutoAuditAssistantsAsync()
    {
        if (this.isAutoAuditing || !this.AssistantPluginAuditSettings.AutomaticallyAuditAssistants)
            return;

        this.isAutoAuditing = true;

        try
        {
            var wasConfigurationChanged = false;
            var assistantPlugins = PluginFactory.RunningPlugins.OfType<PluginAssistants>().ToList();
            foreach (var assistantPlugin in assistantPlugins)
            {
                var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, assistantPlugin);
                if (!securityState.RequiresAudit)
                    continue;

                var audit = await this.AssistantPluginAuditService.RunAuditAsync(assistantPlugin);
                if (audit.Level is AssistantAuditLevel.UNKNOWN)
                {
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.SettingsSuggest, string.Format(this.T("The automatic security audit for the assistant plugin '{0}' failed. Please run it manually."), assistantPlugin.Name)));
                    continue;
                }

                this.UpsertAuditCard(audit);
                wasConfigurationChanged = true;
            }

            if (!wasConfigurationChanged)
                return;

            await this.SettingsManager.StoreSettings();
            await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        }
        finally
        {
            this.isAutoAuditing = false;
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void UpsertAuditCard(PluginAssistantAudit audit)
    {
        var audits = this.SettingsManager.ConfigurationData.AssistantPluginAudits;
        var existingIndex = audits.FindIndex(x => x.PluginId == audit.PluginId);
        if (existingIndex >= 0)
            audits[existingIndex] = audit;
        else
            audits.Add(audit);
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                await this.TryAutoAuditAssistantsAsync();
                await this.InvokeAsync(this.StateHasChanged);
                break;

            case Event.CONFIGURATION_CHANGED:
                await this.InvokeAsync(this.StateHasChanged);
                break;

            case Event.REGISTER_FILE_DROP_AREA when sendingComponent != this:
                if (data is int registeredLayer && registeredLayer > DropLayers.PAGES)
                    this.numDropAreasAboveThis++;

                break;

            case Event.UNREGISTER_FILE_DROP_AREA when sendingComponent != this:
                if (data is int unregisteredLayer && unregisteredLayer > DropLayers.PAGES && this.numDropAreasAboveThis > 0)
                    this.numDropAreasAboveThis--;

                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_HOVERED }:
                if (!this.CanCatchDroppedFile())
                    return;

                this.isDraggingOverPage = true;
                await this.InvokeAsync(this.StateHasChanged);
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_CANCELED }:
            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.WINDOW_NOT_FOCUSED }:
                this.isDraggingOverPage = false;
                await this.InvokeAsync(this.StateHasChanged);
                break;

            case Event.TAURI_EVENT_RECEIVED when data is TauriEvent { EventType: TauriEventType.FILE_DROP_DROPPED, Payload: var droppedPaths }:
                this.isDraggingOverPage = false;
                await this.InvokeAsync(this.StateHasChanged);
                if (!this.CanCatchDroppedFile())
                    return;

                await this.ImportDroppedPluginArchiveAsync(droppedPaths);
                break;
        }
    }

    #endregion

    /// <summary>
    /// Decides whether this page may process dropped files: only when no drop area above it is
    /// active and when the organization allows importing plugins at all.
    /// </summary>
    private bool CanCatchDroppedFile() => this.numDropAreasAboveThis is 0 && this.AllowPluginImport && !this.isImportingAssistantPlugin;

    /// <summary>
    /// Imports a plugin archive the user dropped onto the page. Anything that is not exactly one
    /// plugin archive is reported instead of guessing what the user meant.
    /// </summary>
    /// <param name="droppedPaths">The paths of the dropped files.</param>
    private async Task ImportDroppedPluginArchiveAsync(IReadOnlyList<string> droppedPaths)
    {
        var archivePaths = droppedPaths.Where(path => FileTypes.IsAllowedPath(path, FileTypes.PLUGIN_ARCHIVE)).ToList();
        switch (archivePaths.Count)
        {
            case 0:
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.ReportProblem, string.Format(this.T("Please drop a plugin archive with the extension {0} or .zip."), PluginArchive.PLUGIN_FILE_EXTENSION)));
                return;

            case > 1:
                await this.MessageBus.SendWarning(new(Icons.Material.Filled.ReportProblem, this.T("Please drop only one plugin archive at a time.")));
                return;
        }

        await this.ImportPluginArchiveAsync(archivePaths[0]);
    }
}