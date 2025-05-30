using System.Reflection;

using AIStudio.Components;
using AIStudio.Tools.Metadata;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

using SharedTools;

namespace AIStudio.Dialogs;

public partial class PandocDialog : MSGComponentBase
{
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataArchitectureAttribute META_DATA_ARCH = ASSEMBLY.GetCustomAttribute<MetaDataArchitectureAttribute>()!;
    private static readonly RID CPU_ARCHITECTURE = META_DATA_ARCH.Architecture.ToRID();
    
    [Parameter]
    public bool ShowInstallationPage { get; set; }

    [Parameter]
    public bool ShowInitialResultInSnackbar { get; set; } = true;
    
    [Inject]
    private HttpClient HttpClient { get; set; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("PandocDialog");
    private static readonly string LICENCE_URI = "https://raw.githubusercontent.com/jgm/pandoc/refs/heads/main/COPYING.md";
    private static string LATEST_PANDOC_VERSION = string.Empty;

    private PandocInstallation pandocInstallation;
    private string? licenseText;
    private bool isLoadingLicence;
    private bool isInstallationInProgress;
    private int selectedInstallerIndex = SelectInstallerIndex();
    private int selectedArchiveIndex = SelectArchiveIndex();
    private string downloadUrlArchive = string.Empty;
    private string downloadUrlInstaller = string.Empty;
    
    #region Overrides of ComponentBase
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        LATEST_PANDOC_VERSION = await Pandoc.FetchLatestVersionAsync();
        await this.CheckPandocAvailabilityAsync(this.ShowInitialResultInSnackbar);
    }

    #endregion
    
    private void Cancel() => this.MudDialog.Cancel();

    private async Task CheckPandocAvailabilityAsync(bool useSnackbar)
    {
        this.pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService, useSnackbar);
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task InstallPandocAsync()
    {
        this.isInstallationInProgress = true;
        this.StateHasChanged();
        
        await Pandoc.InstallAsync(this.RustService);
        
        this.isInstallationInProgress = false;
        this.MudDialog.Close(DialogResult.Ok(true));
        await this.DialogService.ShowAsync<PandocDialog>("Pandoc Installation", DialogOptions.FULLSCREEN);
    }

    private void ProceedToInstallation() => this.ShowInstallationPage = true;

    private async Task RejectLicense()
    {
        var message = T("Pandoc is open-source and free, but if you reject its license, you can't install it and some MindWork AI Studio features will be limited (like the integration of Office files) or unavailable (like the generation of Office files). You can change your decision anytime. Are you sure you want to reject the license?");
        var dialogParameters = new DialogParameters
        {
            { "Message", message },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Reject Pandoc's Licence"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled)
            dialogReference.Close();
        else
            this.Cancel();
    }
    
    private async Task WhenExpandingManualInstallation(bool isExpanded)
    {
        if(string.IsNullOrWhiteSpace(this.downloadUrlArchive))
            this.downloadUrlArchive = await Pandoc.GenerateArchiveUriAsync();
        
        if(string.IsNullOrWhiteSpace(this.downloadUrlInstaller))
            this.downloadUrlInstaller = await Pandoc.GenerateInstallerUriAsync();
    }

    private async Task WhenExpandingLicence(bool isExpanded)
    {
        if (isExpanded)
        {
            this.isLoadingLicence = true;
            try
            {
                this.licenseText = await this.LoadLicenseTextAsync();
            }
            catch (Exception ex)
            {
                this.licenseText = T("Error loading license text, please consider following the links to read the GPL.");
                LOG.LogError("Error loading GPL license text: {ErrorMessage}", ex.Message);
            }
            finally
            {
                this.isLoadingLicence = false;
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

    // ReSharper disable RedundantSwitchExpressionArms
    private static int SelectInstallerIndex() => CPU_ARCHITECTURE switch
    {
        RID.OSX_ARM64 => 1,
        RID.OSX_X64 => 2,
        
        RID.WIN_ARM64 => 0,
        RID.WIN_X64 => 0,
        
        _ => 0,
    };
    
    private static int SelectArchiveIndex() => CPU_ARCHITECTURE switch
    {
        RID.OSX_ARM64 => 1,
        RID.OSX_X64 => 1,
        
        RID.WIN_ARM64 => 0,
        RID.WIN_X64 => 0,
        
        RID.LINUX_ARM64 => 2,
        RID.LINUX_X64 => 2,
        
        _ => 0,
    };
    // ReSharper restore RedundantSwitchExpressionArms
}