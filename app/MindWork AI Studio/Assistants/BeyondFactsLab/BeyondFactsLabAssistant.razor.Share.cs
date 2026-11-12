using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

public partial class BeyondFactsLabAssistant
{
    /// <summary>
    /// Opens the dialog with which the person shares the preparation shown with the research team of the lab.
    /// </summary>
    private Task ShareAsync() => this.ShowShareDialogAsync(false);

    /// <summary>
    /// Offers to share the preparation once the person confirmed the work state of the last step for the first time,
    /// as the lab is done then; confirming it again later asks no more.
    /// </summary>
    /// <param name="projectId">The ID of the preparation.</param>
    private async Task OfferSharingAsync(Guid projectId)
    {
        try
        {
            var workState = await this.Store.LoadWorkStateAsync(projectId, BflStep.RESULTS);
            if (workState is not { LastRevision: 1, Confirmed: not null })
                return;
        }
        catch (InvalidDataException exception)
        {
            this.Logger.LogWarning(exception, "Could not read the work state of the last step of a preparation in the BeyondFacts Lab.");
            return;
        }

        await this.ShowShareDialogAsync(true);
    }

    /// <summary>
    /// Shows the dialog with which the person shares the preparation shown.
    /// </summary>
    /// <param name="afterCompletion">Whether the lab asks right after the last step, so that it thanks the person first.</param>
    private async Task ShowShareDialogAsync(bool afterCompletion)
    {
        if (this.selectedProject?.Project is not { } project)
            return;

        var parameters = new DialogParameters<BflShareDialog>
        {
            { dialog => dialog.ProjectId, project.ProjectId },
            { dialog => dialog.AfterCompletion, afterCompletion },
        };

        await this.DialogService.ShowAsync<BflShareDialog>(T("Share with the research team"), parameters, DialogOptions.FULLSCREEN);
    }
}