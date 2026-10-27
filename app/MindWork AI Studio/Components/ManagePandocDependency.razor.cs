using AIStudio.Dialogs;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

/// <summary>
/// Asks to install or update Pandoc where a feature needs it for some files only, such as the HTML files
/// of a local data source. It stays hidden while the check runs and once Pandoc is ready, so it does not
/// distract from the rest of the dialog.
/// </summary>
public partial class ManagePandocDependency : MSGComponentBase
{
    [Parameter]
    public string IntroText { get; set; } = string.Empty;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;

    // Null means the silent availability check has not completed yet.
    private PandocInstallation? pandocInstallation;

    internal static bool ShouldShowInstallationPrompt(PandocInstallation? installation) => installation is { IsAvailable: false };

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService, false);
    }

    #endregion

    //
    // The prompt shows up only for an installation which is not available, so it is either outdated,
    // when the check found one, or missing:
    //
    private bool IsOutdated => this.pandocInstallation is { CheckWasSuccessful: true };

    private Color ButtonColor => this.IsOutdated ? Color.Warning : Color.Error;

    private string ButtonText => this.IsOutdated ? T("Update Pandoc") : T("Install Pandoc");

    private string PromptText => this.IsOutdated
        ? $"{this.IntroText} {T("Your Pandoc installation is outdated. Please update it to the latest version to ensure compatibility with all features.")}"
        : $"{this.IntroText} {T("Pandoc is not installed or not available. Please install it to use the features that require Pandoc.")}";

    private async Task ShowPandocDialogAsync()
    {
        var dialogReference = await this.DialogService.ShowAsync<PandocDialog>(T("Pandoc Installation"), DialogOptions.FULLSCREEN);
        await dialogReference.Result;
        
        // Refresh the availability of Pandoc after the dialog is closed:
        this.pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService, false);
        
        await this.InvokeAsync(this.StateHasChanged);
    }
}