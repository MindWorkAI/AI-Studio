using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class PandocDialog : ComponentBase
{
    [Inject]
    private HttpClient HttpClient { get; set; } = null!;
    
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;
    
    private static readonly string LICENCE_URI = "https://raw.githubusercontent.com/jgm/pandoc/master/COPYRIGHT";

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
    }

    #endregion
    
    private void Cancel() => this.MudDialog.Cancel();

    private async Task CheckPandocAvailabilityAsync()
    {
        this.isPandocAvailable = await Pandoc.CheckAvailabilityAsync();
        this.showSkeleton = false;
        await this.InvokeAsync(this.StateHasChanged);
    }

    private void ProceedToInstallation() => this.showInstallPage = true;
    
    private async Task OnExpandedChanged(bool newVal)
    {
        if (newVal)
        {
            this.isLoading = true;
            try
            {
                await Task.Delay(600);

                this.licenseText = await this.LoadLicenseTextAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Laden des Lizenztexts: {ex.Message}");
                this.licenseText = "Fehler beim Laden des Lizenztexts.";
            }
            finally
            {
                this.isLoading = false;
            }
        }
        else
        {
            await Task.Delay(350);
            this.licenseText = string.Empty;
        }
    }
    
    private async Task<string> LoadLicenseTextAsync()
    {
        var response = await this.HttpClient.GetAsync(LICENCE_URI);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return content;
    }
}