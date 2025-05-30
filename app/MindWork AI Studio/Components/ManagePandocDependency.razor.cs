using AIStudio.Dialogs;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Components;

public partial class ManagePandocDependency : MSGComponentBase
{
    [Parameter]
    public string IntroText { get; set; } = string.Empty;
    
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    private RustService RustService { get; init; } = null!;

    private PandocInstallation pandocInstallation;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        this.pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService, false);
    }

    #endregion

    private Color DetermineButtonColor()
    {
        if (this.pandocInstallation == default)
            return Color.Default;
        
        switch (this.pandocInstallation)
        {
            case { CheckWasSuccessful: true, IsAvailable: true }:
                return Color.Default;
            
            case { CheckWasSuccessful: true, IsAvailable: false }:
                return Color.Warning;
            
            case { CheckWasSuccessful: false }:
                return Color.Error;
        }
    }
    
    private string DetermineButtonText()
    {
        if(this.pandocInstallation == default)
            return T("Please wait while we check the availability of Pandoc.");
        
        switch (this.pandocInstallation)
        {
            case { CheckWasSuccessful: true, IsAvailable: true }:
                return T("Check your Pandoc installation");
            
            case { CheckWasSuccessful: true, IsAvailable: false }:
                return T("Update Pandoc");
            
            case { CheckWasSuccessful: false }:
                return T("Install Pandoc");
        }
    }

    private string DetermineIntroText()
    {
        if (this.pandocInstallation == default)
            return $"{this.IntroText} {T("Please wait while we check the availability of Pandoc.")}";
        
        switch (this.pandocInstallation)
        {
            case { CheckWasSuccessful: true, IsAvailable: true }:
                return $"{this.IntroText} {T("Your Pandoc installation meets the requirements.")}";
            
            case { CheckWasSuccessful: true, IsAvailable: false }:
                return $"{this.IntroText} {T("Your Pandoc installation is outdated. Please update it to the latest version to ensure compatibility with all features.")}";
            
            case { CheckWasSuccessful: false }:
                return $"{this.IntroText} {T("Pandoc is not installed or not available. Please install it to use the features that require Pandoc.")}";
        }
    }
    
    private async Task ShowPandocDialogAsync()
    {
        var dialogReference = await this.DialogService.ShowAsync<PandocDialog>(T("Pandoc Installation"), DialogOptions.FULLSCREEN);
        await dialogReference.Result;
        
        // Refresh the availability of Pandoc after the dialog is closed:
        this.pandocInstallation = await Pandoc.CheckAvailabilityAsync(this.RustService, false);
        
        await this.InvokeAsync(this.StateHasChanged);
    }
}