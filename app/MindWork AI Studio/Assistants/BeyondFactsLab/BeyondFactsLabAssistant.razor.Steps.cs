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
    /// Shows the new time of change of a preparation after a work state or a result of it was stored.
    /// </summary>
    /// <param name="project">The preparation with its new time of change.</param>
    private Task ProjectStoredAsync(BflProject project)
    {
        this.ReplaceProject(project);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the research once the person confirmed the event details, on every topic without one and on every topic
    /// whose public details changed since, and tells the person.
    /// </summary>
    /// <remarks>
    /// Nothing starts for an event which is not public, nor while the work state suggests whether the event is public
    /// and the person has not decided yet; the person hears why. Taking over the suggestion changes the details, so
    /// that the person confirms the work state again, and the research starts then if the event is public.
    /// </remarks>
    /// <param name="step">The step whose work state the person confirmed.</param>
    private async Task WorkStateConfirmedAsync(BflStep step)
    {
        if (step is not BflStep.FRAMEWORK || this.selectedProject?.Project is not { } project || this.ValidateProvider(this.providerSettings) is not null)
            return;

        if (BflEventVisibility.IsNotPublic(this.forms))
        {
            await this.MessageBus.SendInfo(new(AppIcons.NOT_PUBLIC, BflEventVisibility.NotPublicMessage));
            return;
        }

        if (BflEventVisibility.IsSuggestionPending(this.workStates, this.forms))
        {
            await this.MessageBus.SendInfo(new(AppIcons.WEB_RESEARCH, string.Format(TA("The lab has not started the research on the web yet. First, decide on the suggestion below the work state whether the event is public. Afterward, you can start the research in the step '{0}'."), BflStep.STRATEGY.Title)));
            return;
        }

        var started = await this.ResearchRunner.StartWhenDueAsync(project.ProjectId, this.providerSettings, this.forms);
        if (started.Count > 0)
            await this.MessageBus.SendInfo(new(AppIcons.WEB_RESEARCH, string.Format(TA("The lab now researches the event on the web, with the public details about it only. You find the result in the step '{0}'."), BflStep.STRATEGY.Title)));
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
            await this.MessageBus.SendError(new(AppIcons.ASSISTANT_BEYOND_FACTS_LAB, TA("AI Studio could not save your details. Please try again.")));
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

        var notes = new List<string>(2);
        if (BflMissingDetails.Of(step, form).Count > 0)
            notes.Add(T("Details missing"));

        switch (BflStaleness.Of(step, this.workStates, this.forms))
        {
            case BflWorkStateStatus.OUTDATED:
                notes.Add(T("Outdated"));
                break;

            case BflWorkStateStatus.DETAILS_CHANGED:
                notes.Add(T("Details changed"));
                break;
        }

        return string.Join(" · ", notes);
    }

    /// <summary>
    /// Tells whether the stepper shows a step as done: the person confirmed its work state, and no earlier step changed
    /// since.
    /// </summary>
    /// <param name="step">The step.</param>
    private bool IsStepCompleted(BflStep step) => BflStaleness.Of(step, this.workStates, this.forms) is BflWorkStateStatus.CONFIRMED or BflWorkStateStatus.DETAILS_CHANGED;
}