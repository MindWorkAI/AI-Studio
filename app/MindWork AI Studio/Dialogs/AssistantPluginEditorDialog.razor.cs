using System.Text;
using AIStudio.Components;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public sealed record AssistantPluginEditorDialogResult(Guid PluginId, string PluginName);

public partial class AssistantPluginEditorDialog : MSGComponentBase
{
    [Inject]
    protected RustService RustService { get; init; } = null!;
    
    [Inject]
    protected ISnackbar Snackbar { get; init; } = null!;
    
    private const string PLUGIN_FILE_NAME = "plugin.lua";
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(AssistantPluginEditorDialog));
    
    private readonly MudBlazor.DialogOptions optionsFullscreen = new()
    {
        BackdropClick = false,
        CloseButton = true,
        FullScreen = true,
        FullWidth = true,
        NoHeader = true,
    };

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private AssistantPluginInstallService AssistantPluginInstallService { get; init; } = null!;

    [Parameter]
    public Guid PluginId { get; set; }

    [Parameter]
    public string PluginLocalPath { get; set; } = string.Empty;

    private IAvailablePlugin? plugin;
    private CodeEditor? codeEditor;
    private string pluginFile = string.Empty;
    private string luaCode = string.Empty;
    private string issue = string.Empty;
    private bool isLoading = true;
    private bool isSaving;
    private bool isFullscreen;

    private bool CanSave => this.plugin is not null && !this.isLoading && !this.isSaving;
    private string FullscreenIcon => this.isFullscreen ? Icons.Material.Filled.FullscreenExit : Icons.Material.Filled.Fullscreen;
    private string FullscreenLabel => this.isFullscreen ? T("Exit fullscreen") : T("Fullscreen");

    private Func<string> Result2Copy => () => string.IsNullOrEmpty(this.pluginFile) ? string.Empty : this.pluginFile;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            this.plugin = PluginFactory.AvailablePlugins
                .OfType<IAvailablePlugin>()
                .FirstOrDefault(x => x.Id == this.PluginId && AreSamePath(x.LocalPath, this.PluginLocalPath));

            if (this.plugin is null)
            {
                this.issue = T("The assistant plugin could not be resolved.");
                return;
            }

            if (this.plugin is { IsInternal: true } || this.plugin.Type is not PluginType.ASSISTANT || string.IsNullOrWhiteSpace(this.plugin.LocalPath))
            {
                this.issue = T("This plugin cannot be edited.");
                return;
            }

            this.pluginFile = Path.Join(this.plugin.LocalPath, PLUGIN_FILE_NAME);
            if (!File.Exists(this.pluginFile))
            {
                this.issue = T("The plugin.lua file could not be found.");
                return;
            }

            this.luaCode = await File.ReadAllTextAsync(this.pluginFile, Encoding.UTF8);
        }
        catch (Exception e)
        {
            this.issue = string.Format(T("The assistant plugin could not be loaded: {0}"), e.Message);
        }
        finally
        {
            this.isLoading = false;
        }
        
        await base.OnInitializedAsync();
    }

    private async Task SaveAsync()
    {
        if (!this.CanSave || this.plugin is null || this.codeEditor is null)
            return;

        this.isSaving = true;
        this.issue = string.Empty;
        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            var editedLua = await this.codeEditor.GetCodeAsync();
            var result = await this.AssistantPluginInstallService.UpdateInstalledAssistantAsync(this.plugin, editedLua, CancellationToken.None);
            if (!result.Success)
            {
                LOGGER.LogError($"Failed to update assistant plugin '{result.PluginName}' ({result.PluginId}) in '{result.PluginDirectory}' with issue '{result.Issue}'.");
                this.issue = result.Issue;
                return;
            }

            this.MudDialog.Close(DialogResult.Ok(new AssistantPluginEditorDialogResult(result.PluginId, result.PluginName)));
        }
        finally
        {
            this.isSaving = false;
            if (!string.IsNullOrWhiteSpace(this.issue))
                await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private async Task ToggleFullscreenAsync()
    {
        this.isFullscreen = !this.isFullscreen;
        await this.MudDialog.SetOptionsAsync(this.isFullscreen ? this.optionsFullscreen : DialogOptions.BLOCKING_FULLSCREEN);
    }

    private void Cancel() => this.MudDialog.Cancel();
    
    private async Task CopyToClipboard() => await this.RustService.CopyText2Clipboard(this.Snackbar, this.Result2Copy());

    private static bool AreSamePath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison);
    }
}
