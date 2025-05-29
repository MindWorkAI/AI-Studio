using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class PandocDialog : ComponentBase
{
    [Inject]
    private HttpClient HttpClient { get; set; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    protected IJSRuntime JsRuntime { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("PandocDialog");
    private static readonly string LICENCE_URI = "https://raw.githubusercontent.com/jgm/pandoc/refs/heads/main/COPYING.md";
    private static string PANDOC_VERSION = string.Empty;

    private bool isPandocAvailable;
    private bool showSkeleton;
    private bool showInstallPage;
    private string? licenseText;
    private bool isLoading;
    
    #region Overrides of ComponentBase
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.showSkeleton = true;
        await this.CheckPandocAvailabilityAsync();
        PANDOC_VERSION = await Pandoc.FetchLatestVersionAsync();
    }

    #endregion
    
    private void Cancel() => this.MudDialog.Cancel();

    private async Task CheckPandocAvailabilityAsync()
    {
        var pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService);
        this.isPandocAvailable = pandocInstallation.IsAvailable;
        this.showSkeleton = false;
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task InstallPandocAsync()
    {
        await Pandoc.InstallAsync(this.RustService);
        this.MudDialog.Close(DialogResult.Ok(true));
        await this.DialogService.ShowAsync<PandocDialog>("pandoc dialog");
    }

    private void ProceedToInstallation() => this.showInstallPage = true;

    private async Task GetInstaller()
    {
        var uri = await Pandoc.GenerateInstallerUriAsync();
        var filename = this.FilenameFromUri(uri);
        await this.JsRuntime.InvokeVoidAsync("triggerDownload", uri, filename);
    }

    private async Task GetArchive()
    {
        var uri = await Pandoc.GenerateArchiveUriAsync();
        var filename = this.FilenameFromUri(uri);
        await this.JsRuntime.InvokeVoidAsync("triggerDownload", uri, filename);
    }

    private async Task RejectLicense()
    {
        var message = "Pandoc is open-source and free of charge, but if you reject Pandoc's license, it can not be installed and some of AIStudios data retrieval features will be disabled (e.g. using Office files like Word)." +
                      "This decision can be revoked at any time. Are you sure you want to reject the license?";
        
        var dialogParameters = new DialogParameters
        {
            { "Message", message },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Reject Pandoc's licence", dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            dialogReference.Close();
        else
            this.Cancel();
    }

    private string FilenameFromUri(string uri)
    {
        var index = uri.LastIndexOf('/');
        return uri[(index + 1)..];
    }

    private async Task OnExpandedChanged(bool isExpanded)
    {
        if (isExpanded)
        {
            this.isLoading = true;
            try
            {
                this.licenseText = await this.LoadLicenseTextAsync();
            }
            catch (Exception ex)
            {
                this.licenseText = "Error loading license text, please consider following the links to read the GPL.";
                LOG.LogError("Error loading GPL license text:\n{ErrorMessage}", ex.Message);
            }
            finally
            {
                this.isLoading = false;
            }
        }
        else
        {
            this.licenseText = string.Empty;
        }
    }
    
    private async Task<string> LoadLicenseTextAsync()
    {
        var response = await this.HttpClient.GetAsync(LICENCE_URI);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
}