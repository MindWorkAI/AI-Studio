using AIStudio.Dialogs;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.ModelComparison;

public partial class AssistantModelComparison : AssistantBaseCore<SettingsDialogModelComparison>
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PromptInjectionGuardService PromptInjectionGuard { get; init; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    protected override Tools.Components Component => Tools.Components.MODEL_COMPARISON_ASSISTANT;

    protected override string Title => ModelComparisonMode.STABILITY.PageTitle;

    protected override string Description => TA("Send the same request to two models, once or many times, and compare their answers side by side. You vote blindly for the better answer, see how much the answers vary, and learn at the end which model wrote which answer.");

    protected override string SystemPrompt => string.Empty;

    // The answers of this assistant are shown and judged on its own page, so no chat continues its conversations:
    protected override ChatSystemPromptChoice ChatSystemPrompt => ChatSystemPromptChoice.AI_STUDIO_DEFAULT;

    protected override bool AllowProfiles => false;

    protected override bool ShowResult => false;

    /// <remarks>
    /// The footer of the base class offers to send the result to another assistant, and to copy it.
    /// Both work on the one result the base class knows about, and this assistant has none: its
    /// answers are shown by the page itself. Each answer has a copy button of its own.
    /// </remarks>
    protected override bool ShowSendTo => false;

    protected override bool ShowCopyResult => false;

    /// <remarks>
    /// Two models and a judge: the footer would show the confidence of the first model only. Each model selection
    /// shows the confidence of its own model instead.
    /// </remarks>
    protected override bool ShowProviderConfidence => false;

    protected override string SubmitText => T("Compare");

    /// <summary>
    /// Once there are results, the request is not editable, so there is nothing to submit. The way
    /// to the next comparison is the button below the summary.
    /// </summary>
    /// <remarks>
    /// The button of the base class cannot be hidden, and repurposing it would start a session of
    /// the base class just to switch the view, with the waiting animation flashing by.
    /// </remarks>
    protected override bool SubmitDisabled => this.ShowsBatch;

    protected override Func<Task> SubmitAction => this.Compare;

    /// <remarks>
    /// Starts over with the selected preset as it is stored: OnDefaultsAppliedAsync fills the form with it right after
    /// this ran.
    /// </remarks>
    protected override void ResetForm()
    {
        this.hasAttemptedCompare = false;
        this.isComparing = false;
        this.ClearBatch();
    }

    /// <summary>
    /// How many independent runs to make of the same comparison. One is the plain comparison this
    /// assistant always offered; more repeat the same request several times, to see how consistently
    /// the two models -- and the judge -- come out the same way.
    /// </summary>
    private int runCount = 1;

    /// <summary>
    /// The batch which runs or ran, or null while the form is shown.
    /// </summary>
    private ModelComparisonBatch? batch;

    /// <summary>
    /// Whether the batch is being asked right now: at least one run is still in flight. Set once the
    /// form has been validated, and not when the session starts: the base class marks the session as
    /// processing before the request has been checked, and the form must not be taken away before
    /// that.
    /// </summary>
    private bool isComparing;

    /// <summary>
    /// Whether the page shows the batch instead of the form: while it runs, and once runs arrived.
    /// </summary>
    private bool ShowsBatch => this.isComparing || this.batch is { Entries.Count: > 0 };

    /// <summary>
    /// A render already queued on the UI dispatcher, if one is still pending. Reused by RequestBackgroundUIRefresh, so
    /// a burst of runs arriving close together queues at most one more render instead of one per run.
    /// </summary>
    private Task? pendingBackgroundRefresh;

    /// <summary>
    /// Asks for a UI refresh the same way RefreshAssistantUIAsync does, but skips
    /// asking again while one is already queued.
    /// </summary>
    /// <remarks>
    /// Blazor Server processes every queued render, and every click, on the same one dispatcher queue, each in its own
    /// turn. With up to ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS runs arriving close together, a render per run
    /// queues up several of them ahead of whatever the user just clicked -- the vote button above all -- which is
    /// exactly the multi-second stall this avoids. A run which arrives while a render is already queued does not need
    /// one of its own: the queued render still runs after this run was added to the batch, so it shows it all the same.
    /// </remarks>
    private void RequestBackgroundUIRefresh()
    {
        if (this.pendingBackgroundRefresh is { IsCompleted: false })
            return;

        this.pendingBackgroundRefresh = this.RefreshAssistantUIAsync();
    }

    /// <summary>
    /// Forgets the current batch, which brings the form back.
    /// </summary>
    private void ClearBatch() => this.batch = null;

    private string inputContext = string.Empty;
    private string inputQuestion = string.Empty;

    /// <summary>
    /// What both models are asked, as the user entered it right now. Both models, and the judge with
    /// them, are asked to answer in the app's active language, regardless of what language the
    /// question itself is written in.
    /// </summary>
    private ModelComparisonRequest CurrentRequest => new(this.inputContext, this.inputQuestion, ModelComparisonFormat.LanguageName);

    protected override bool MightPreselectValues() => false;

    /// <summary>
    /// Fills the form with the selected preset, once the page opens and after every reset. The page always works in a
    /// preset: without any, it adds one.
    /// </summary>
    /// <remarks>
    /// The base class preselects the first model through GetPreselectedProvider by then, which knows nothing of the
    /// presets. An assistant session which the base class restores afterward brings back its own form and preset, see
    /// RestoreCustomAssistantSessionState.
    /// </remarks>
    protected override async Task OnDefaultsAppliedAsync()
    {
        if (this.SelectedPreset is null)
            this.selectedPresetId = ModelComparisonPresetChoice.Choose(this.presets, this.SettingsManager.ConfigurationData.ModelComparison) ?? await this.AddNewPreset();

        this.LoadSelectedPreset();
    }

    /// <summary>
    /// The first model is ProviderSettings of the base class, so that everything the
    /// base class does with a provider keeps working for it. This is the second one.
    /// </summary>
    private AIStudio.Settings.Provider secondProvider = Settings.Provider.NONE;

    /// <summary>
    /// Whether an optional third model judges the two answers. On from the start only when the options of the
    /// assistant preselect a judge model, see OnDefaultsAppliedAsync.
    /// </summary>
    private bool judgeEnabled;

    /// <summary>
    /// Whether the panel of the judge is open. A preset with a judge opens it, so that the user sees the judge at once.
    /// </summary>
    private bool judgePanelExpanded;

    /// <summary>
    /// The judge model, asked only when judgeEnabled is set.
    /// </summary>
    private AIStudio.Settings.Provider judgeProvider = Settings.Provider.NONE;

    /// <summary>
    /// The expectations, values, and goals of the user, in their own words, by which the judge decides which answer is
    /// better, see ModelComparisonJudgeExpectations. Sent to the judge together with the request and both answers, but
    /// the judge never learns which model wrote which answer.
    /// </summary>
    private string judgeInstructions = string.Empty;

    /// <summary>
    /// Whether the user confirmed that the judge may decide by its own standards, which counts only while
    /// judgeInstructions is empty. Not part of a preset: whoever describes nothing confirms that once per session.
    /// </summary>
    private bool judgeOwnStandardsAccepted;

    /// <summary>
    /// Whether the user has tried to submit the form at least once. The judge's provider field mounts
    /// only once the judge is switched on, which can happen well after the form itself first
    /// rendered, and MudForm would otherwise mark it invalid on sight, before the user had any chance
    /// to pick one. ValidatingJudgeProvider stays silent until this is set, regardless
    /// of when MudForm itself happens to call it.
    /// </summary>
    private bool hasAttemptedCompare;

    /// <summary>
    /// Whether the summary of the request (the question preview and the context's length) is
    /// expanded, while a batch is shown. Collapsed by default: useful to check back on, not
    /// something to stare at while a batch runs.
    /// </summary>
    private bool requestSummaryExpanded;

    /// <summary>
    /// The presets as they are stored, sorted by name when the store listed them last.
    /// </summary>
    private List<ModelComparisonPreset> presets = [];

    /// <summary>
    /// The ID of the preset the form shows and changes, or null when the store could not provide any.
    /// </summary>
    private Guid? selectedPresetId;

    /// <summary>
    /// What the name field shows for the selected preset, which may be a name not valid yet.
    /// </summary>
    private string presetName = string.Empty;

    /// <summary>
    /// The preset selectedPresetId points to, or null when there is none.
    /// </summary>
    private ModelComparisonPreset? SelectedPreset => this.presets.FirstOrDefault(preset => preset.Id == this.selectedPresetId);

    /// <summary>
    /// Whether the presets may change right now: not while the models are being asked.
    /// </summary>
    private bool ArePresetControlsDisabled => this.IsProcessing;

    protected override async Task OnInitializedAsync()
    {
        // The presets come first: the base class fills the form with one of them, see OnDefaultsAppliedAsync.
        await this.ReloadPresets();
        await base.OnInitializedAsync();
    }

    /// <summary>
    /// Stores a change of the form, a moment after the user stopped typing.
    /// </summary>
    protected override async Task OnFormChange() => await this.StorePresetIfChanged();

    /// <summary>
    /// Stores what the form changed, also when the user leaves the page before the form noticed the change.
    /// </summary>
    /// <remarks>
    /// The store serves the whole app, so it finishes this even after the page is gone. A newer change of the same
    /// preset, e.g., after the user came back right away, is stored after it, since the store writes each preset one
    /// after the other.
    /// </remarks>
    protected override void DisposeResources()
    {
        this.StorePresetIfChanged().Observe("Model Comparison: storing the preset when leaving the page");
        base.DisposeResources();
    }

    /// <summary>
    /// Lists the presets anew.
    /// </summary>
    private async Task ReloadPresets()
    {
        try
        {
            this.presets = [.. await this.ModelComparisonStore.ListPresetsAsync()];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogWarning(exception, "Could not list the presets of the Model Comparison assistant.");
        }
    }

    /// <summary>
    /// Stores a change of the presets.
    /// </summary>
    /// <param name="change">The change: a save or a delete in the store.</param>
    /// <returns>Whether the change was stored.</returns>
    private async Task<bool> ChangePresets(Func<Task> change)
    {
        try
        {
            await change();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not store a change of the presets of the Model Comparison assistant.");
            await MessageBus.INSTANCE.SendError(new(AppIcons.ASSISTANT_MODEL_COMPARISON, TA("AI Studio could not save the change to your presets. Please try again.")));
            return false;
        }
    }

    /// <summary>
    /// Captures the form as it stands right now as a preset.
    /// </summary>
    /// <param name="id">The ID of the preset.</param>
    /// <param name="name">The name of the preset.</param>
    /// <returns>The preset.</returns>
    private ModelComparisonPreset PresetFromForm(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        FirstProviderId = this.ProviderSettings.Id,
        SecondProviderId = this.secondProvider.Id,
        JudgeEnabled = this.judgeEnabled,
        JudgeProviderId = this.judgeProvider.Id,
        JudgeInstructions = this.judgeInstructions,
        Context = this.inputContext,
        Question = this.inputQuestion,
        RunCount = this.runCount,
    };

    /// <summary>
    /// Fills the form with the selected preset.
    /// </summary>
    private void LoadSelectedPreset()
    {
        var preset = this.SelectedPreset;
        if (preset is null)
        {
            this.presetName = string.Empty;
            return;
        }

        this.ProviderSettings = this.SettingsManager.GetProviderById(preset.FirstProviderId);
        this.secondProvider = this.SettingsManager.GetProviderById(preset.SecondProviderId);
        this.judgeEnabled = preset.JudgeEnabled;
        this.judgePanelExpanded = preset.JudgeEnabled;
        this.judgeProvider = this.SettingsManager.GetProviderById(preset.JudgeProviderId);
        this.judgeInstructions = preset.JudgeInstructions;
        this.inputContext = preset.Context;
        this.inputQuestion = preset.Question;
        this.runCount = preset.RunCount;
        this.presetName = preset.Name;
    }

    /// <summary>
    /// Stores the form into the selected preset, when it changed anything about it.
    /// </summary>
    /// <remarks>
    /// A name which is not valid yet, e.g., an empty one while the user retypes it, is not stored: the preset keeps
    /// the name it was stored with until the new one is valid.
    /// </remarks>
    private async Task StorePresetIfChanged()
    {
        if (this.SelectedPreset is not { } storedPreset)
            return;

        var name = this.ValidatingPresetName(this.presetName) is null ? this.presetName.Trim() : storedPreset.Name;
        var changedPreset = this.PresetFromForm(storedPreset.Id, name);
        if (changedPreset == storedPreset)
            return;

        if (!await this.ChangePresets(() => this.ModelComparisonStore.SavePresetAsync(changedPreset)))
            return;

        // Only now, so that a store which failed is tried again with the next change. The list may have been listed anew meanwhile:
        var index = this.presets.FindIndex(preset => preset.Id == changedPreset.Id);
        if (index >= 0)
            this.presets[index] = changedPreset;
    }

    /// <summary>
    /// Switches the form to another preset, once the changes of the current one are stored.
    /// </summary>
    /// <param name="presetId">The ID of the preset.</param>
    private async Task SelectedPresetChanged(Guid presetId)
    {
        if (this.ArePresetControlsDisabled || presetId == this.selectedPresetId || this.presets.All(preset => preset.Id != presetId))
            return;

        await this.StorePresetIfChanged();
        await this.SelectPreset(presetId);
    }

    /// <summary>
    /// Fills the form with a preset and forgets the complaints about the previous one.
    /// </summary>
    /// <param name="presetId">The ID of the preset.</param>
    private async Task SelectPreset(Guid? presetId)
    {
        this.selectedPresetId = presetId;
        this.LoadSelectedPreset();
        this.hasAttemptedCompare = false;
        if (this.Form is not null)
            await this.Form.ResetValidationAsync();

        this.ClearInputIssues();
    }

    /// <summary>
    /// Adds a preset and switches the form to it, once the changes of the current one are stored.
    /// </summary>
    private async Task AddPreset()
    {
        if (this.ArePresetControlsDisabled)
            return;

        await this.StorePresetIfChanged();
        if (await this.AddNewPreset() is { } presetId)
            await this.SelectPreset(presetId);
    }

    /// <summary>
    /// Stores a new preset with the models which the options of the assistant preselect, and lists the presets anew.
    /// </summary>
    /// <returns>The ID of the new preset, or null when it could not be stored.</returns>
    private async Task<Guid?> AddNewPreset()
    {
        var judge = this.SettingsManager.PreselectedComparisonJudge;
        var preset = new ModelComparisonPreset
        {
            Id = Guid.NewGuid(),

            // With seconds, so that two presets added right after each other do not get the same name, which has to be unique:
            Name = string.Format(DisplayCulture, T("Preset from {0}"), DateTime.Now),
            FirstProviderId = this.SettingsManager.PreselectedComparisonModel.Id,
            JudgeEnabled = judge != Settings.Provider.NONE,
            JudgeProviderId = judge.Id,
        };

        if (!await this.ChangePresets(() => this.ModelComparisonStore.SavePresetAsync(preset)))
            return null;

        await this.ReloadPresets();
        return preset.Id;
    }

    /// <summary>
    /// Gets the name the list shows for a preset: for the selected one, the name the user is typing, so that the list
    /// never shows an outdated name. The preset itself is stored a moment later, see StorePresetIfChanged.
    /// </summary>
    /// <param name="preset">The preset.</param>
    private string ListedName(ModelComparisonPreset preset) => preset.Id == this.selectedPresetId && !string.IsNullOrWhiteSpace(this.presetName)
        ? this.presetName.Trim()
        : preset.Name;

    /// <summary>
    /// Deletes the selected preset, after asking: a preset can carry a long material text that took real effort to
    /// assemble, and one click by mistake would lose it for good. The form then switches to another preset.
    /// </summary>
    private async Task DeletePreset()
    {
        if (this.ArePresetControlsDisabled || this.SelectedPreset is not { } preset)
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, string.Format(DisplayCulture, TA("Are you sure you want to delete the preset '{0}'? This cannot be undone."), preset.Name) },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete preset"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        if (!await this.ChangePresets(() => this.ModelComparisonStore.DeletePresetAsync(preset.Id)))
            return;

        await this.ReloadPresets();
        await this.SelectPreset(ModelComparisonPresetChoice.Choose(this.presets, this.SettingsManager.ConfigurationData.ModelComparison) ?? await this.AddNewPreset());
    }

    /// <summary>
    /// Checks the name of the selected preset, the same way the Document Analysis checks the names of its policies.
    /// </summary>
    private string? ValidatingPresetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return TA("Please provide a name for your preset.");

        var trimmedName = name.Trim();
        if (trimmedName.Length is > 60 or < 6)
            return TA("The name of your preset must be between 6 and 60 characters long.");

        if (ModelComparisonNames.IsTaken(trimmedName, this.presets.Where(preset => preset.Id != this.selectedPresetId).Select(preset => preset.Name)))
            return TA("A preset with this name already exists. Please choose a different name.");

        return null;
    }

    private static readonly AssistantSessionStateKey<string> INPUT_CONTEXT_STATE_KEY = new(nameof(inputContext));
    private static readonly AssistantSessionStateKey<string> INPUT_QUESTION_STATE_KEY = new(nameof(inputQuestion));
    private static readonly AssistantSessionStateKey<bool> IS_COMPARING_STATE_KEY = new(nameof(isComparing));
    private static readonly AssistantSessionStateKey<int> RUN_COUNT_STATE_KEY = new(nameof(runCount));
    private static readonly AssistantSessionStateKey<ModelComparisonBatch?> BATCH_STATE_KEY = new(nameof(batch));
    private static readonly AssistantSessionStateKey<AIStudio.Settings.Provider> SECOND_PROVIDER_STATE_KEY = new(nameof(secondProvider));
    private static readonly AssistantSessionStateKey<bool> JUDGE_ENABLED_STATE_KEY = new(nameof(judgeEnabled));
    private static readonly AssistantSessionStateKey<AIStudio.Settings.Provider> JUDGE_PROVIDER_STATE_KEY = new(nameof(judgeProvider));
    private static readonly AssistantSessionStateKey<string> JUDGE_INSTRUCTIONS_STATE_KEY = new(nameof(judgeInstructions));
    private static readonly AssistantSessionStateKey<bool> JUDGE_OWN_STANDARDS_ACCEPTED_STATE_KEY = new(nameof(judgeOwnStandardsAccepted));
    private static readonly AssistantSessionStateKey<Guid?> SELECTED_PRESET_ID_STATE_KEY = new(nameof(selectedPresetId));
    private static readonly AssistantSessionStateKey<string> PRESET_NAME_STATE_KEY = new(nameof(presetName));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(INPUT_CONTEXT_STATE_KEY, this.inputContext);
        state.Set(INPUT_QUESTION_STATE_KEY, this.inputQuestion);
        state.Set(IS_COMPARING_STATE_KEY, this.isComparing);
        state.Set(RUN_COUNT_STATE_KEY, this.runCount);
        state.Set(BATCH_STATE_KEY, this.batch?.Copy());
        state.Set(SECOND_PROVIDER_STATE_KEY, this.secondProvider);
        state.Set(JUDGE_ENABLED_STATE_KEY, this.judgeEnabled);
        state.Set(JUDGE_PROVIDER_STATE_KEY, this.judgeProvider);
        state.Set(JUDGE_INSTRUCTIONS_STATE_KEY, this.judgeInstructions);
        state.Set(JUDGE_OWN_STANDARDS_ACCEPTED_STATE_KEY, this.judgeOwnStandardsAccepted);
        state.Set(SELECTED_PRESET_ID_STATE_KEY, this.selectedPresetId);
        state.Set(PRESET_NAME_STATE_KEY, this.presetName);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The restored form belongs to the preset of the session, which therefore wins over the one OnDefaultsAppliedAsync
    /// chose: otherwise, the next change would store this form into another preset. When the preset of the session was
    /// deleted meanwhile, the form shows the chosen preset instead, unless the session holds a batch: its results belong
    /// to the form of the session. The preset list is not shown along with a batch, so this does not happen through
    /// this page. The base class restores the first model before this method, so loading the preset here wins over it.
    /// </remarks>
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(INPUT_CONTEXT_STATE_KEY, value => this.inputContext = value);
        state.Restore(INPUT_QUESTION_STATE_KEY, value => this.inputQuestion = value);
        state.Restore(IS_COMPARING_STATE_KEY, value => this.isComparing = value);
        state.Restore(RUN_COUNT_STATE_KEY, value => this.runCount = value);
        state.Restore(BATCH_STATE_KEY, value => this.batch = value?.Copy());
        state.Restore(SECOND_PROVIDER_STATE_KEY, value => this.secondProvider = value);
        state.Restore(JUDGE_ENABLED_STATE_KEY, value => this.judgeEnabled = value);
        state.Restore(JUDGE_PROVIDER_STATE_KEY, value => this.judgeProvider = value);
        state.Restore(JUDGE_INSTRUCTIONS_STATE_KEY, value => this.judgeInstructions = value);
        state.Restore(JUDGE_OWN_STANDARDS_ACCEPTED_STATE_KEY, value => this.judgeOwnStandardsAccepted = value);

        Guid? sessionPresetId = null;
        state.Restore(SELECTED_PRESET_ID_STATE_KEY, value => sessionPresetId = value);
        if (sessionPresetId is { } presetId && this.presets.Any(preset => preset.Id == presetId))
        {
            this.selectedPresetId = presetId;
            state.Restore(PRESET_NAME_STATE_KEY, value => this.presetName = value);
        }
        else if (this.batch is null)
            this.LoadSelectedPreset();
    }

    /// <summary>
    /// Starts every run of the batch and shows each one for the vote as soon as it is ready.
    /// </summary>
    /// <remarks>
    /// The base class has already started the session, so the token of the stop button is the one which ends every run
    /// still in flight. Cancelling it needs no special handling here: every run, in flight or still queued behind
    /// ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS, resolves into an entry to show either way -- the batch runner
    /// never lets a run end any other way -- so the loop below simply keeps consuming them until none are left.
    /// </remarks>
    private async Task Compare()
    {
        //
        // Set before validating, so the judge provider field -- silent about being empty until now --
        // reports a real error on this very attempt, the same as every other required field does:
        //
        this.hasAttemptedCompare = true;

        //
        // The validation has to come first, while the fields of the form are still on the screen: it
        // checks the fields which are mounted. A form which has been taken away has nothing left to
        // find fault with, and an empty question would go through.
        //
        await this.Form!.ValidateAsync();

        //
        // The fields of the judge sit in a panel the user may have closed. The form checks them all the same, but the
        // user would not see which one is wrong. So the page opens the panel:
        //
        if (this.JudgeHasIssue)
            this.judgePanelExpanded = true;

        if (!this.InputIsValid)
            return;

        // The preset keeps what was compared, also when the form changed only a moment ago:
        await this.StorePresetIfChanged();

        this.ClearBatch();
        this.batch = new ModelComparisonBatch(this.runCount);

        // From here on, the request is shown as a summary instead of the form:
        this.isComparing = true;
        await this.CheckpointAssistantSession();
        await this.RefreshAssistantUIAsync();

        var token = this.CancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            // One action for the whole batch: the user hears once, when it is over, what the filter removed from the answers the judge read:
            await using var promptInjectionScope = this.PromptInjectionGuard.BeginAction();

            var judgeInputFilter = ModelComparisonProviderAdapter.CreateJudgeInputFilter(this.PromptInjectionGuard, promptInjectionScope, this.Title);
            var batchRunner = new ModelComparisonBatchRunner(new ModelComparisonRunner(this.Logger, judgeInputFilter));
            var judge = this.judgeEnabled
                ? ModelComparisonProviderAdapter.CreateParticipant(this.judgeProvider, this.SettingsManager, this.Component, this.Title)
                : null;

            var tasks = batchRunner.Start(
                ModelComparisonProviderAdapter.CreateParticipant(this.ProviderSettings, this.SettingsManager, this.Component, this.Title),
                ModelComparisonProviderAdapter.CreateParticipant(this.secondProvider, this.SettingsManager, this.Component, this.Title),
                this.CurrentRequest,
                this.runCount,
                token,
                judge,
                this.judgeInstructions);

            //
            // ConfigureAwait(false) throughout this loop, deliberately: Blazor Server processes every
            // click on this page through the same synchronization context this method would otherwise
            // keep resuming on for as long as the batch runs. A click has to wait its turn behind
            // whatever is already queued on that context, and a background loop which keeps hopping
            // back onto it -- once per run, for a whole batch -- can make that turn a long time coming.
            // It touches that context on purpose only twice per run: to add the run to the lists, which
            // costs next to nothing, and to render, which is not awaited -- see the remark further down.
            //
            await foreach (var completedTask in Task.WhenEach(tasks).ConfigureAwait(false))
            {
                var entry = await completedTask.ConfigureAwait(false);

                //
                // The batch is what the page renders and what a vote changes, so it changes on the
                // dispatcher of the page only, never here on the thread pool: it is not safe for a
                // writer on one thread and a reader or a second writer on another.
                //
                await this.InvokeAsync(() => this.batch?.Add(entry)).ConfigureAwait(false);

                //
                // Coalesced, not one render per run: every render -- this one included -- queues on
                // the same one dispatcher a click on this page has to wait its turn on too. Several
                // runs arriving close together, up to ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS of
                // them, would otherwise queue up that many renders ahead of whatever the user just
                // clicked, which is exactly the multi-second stall this avoids.
                //
                this.RequestBackgroundUIRefresh();
            }

            // The snapshot reads the batch, so it is taken on the dispatcher as well:
            await this.InvokeAsync(this.CheckpointAssistantSession).ConfigureAwait(false);
        }
        finally
        {
            await this.InvokeAsync(() =>
            {
                // Whatever ended the batch, a canceled one as well, the form is back unless there are results:
                this.isComparing = false;
                this.batch?.Settle(token.IsCancellationRequested);
            }).ConfigureAwait(false);

            this.RequestBackgroundUIRefresh();

            // A batch whose remaining votes the user skipped, or which the user stopped, is over only now:
            await this.InvokeAsync(this.StoreResultsIfFinished).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Goes back from the results to the request, which is as the user left it.
    /// </summary>
    /// <remarks>
    /// Offered with the summary, once the user is through with every run. Before that, stopping the
    /// batch (the button next to its progress) reaches the same screen: whatever has arrived by then
    /// stays shown.
    /// </remarks>
    private async Task StartNewBatch()
    {
        this.ClearBatch();
        await this.CheckpointAssistantSession();
    }

    /// <summary>
    /// Shows what all stored comparisons say about the models. The results of a batch are stored once its
    /// summary shows, so the dialog counts them already.
    /// </summary>
    private async Task OpenResultsDialog() => await this.DialogService.ShowAsync<ModelComparisonResultsDialog>(T("Results of all comparisons"), DialogOptions.FULLSCREEN);

    /// <summary>
    /// Goes back to the choice between the two modes. A batch in progress goes on, and the assistant session brings it
    /// back on return.
    /// </summary>
    private void SwitchMode() => this.NavigationManager.NavigateTo(Routes.ASSISTANT_MODEL_COMPARISON);

    /// <summary>
    /// Takes the vote for the current run and moves on to the next one, or to the summary after the last run. The
    /// names of the models stay hidden until the summary, see ModelComparisonBatch.Vote.
    /// </summary>
    private async Task Vote(ModelComparisonVote vote)
    {
        if (this.batch?.Vote(vote) is not true)
            return;

        await this.ShowCurrentEntry();
    }

    /// <summary>
    /// Moves on past a run which has nothing to vote on, because a model of that run did not answer.
    /// </summary>
    private async Task GoToNextEntry()
    {
        if (this.batch?.GoToNextEntry() is not true)
            return;

        await this.ShowCurrentEntry();
    }

    /// <summary>
    /// Moves on to the next run without voting on this one.
    /// </summary>
    private async Task SkipThisRun()
    {
        if (this.batch is null)
            return;

        this.batch.SkipThisRun();
        await this.ShowCurrentEntry();
    }

    /// <summary>
    /// Stops asking for a vote on every remaining run of this batch. A run still resolves itself as it arrives, but
    /// none of them are shown individually anymore: the summary appears once the whole batch has settled, which may
    /// already be the case right now.
    /// </summary>
    private async Task SkipRemainingVotes()
    {
        if (this.batch is null)
            return;

        this.batch.SkipRemainingVotes(this.isComparing);
        await this.ShowCurrentEntry();
    }

    /// <summary>
    /// Shows the run the batch moved on to, or its summary.
    /// </summary>
    /// <remarks>
    /// The next run is shown before the checkpoint is awaited, not after: a batch snapshot carries the answer text of
    /// every run so far, and saving it can take a moment the click must not wait on.
    /// </remarks>
    private async Task ShowCurrentEntry()
    {
        await this.RefreshAssistantUIAsync();
        await this.CheckpointAssistantSession();
        await this.StoreResultsIfFinished();
    }

    /// <summary>
    /// The batch whose results this page stored already, so that it does not write them again: after the last vote
    /// and once more when the batch settles, for example.
    /// </summary>
    private Guid? storedBatchId;

    /// <summary>
    /// Stores the results of the batch once the user is through with it, for the statistics.
    /// </summary>
    /// <remarks>
    /// Only then all votes are in. A batch without any vote and without any verdict of a judge holds nothing to count
    /// and is not stored. Called on the dispatcher of the page only, since it reads the batch.
    /// </remarks>
    private async Task StoreResultsIfFinished()
    {
        if (this.batch is not { IsFinished: true } finishedBatch || this.storedBatchId == finishedBatch.Id)
            return;

        var record = ModelComparisonRecord.Of(
            finishedBatch,
            DateTimeOffset.UtcNow,
            this.CurrentRequest.GetQuestionPreview(),
            ModelComparisonModelIdentity.Of(this.ProviderSettings),
            ModelComparisonModelIdentity.Of(this.secondProvider),
            this.judgeEnabled ? ModelComparisonModelIdentity.Of(this.judgeProvider) : null);

        this.storedBatchId = finishedBatch.Id;
        if (!record.HoldsAnyChoice)
            return;

        try
        {
            await this.ModelComparisonStore.SaveResultAsync(record);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not store the results of a batch of the Model Comparison assistant.");
            await MessageBus.INSTANCE.SendError(new(AppIcons.ASSISTANT_MODEL_COMPARISON, T("AI Studio could not save the results of this comparison for the statistics.")));
        }
    }

    /// <summary>
    /// The culture of the active language, see ModelComparisonFormat.Culture.
    /// </summary>
    private static System.Globalization.CultureInfo DisplayCulture => ModelComparisonFormat.Culture;

    /// <summary>
    /// Only the question is required. A comparison without a context is a comparison of how two
    /// models answer a plain question, which is a fair thing to want to know.
    /// </summary>
    private string? ValidatingQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return TA("Please provide a question or task for both models.");

        return null;
    }

    /// <summary>
    /// Checks a model of the comparison: either compared model or the judge. The selection only offers
    /// providers which may be used, but it keeps what was chosen earlier, and a preset brings back what
    /// was chosen when it was saved: a provider which the organization prohibited since, or one selected
    /// before the confidence requirements were raised, must not be sent a request all the same.
    /// </summary>
    /// <remarks>
    /// ModelComparisonProviderAdapter refuses such a provider as well, but only once the run has
    /// started, and then the model shows up as one which did not answer. Here the user is told before
    /// that.
    /// </remarks>
    private string? ValidatingComparedProvider(AIStudio.Settings.Provider provider)
    {
        return this.ValidatingProvider(provider) ?? ModelComparisonProviderCheck.IssueOf(this.SettingsManager, provider);
    }

    /// <summary>
    /// Checks the judge model. It may be the same provider as either compared model, the same as
    /// the two compared models may be the same provider as each other: nothing here rules out a
    /// strong model judging one it would also have been a fair pick for, or a model being compared
    /// against itself to see how consistent it is with its own answers.
    /// </summary>
    /// <remarks>
    /// Silent until hasAttemptedCompare: this field mounts only once the judge is
    /// switched on, often well after the form first rendered, and MudForm would otherwise flag it as
    /// soon as it exists, before the user had any chance to fill it in.
    /// </remarks>
    private string? ValidatingJudgeProvider(AIStudio.Settings.Provider provider)
    {
        return this.hasAttemptedCompare ? this.ValidatingComparedProvider(provider) : null;
    }

    /// <summary>
    /// Whether one of the fields of the switched-on judge has an issue: its model, or the expectations of the user
    /// without the confirmation that the judge may decide by its own standards.
    /// </summary>
    private bool JudgeHasIssue => this.judgeEnabled && (this.ValidatingComparedProvider(this.judgeProvider) is not null || ModelComparisonJudgeExpectations.IssueOf(this.judgeInstructions, this.judgeOwnStandardsAccepted) is not null);
}
