using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class SelectFile : MSGComponentBase
{
    [Parameter]
    public string File { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> FileChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string FileDialogTitle { get; set; } = "Select File";
    
    [Parameter]
    public FileTypeFilter? Filter { get; set; }
    
    [Parameter]
    public Func<string, string?> Validation { get; set; } = _ => null;

    [Inject]
    public RustService RustService { get; set; } = null!;
    
    [Inject]
    protected ILogger<SelectDirectory> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        await base.OnInitializedAsync();
    }

    #endregion

    private void InternalFileChanged(string file)
    {
        this.File = file;
        this.FileChanged.InvokeAsync(file);
    }

    private async Task OpenFileDialog()
    {
        var response = await this.RustService.SelectFile(this.FileDialogTitle, this.Filter, string.IsNullOrWhiteSpace(this.File) ? null : this.File);
        this.Logger.LogInformation($"The user selected the file '{response.SelectedFilePath}'.");

        if (!response.UserCancelled)
            this.InternalFileChanged(response.SelectedFilePath);
    }
}