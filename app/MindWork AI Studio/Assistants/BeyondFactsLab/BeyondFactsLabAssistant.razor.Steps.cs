namespace AIStudio.Assistants.BeyondFactsLab;

public partial class BeyondFactsLabAssistant
{
    /// <summary>
    /// The forms of the preparation shown, by their steps. A step missing here has a form this version cannot open.
    /// </summary>
    private Dictionary<BflStep, BflStepFormData> forms = [];

    /// <summary>
    /// The work states of the preparation shown, by their steps. A step missing here has a work state this version
    /// cannot open.
    /// </summary>
    private Dictionary<BflStep, BflWorkState> workStates = [];

    /// <summary>
    /// The step shown.
    /// </summary>
    private BflStep activeStep = BflStep.FRAMEWORK;

    /// <summary>
    /// Gets the position of the step shown in the stepper, which lists the steps in the order of their values.
    /// </summary>
    private int ActiveStepIndex => (int)this.activeStep;

    /// <summary>
    /// Shows a preparation with its forms, at the step the person looked at last.
    /// </summary>
    /// <param name="project">The preparation, or null to show none.</param>
    private async Task ShowProjectAsync(BflProjectEntry? project)
    {
        this.selectedProject = project;
        this.ResolveProvider(project?.Project);
        if (project?.Project is { } openedProject)
        {
            this.forms = new(await this.Store.LoadFormsAsync(openedProject.ProjectId));
            this.workStates = new(await this.Store.LoadWorkStatesAsync(openedProject.ProjectId));
            this.activeStep = openedProject.CurrentStep;
        }
        else
        {
            this.forms = [];
            this.workStates = [];
            this.activeStep = BflStep.FRAMEWORK;
        }
    }

    /// <summary>
    /// Shows a work state after the panel of its step stored it.
    /// </summary>
    /// <param name="project">The preparation with its new time of change.</param>
    private Task WorkStateStoredAsync(BflProject project)
    {
        this.ReplaceProject(project);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads the work states of the preparation shown again once the generator started, ended, or stored a draft.
    /// </summary>
    /// <param name="location">The step whose work state changed.</param>
    private Task OnWorkStateChangedAsync(BflStepThreadLocation location)
    {
        if (this.selectedProject?.ProjectId != location.ProjectId)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            this.workStates = new(await this.Store.LoadWorkStatesAsync(location.ProjectId));
            if (await this.Store.LoadAsync(location.ProjectId) is { } project)
                this.ReplaceProject(project);

            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Shows the step the person chose in the stepper, and remembers it for the next time.
    /// </summary>
    /// <param name="index">The position of the step in the stepper.</param>
    private async Task SelectStepAsync(int index)
    {
        var steps = BflStep.All;
        if (index < 0 || index >= steps.Count || steps[index] == this.activeStep)
            return;

        this.activeStep = steps[index];
        this.scrollToActiveStep = true;
        if (this.selectedProject?.Project is not { } project)
            return;

        try
        {
            this.ReplaceProject(await this.Store.SetCurrentStepAsync(project.ProjectId, this.activeStep));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // Only the step to open next time is lost, which is not worth bothering the person with:
            this.Logger.LogWarning(exception, "Could not remember the step shown last in the BeyondFacts Lab.");
        }
    }

    /// <summary>
    /// Stores the form of a step after the person changed a value.
    /// </summary>
    /// <remarks>
    /// Unlike the other changes, saving leaves the page usable: the fields stay enabled, so that the next field
    /// keeps the focus while the previous one is stored.
    /// </remarks>
    /// <param name="step">The step.</param>
    private async Task SaveFormAsync(BflStep step)
    {
        if (this.selectedProject?.Project is not { } project || !this.forms.TryGetValue(step, out var form))
            return;

        try
        {
            this.ReplaceProject(await this.Store.SaveFormAsync(project.ProjectId, step, form));
            if (BflMissingDetails.StepsOf(this.forms).Count > 0)
                this.projectsWithMissingDetails.Add(project.ProjectId);
            else
                this.projectsWithMissingDetails.Remove(project.ProjectId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            this.Logger.LogError(exception, "Could not save the details of a step in the BeyondFacts Lab.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.RecordVoiceOver, TA("AI Studio could not save your details. Please try again.")));
        }
    }

    /// <summary>
    /// Puts a preparation the store just wrote into the list, without reading the list again.
    /// </summary>
    /// <param name="project">The preparation.</param>
    private void ReplaceProject(BflProject project)
    {
        var entry = BflProjectEntry.FromProject(project);
        this.projects = [.. this.projects.Select(candidate => candidate.ProjectId == project.ProjectId ? entry : candidate).OrderByDescending(candidate => candidate.ModifiedAtUtc)];
        if (this.selectedProject?.ProjectId == project.ProjectId)
            this.selectedProject = entry;
    }

    /// <summary>
    /// Gets the line below the title of a step in the stepper.
    /// </summary>
    /// <param name="step">The step.</param>
    private string StepSecondaryText(BflStep step)
    {
        if (!this.forms.TryGetValue(step, out var form))
            return T("Cannot be opened");

        return BflMissingDetails.Of(step, form).Count > 0 ? T("Details missing") : string.Empty;
    }
}