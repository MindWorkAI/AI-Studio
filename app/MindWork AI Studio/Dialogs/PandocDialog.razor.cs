using System.Reflection;

using AIStudio.Tools.Metadata;
using AIStudio.Tools.Services;
using Microsoft.AspNetCore.Components;

using SharedTools;

namespace AIStudio.Dialogs;

public partial class PandocDialog : ComponentBase
{
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataArchitectureAttribute META_DATA_ARCH = ASSEMBLY.GetCustomAttribute<MetaDataArchitectureAttribute>()!;
    private static readonly RID CPU_ARCHITECTURE = META_DATA_ARCH.Architecture.ToRID();
    
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
    private bool showInstallationPage;
    private string? licenseText;
    private bool isLoadingLicence;
    private int selectedInstallerIndex = SelectInstallerIndex();
    private int selectedArchiveIndex = SelectArchiveIndex();
    private string downloadUrlArchive = string.Empty;
    private string downloadUrlInstaller = string.Empty;
    
    #region Overrides of ComponentBase
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        LATEST_PANDOC_VERSION = await Pandoc.FetchLatestVersionAsync();
        await this.CheckPandocAvailabilityAsync();
    }

    #endregion
    
    private void Cancel() => this.MudDialog.Cancel();

    private async Task CheckPandocAvailabilityAsync()
    {
        this.pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService);
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task InstallPandocAsync()
    {
        await Pandoc.InstallAsync(this.RustService);
        this.MudDialog.Close(DialogResult.Ok(true));
        
        await this.DialogService.ShowAsync<PandocDialog>("pandoc dialog");
    }

    private void ProceedToInstallation() => this.showInstallationPage = true;

    private async Task RejectLicense()
    {
        var message = "Pandoc is open-source and free of charge, but if you reject Pandoc's license, it can not be installed and some of AIStudios data retrieval features will be disabled (e.g. using Office files like Word). This decision can be revoked at any time. Are you sure you want to reject the license?";
        var dialogParameters = new DialogParameters
        {
            { "Message", message },
        };
        
        var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Reject Pandoc's Licence", dialogParameters, DialogOptions.FULLSCREEN);
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
                this.licenseText = "Error loading license text, please consider following the links to read the GPL.";
                LOG.LogError("Error loading GPL license text:\n{ErrorMessage}", ex.Message);
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