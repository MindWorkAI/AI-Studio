using AIStudio.Chat;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AssistantSessions;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

public partial class AssistantModelComparison : AssistantBaseCore<NoSettingsPanel>
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    protected override Tools.Components Component => Tools.Components.MODEL_COMPARISON_ASSISTANT;

    protected override string Title => T("Model Comparison");

    protected override string Description => T("Send the same request to two models and compare their answers side by side. You vote blindly for the better answer, and only after your vote you learn which model wrote which answer.");

    protected override string SystemPrompt => string.Empty;

    protected override bool AllowProfiles => false;

    protected override bool ShowResult => false;

    /// <remarks>
    /// The footer of the base class offers to send the result to another assistant, and to copy it.
    /// Both work on the one result the base class knows about, and this assistant has none: its
    /// answers are shown by the page itself. Each answer has a copy button of its own.
    /// </remarks>
    protected override bool ShowSendTo => false;

    protected override bool ShowCopyResult => false;

    protected override string SubmitText => T("Compare");

    /// <summary>
    /// Once there are results, the request is not editable, so there is nothing to submit. The way
    /// to the next comparison is the button below the vote.
    /// </summary>
    /// <remarks>
    /// The button of the base class cannot be hidden, and repurposing it would start a session of
    /// the base class just to switch the view, with the waiting animation flashing by.
    /// </remarks>
    protected override bool SubmitDisabled => this.isComparing || this.batchEntries.Count > 0;

    protected override Func<Task> SubmitAction => this.Compare;

    /// <remarks>
    /// Starts over with the form, both models included: the base class has already cleared the first
    /// one by the time this runs.
    /// </remarks>
    protected override void ResetForm()
    {
        this.inputContext = string.Empty;
        this.inputQuestion = string.Empty;
        this.secondProvider = AIStudio.Settings.Provider.NONE;
        this.judgeEnabled = true;
        this.judgeProvider = AIStudio.Settings.Provider.NONE;
        this.judgeInstructions = string.Empty;
        this.runCount = 1;
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
    /// The runs of the current batch, in the order they finished -- not the order they were started
    /// in, since every run asks the same two models the same question, and nothing distinguishes
    /// "run 3" from "run 7" beyond how long each happened to take. Filled in one at a time as each
    /// run completes, so the user can start voting on the first one without waiting for the slowest.
    /// </summary>
    private readonly List<ModelComparisonBatchEntry> batchEntries = [];

    /// <summary>
    /// What the user voted for each entry of <see cref="batchEntries"/>, at the same index. Null at
    /// an index means that entry has not been voted on yet.
    /// </summary>
    private readonly List<ModelComparisonVote?> batchVotes = [];

    /// <summary>
    /// Which entry of the batch the user is currently looking at. Advances once its vote is in, or
    /// the user chose to skip it, via <see cref="GoToNextEntry"/>/<see cref="SkipThisRun"/>, so it
    /// never skips ahead of an entry which is still blind on its own -- except when
    /// <see cref="skippingRemainingVotes"/> jumps it straight past every run left.
    /// </summary>
    private int currentEntryIndex;

    /// <summary>
    /// Whether the batch is being asked right now: at least one run is still in flight. Set once the
    /// form has been validated, and not when the session starts: the base class marks the session as
    /// processing before the request has been checked, and the form must not be taken away before
    /// that.
    /// </summary>
    private bool isComparing;

    /// <summary>
    /// Whether the user chose to stop voting on the runs of this batch. Every run still resolves
    /// itself as it arrives -- the judge, if one is configured, still judges it -- but none of them
    /// are shown individually anymore: the summary appears once the whole batch has settled.
    /// </summary>
    private bool skippingRemainingVotes;

    /// <summary>
    /// The entry the user is currently looking at, or null once every run has been voted on, or
    /// before the first one has arrived.
    /// </summary>
    private ModelComparisonBatchEntry? CurrentEntry => this.currentEntryIndex < this.batchEntries.Count ? this.batchEntries[this.currentEntryIndex] : null;

    /// <summary>
    /// What the user voted for <see cref="CurrentEntry"/>, or null while that vote is still to come.
    /// </summary>
    private ModelComparisonVote? CurrentVote => this.currentEntryIndex < this.batchVotes.Count ? this.batchVotes[this.currentEntryIndex] : null;

    /// <summary>
    /// Whether every run of the batch has both arrived and been voted on.
    /// </summary>
    private bool BatchFinished => this.currentEntryIndex >= this.runCount;

    /// <summary>
    /// Whether <see cref="CurrentEntry"/> is the last run of the batch. Only used to label its "move
    /// on" button "Finish" instead of "Next run" -- both call <see cref="GoToNextEntry"/> all the
    /// same, which lands on the batch summary once nothing is left to move on to.
    /// </summary>
    private bool IsLastEntry => this.currentEntryIndex + 1 >= this.runCount;

    /// <summary>
    /// Whether the run at this index has both models' answers in yet -- and, when a judge is
    /// configured, its verdict too, since an entry only exists once everything about it is settled.
    /// </summary>
    /// <param name="index">A run's position in the batch, zero-based.</param>
    private bool RunHasArrived(int index) => index < this.batchEntries.Count;

    /// <summary>
    /// Whether the run at this index is one of the ones actually in flight right now, as opposed to
    /// arrived already or still queued behind <see cref="ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS"/>
    /// others.
    /// </summary>
    /// <remarks>
    /// Nothing tracks which run is which once they are all started -- they are interchangeable, and
    /// only how many have arrived is known. So this counts positions, not identities: the runs still
    /// outstanding are shown as if the ones nearest to arriving were the ones actually running, which
    /// is true in substance even when it is not true of that exact run.
    /// </remarks>
    /// <param name="index">A run's position in the batch, zero-based.</param>
    private bool RunIsActive(int index) => !this.RunHasArrived(index) && index < this.batchEntries.Count + ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS;

    /// <summary>
    /// Whether the run at this index carries a completed judge verdict.
    /// </summary>
    /// <param name="index">A run's position in the batch, zero-based.</param>
    private bool RunHasJudgeVerdict(int index) => index < this.batchEntries.Count && this.batchEntries[index].Result.Judge is { Completed: true };

    /// <summary>
    /// Whether the user has voted on the run at this index.
    /// </summary>
    /// <param name="index">A run's position in the batch, zero-based.</param>
    private bool RunIsVoted(int index) => index < this.batchVotes.Count && this.batchVotes[index] is not null;

    /// <summary>
    /// Whether the run at this index arrived without a usable answer from at least one of the two
    /// models -- because the batch was stopped before it got there, or because a model failed on its
    /// own. Both are shown the same way: there is nothing left to vote on either way.
    /// </summary>
    /// <param name="index">A run's position in the batch, zero-based.</param>
    private bool RunNotCompleted(int index) => this.RunHasArrived(index) && !this.batchEntries[index].Result.BothCompleted;

    /// <summary>
    /// Whether the run at this index had a usable answer, but the user moved past it -- via
    /// <see cref="SkipThisRun"/> or <see cref="SkipRemainingVotes"/> -- without voting on it.
    /// </summary>
    /// <param name="index">A run's position in the batch, zero-based.</param>
    private bool RunVoteWasSkipped(int index) => this.RunHasArrived(index) && !this.RunNotCompleted(index) && !this.RunIsVoted(index) && index < this.currentEntryIndex;

    /// <summary>
    /// What the progress badge for this run means, for the tooltip over it: the badge alone only
    /// shows colour and a small icon, not enough on its own to be sure what each states for.
    /// </summary>
    /// <param name="index">A run's position in the batch, zero-based.</param>
    private string DescribeRunStatus(int index)
    {
        var runNumber = index + 1;

        if (!this.RunHasArrived(index))
            return this.RunIsActive(index)
                ? string.Format(DisplayCulture, T("Run {0}: still working."), runNumber)
                : string.Format(DisplayCulture, T("Run {0}: queued, waiting for a free slot."), runNumber);

        if (this.RunNotCompleted(index))
            return string.Format(DisplayCulture, T("Run {0}: no usable answer, nothing to vote on."), runNumber);

        if (this.RunIsVoted(index))
            return string.Format(DisplayCulture, T("Run {0}: voted on."), runNumber);

        if (this.RunVoteWasSkipped(index))
            return string.Format(DisplayCulture, T("Run {0}: your vote was skipped."), runNumber);

        if (this.judgeEnabled && !this.RunHasJudgeVerdict(index))
            return string.Format(DisplayCulture, T("Run {0}: the models are done, waiting for the judge."), runNumber);

        return this.judgeEnabled
            ? string.Format(DisplayCulture, T("Run {0}: the models and the judge are done, your vote is missing."), runNumber)
            : string.Format(DisplayCulture, T("Run {0}: the models are done, your vote is missing."), runNumber);
    }

    /// <summary>
    /// How much of the question the summary above the results shows.
    /// </summary>
    private const int QUESTION_PREVIEW_LENGTH = 300;

    /// <summary>
    /// The answers in the order of the columns, as the chat component wants them, for
    /// <see cref="CurrentEntry"/>. Null unless both models of that entry answered. They are built
    /// once per entry and not on every render: the component compares what it is given to decide
    /// whether anything has to be drawn again.
    /// </summary>
    private ContentText? answerAContent;
    private ContentText? answerBContent;

    /// <summary>
    /// A render already queued on the UI dispatcher, if one is still pending. Reused by
    /// <see cref="RequestBackgroundUIRefresh"/> so a burst of runs arriving close together queues at
    /// most one more render instead of one per run.
    /// </summary>
    private Task? pendingBackgroundRefresh;

    /// <summary>
    /// Asks for a UI refresh the same way <see cref="RefreshAssistantUIAsync"/> does, but skips
    /// asking again while one is already queued.
    /// </summary>
    /// <remarks>
    /// Blazor Server processes every queued render, and every click, on the same one dispatcher
    /// queue, each in its own turn. With up to <see cref="ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS"/>
    /// runs arriving close together, a render per run queues up several of them ahead of whatever the
    /// user just clicked -- the vote button above all -- which is exactly the multi-second stall this
    /// avoids. A run which arrives while a render is already queued does not need one of its own: the
    /// queued render still runs after this run was added to <see cref="batchEntries"/>, so it shows it
    /// all the same.
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
    private void ClearBatch()
    {
        this.batchEntries.Clear();
        this.batchVotes.Clear();
        this.currentEntryIndex = 0;
        this.skippingRemainingVotes = false;
        this.RebuildShownAnswers();
    }

    /// <summary>
    /// Builds the answers of the columns from <see cref="CurrentEntry"/>.
    /// </summary>
    /// <remarks>
    /// Called wherever the current entry changes: when it newly arrives, when the user moves on to
    /// the next one, after a reset, and after the state has been restored. What is shown is derived
    /// from the entry, never stored on its own.
    /// </remarks>
    private void RebuildShownAnswers()
    {
        if (this.CurrentEntry is not { Result.BothCompleted: true } entry)
        {
            this.answerAContent = null;
            this.answerBContent = null;
            return;
        }

        this.answerAContent = new ContentText { Text = entry.PresentationOrder.InColumnA(entry.Result).Text };
        this.answerBContent = new ContentText { Text = entry.PresentationOrder.InColumnB(entry.Result).Text };
    }

    private string inputContext = string.Empty;
    private string inputQuestion = string.Empty;

    /// <summary>
    /// What both models are asked, as the user entered it right now. Both models, and the judge with
    /// them, are asked to answer in the app's active language, regardless of what language the
    /// question itself is written in.
    /// </summary>
    private ModelComparisonRequest CurrentRequest => new(this.inputContext, this.inputQuestion, ActiveLanguageName);

    protected override bool MightPreselectValues() => false;

    /// <summary>
    /// The first model is <see cref="AssistantLowerBase.ProviderSettings"/>, so that everything the
    /// base class does with a provider keeps working for it. This is the second one.
    /// </summary>
    private AIStudio.Settings.Provider secondProvider = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Whether an optional third model judges the two answers. On by default; the user can still
    /// switch it off for a plain two-model comparison.
    /// </summary>
    private bool judgeEnabled = true;

    /// <summary>
    /// The judge model, asked only when <see cref="judgeEnabled"/> is set.
    /// </summary>
    private AIStudio.Settings.Provider judgeProvider = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// What the user wants the judge to pay attention to, in their own words, for example what makes
    /// a good summary. Sent to the judge together with the request and both answers, but the judge
    /// never learns which model wrote which answer.
    /// </summary>
    private string judgeInstructions = string.Empty;

    /// <summary>
    /// Whether the user has tried to submit the form at least once. The judge's provider field mounts
    /// only once the judge is switched on, which can happen well after the form itself first
    /// rendered, and MudForm would otherwise mark it invalid on sight, before the user had any chance
    /// to pick one. <see cref="ValidatingJudgeProvider"/> stays silent until this is set, regardless
    /// of when MudForm itself happens to call it.
    /// </summary>
    private bool hasAttemptedCompare;

    /// <summary>
    /// Whether the presets box is expanded. Collapsed by default: presets are a convenience for
    /// repeated testing, not something every visit to this assistant needs to see.
    /// </summary>
    private bool presetsSectionExpanded;

    /// <summary>
    /// Whether the summary of the request (the question preview and the context's length) is
    /// expanded, while a batch is shown. Collapsed by default, for the same reason as
    /// <see cref="presetsSectionExpanded"/>: useful to check back on, not something to stare at while
    /// a batch runs.
    /// </summary>
    private bool requestSummaryExpanded;

    /// <summary>
    /// The Id of the saved preset currently loaded into the form, or null while nothing is loaded.
    /// Not itself persisted: it only drives which preset the rename field and the delete button act
    /// on, for the lifetime of this session.
    /// </summary>
    private string? selectedPresetId;

    /// <summary>
    /// What the rename field shows for <see cref="SelectedPreset"/>. Kept apart from the preset's own
    /// <see cref="ModelComparisonPreset.Name"/> so a half-typed name is not written back on every
    /// keystroke, only once the field loses focus.
    /// </summary>
    private string presetName = string.Empty;

    /// <summary>
    /// The saved preset <see cref="selectedPresetId"/> points to, or null when nothing is loaded or
    /// the preset was deleted from another session in the meantime.
    /// </summary>
    private ModelComparisonPreset? SelectedPreset => this.SettingsManager.ConfigurationData.ModelComparison.Presets.FirstOrDefault(preset => preset.Id == this.selectedPresetId);

    /// <summary>
    /// Loads a saved preset's fields into the form, or clears back to a blank form when the picker
    /// was cleared.
    /// </summary>
    private void LoadPreset(string? presetId)
    {
        this.selectedPresetId = presetId;
        var preset = this.SelectedPreset;
        if (preset is null)
        {
            this.presetName = string.Empty;
            return;
        }

        this.ProviderSettings = this.SettingsManager.GetProviderById(preset.FirstProviderId);
        this.secondProvider = this.SettingsManager.GetProviderById(preset.SecondProviderId);
        this.judgeEnabled = preset.JudgeEnabled;
        this.judgeProvider = this.SettingsManager.GetProviderById(preset.JudgeProviderId);
        this.judgeInstructions = preset.JudgeInstructions;
        this.inputContext = preset.Context;
        this.inputQuestion = preset.Question;
        this.runCount = preset.RunCount;
        this.presetName = preset.Name;
    }

    /// <summary>
    /// Captures the form as it stands right now into a new saved preset, under a placeholder name the
    /// user is expected to change with the rename field which appears once it is selected.
    /// </summary>
    private async Task SaveAsNewPreset()
    {
        var preset = new ModelComparisonPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = string.Format(DisplayCulture, T("Preset from {0}"), DateTimeOffset.Now),
            FirstProviderId = this.ProviderSettings.Id,
            SecondProviderId = this.secondProvider.Id,
            JudgeEnabled = this.judgeEnabled,
            JudgeProviderId = this.judgeProvider.Id,
            JudgeInstructions = this.judgeInstructions,
            Context = this.inputContext,
            Question = this.inputQuestion,
            RunCount = this.runCount,
        };

        this.SettingsManager.ConfigurationData.ModelComparison.Presets.Add(preset);
        this.selectedPresetId = preset.Id;
        this.presetName = preset.Name;

        // Opens the box so the new preset -- and the rename field to give it a real name -- is
        // actually visible, instead of the save appearing to have done nothing:
        this.presetsSectionExpanded = true;

        await this.SettingsManager.StoreSettings();
    }

    /// <summary>
    /// Captures the form as it stands right now back into <see cref="SelectedPreset"/>, keeping its
    /// Id and name, instead of creating a new preset next to it.
    /// </summary>
    private async Task UpdateSelectedPreset()
    {
        var preset = this.SelectedPreset;
        if (preset is null)
            return;

        var presets = this.SettingsManager.ConfigurationData.ModelComparison.Presets;
        var index = presets.IndexOf(preset);
        if (index < 0)
            return;

        presets[index] = preset with
        {
            FirstProviderId = this.ProviderSettings.Id,
            SecondProviderId = this.secondProvider.Id,
            JudgeEnabled = this.judgeEnabled,
            JudgeProviderId = this.judgeProvider.Id,
            JudgeInstructions = this.judgeInstructions,
            Context = this.inputContext,
            Question = this.inputQuestion,
            RunCount = this.runCount,
        };

        await this.SettingsManager.StoreSettings();
    }

    /// <summary>
    /// Writes a typed-in name back to <see cref="SelectedPreset"/>, once the field loses focus. An
    /// empty name is refused rather than saved: the picker would show nothing to click on.
    /// </summary>
    private async Task PresetNameWasChanged()
    {
        var preset = this.SelectedPreset;
        if (preset is null || string.IsNullOrWhiteSpace(this.presetName))
            return;

        preset.Name = this.presetName.Trim();
        await this.SettingsManager.StoreSettings();
    }

    /// <summary>
    /// Deletes <see cref="SelectedPreset"/>, after asking: a preset can carry a long context text
    /// that took real effort to assemble, and one click by mistake would lose it for good.
    /// </summary>
    private async Task DeletePreset()
    {
        var preset = this.SelectedPreset;
        if (preset is null)
            return;

        var discard = await this.DialogService.ShowMessageBox(
            T("Delete this preset?"),
            string.Format(T("The preset '{0}' is deleted right away. This cannot be undone."), preset.Name),
            T("Yes, delete it"),
            T("No, keep it"));

        if (discard is not true)
            return;

        this.SettingsManager.ConfigurationData.ModelComparison.Presets.Remove(preset);
        this.selectedPresetId = null;
        this.presetName = string.Empty;
        await this.SettingsManager.StoreSettings();
    }

    private static readonly AssistantSessionStateKey<string> INPUT_CONTEXT_STATE_KEY = new(nameof(inputContext));
    private static readonly AssistantSessionStateKey<string> INPUT_QUESTION_STATE_KEY = new(nameof(inputQuestion));
    private static readonly AssistantSessionStateKey<bool> IS_COMPARING_STATE_KEY = new(nameof(isComparing));
    private static readonly AssistantSessionStateKey<int> RUN_COUNT_STATE_KEY = new(nameof(runCount));
    private static readonly AssistantSessionStateKey<List<ModelComparisonBatchEntry>> BATCH_ENTRIES_STATE_KEY = new(nameof(batchEntries));
    private static readonly AssistantSessionStateKey<List<ModelComparisonVote?>> BATCH_VOTES_STATE_KEY = new(nameof(batchVotes));
    private static readonly AssistantSessionStateKey<int> CURRENT_ENTRY_INDEX_STATE_KEY = new(nameof(currentEntryIndex));
    private static readonly AssistantSessionStateKey<AIStudio.Settings.Provider> SECOND_PROVIDER_STATE_KEY = new(nameof(secondProvider));
    private static readonly AssistantSessionStateKey<bool> JUDGE_ENABLED_STATE_KEY = new(nameof(judgeEnabled));
    private static readonly AssistantSessionStateKey<AIStudio.Settings.Provider> JUDGE_PROVIDER_STATE_KEY = new(nameof(judgeProvider));
    private static readonly AssistantSessionStateKey<string> JUDGE_INSTRUCTIONS_STATE_KEY = new(nameof(judgeInstructions));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(INPUT_CONTEXT_STATE_KEY, this.inputContext);
        state.Set(INPUT_QUESTION_STATE_KEY, this.inputQuestion);
        state.Set(IS_COMPARING_STATE_KEY, this.isComparing);
        state.Set(RUN_COUNT_STATE_KEY, this.runCount);
        state.SetList(BATCH_ENTRIES_STATE_KEY, this.batchEntries);
        state.SetList(BATCH_VOTES_STATE_KEY, this.batchVotes);
        state.Set(CURRENT_ENTRY_INDEX_STATE_KEY, this.currentEntryIndex);
        state.Set(SECOND_PROVIDER_STATE_KEY, this.secondProvider);
        state.Set(JUDGE_ENABLED_STATE_KEY, this.judgeEnabled);
        state.Set(JUDGE_PROVIDER_STATE_KEY, this.judgeProvider);
        state.Set(JUDGE_INSTRUCTIONS_STATE_KEY, this.judgeInstructions);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(INPUT_CONTEXT_STATE_KEY, value => this.inputContext = value);
        state.Restore(INPUT_QUESTION_STATE_KEY, value => this.inputQuestion = value);
        state.Restore(IS_COMPARING_STATE_KEY, value => this.isComparing = value);
        state.Restore(RUN_COUNT_STATE_KEY, value => this.runCount = value);
        state.RestoreList(BATCH_ENTRIES_STATE_KEY, this.batchEntries);
        state.RestoreList(BATCH_VOTES_STATE_KEY, this.batchVotes);
        state.Restore(CURRENT_ENTRY_INDEX_STATE_KEY, value => this.currentEntryIndex = value);
        state.Restore(SECOND_PROVIDER_STATE_KEY, value => this.secondProvider = value);
        state.Restore(JUDGE_ENABLED_STATE_KEY, value => this.judgeEnabled = value);
        state.Restore(JUDGE_PROVIDER_STATE_KEY, value => this.judgeProvider = value);
        state.Restore(JUDGE_INSTRUCTIONS_STATE_KEY, value => this.judgeInstructions = value);
        this.RebuildShownAnswers();
    }

    /// <summary>
    /// Starts every run of the batch and reveals each one as soon as it is ready.
    /// </summary>
    /// <remarks>
    /// The base class has already started the session, so the token of the stop button is the one
    /// which ends every run still in flight. Cancelling it needs no special handling here: every run,
    /// in flight or still queued behind <see cref="ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS"/>,
    /// resolves into an entry to show either way -- <see cref="ModelComparisonBatchRunner"/> never lets
    /// a run end any other way -- so the loop below simply keeps consuming them until none are left.
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
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;

        this.ClearBatch();

        // From here on, the request is shown as a summary instead of the form:
        this.isComparing = true;
        await this.CheckpointAssistantSession();
        await this.RefreshAssistantUIAsync();

        var token = this.CancellationTokenSource?.Token ?? CancellationToken.None;

        try
        {
            var batchRunner = new ModelComparisonBatchRunner(new ModelComparisonRunner(this.Logger));
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
            // Its one deliberate touch of that context, the render below, is not awaited either, for
            // the same reason -- see the remark right above it.
            //
            await foreach (var completedTask in Task.WhenEach(tasks).ConfigureAwait(false))
            {
                var entry = await completedTask.ConfigureAwait(false);
                this.batchEntries.Add(entry);
                this.batchVotes.Add(null);

                // Only a rebuild for the entry the user is actually looking at avoids rebuilding the
                // shown answers -- and so the chat component re-parsing them -- for runs which arrived
                // while the user was still on an earlier one:
                if (this.batchEntries.Count - 1 == this.currentEntryIndex)
                    this.RebuildShownAnswers();

                //
                // Coalesced, not one render per run: every render -- this one included -- queues on
                // the same one dispatcher a click on this page has to wait its turn on too. Several
                // runs arriving close together, up to ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS of
                // them, would otherwise queue up that many renders ahead of whatever the user just
                // clicked, which is exactly the multi-second stall this avoids.
                //
                this.RequestBackgroundUIRefresh();
            }

            await this.CheckpointAssistantSession();
        }
        finally
        {
            // Whatever ended the batch, a canceled one as well, the form is back unless there are results:
            this.isComparing = false;

            // Nothing left to arrive and flip this instead, now that the batch has settled itself.
            // A stopped batch goes straight to the summary the same way: there is no point asking
            // for votes on runs the user just chose to stop waiting for.
            if (this.skippingRemainingVotes || token.IsCancellationRequested)
                this.currentEntryIndex = this.runCount;

            this.RequestBackgroundUIRefresh();
        }
    }

    /// <summary>
    /// Goes back from the results to the request, which is as the user left it.
    /// </summary>
    /// <remarks>
    /// Offered without a question once every run has been voted on, or when a run has nothing to
    /// vote on because a model did not answer. Before that, stopping the batch (the button next to
    /// its progress) reaches the same screen: whatever has arrived by then stays shown.
    /// </remarks>
    private async Task StartNewBatch()
    {
        this.ClearBatch();
        await this.CheckpointAssistantSession();
    }

    /// <summary>
    /// Takes the vote for <see cref="CurrentEntry"/>, which ends the blind part: from here on, the
    /// names of the models are shown.
    /// </summary>
    /// <remarks>
    /// The vote is final. A second click, or a second one on another button, changes nothing: once
    /// the names are on the screen, the answers can no longer be judged without knowing them.
    ///
    /// The reveal is shown before the checkpoint is awaited, not after: a batch snapshot carries the
    /// answer text of every run so far, and saving it can take a moment the click must not wait on.
    /// </remarks>
    private async Task Vote(ModelComparisonVote vote)
    {
        if (this.CurrentVote is not null || this.CurrentEntry is not { Result.BothCompleted: true })
            return;

        this.batchVotes[this.currentEntryIndex] = vote;
        await this.RefreshAssistantUIAsync();
        await this.CheckpointAssistantSession();
    }

    /// <summary>
    /// Moves on to the next run of the batch, once the current one has been voted on -- or right
    /// away when there was nothing to vote on, because a model of that run did not answer.
    /// </summary>
    private Task GoToNextEntry()
    {
        if (this.CurrentVote is null && this.CurrentEntry is { Result.BothCompleted: true })
            return Task.CompletedTask;

        return this.AdvanceToNextEntry();
    }

    /// <summary>
    /// Moves on to the next run without voting on this one, even though there was something to vote
    /// on -- the user's own choice, unlike the silent skip in <see cref="GoToNextEntry"/> for a run
    /// with nothing to vote on.
    /// </summary>
    private Task SkipThisRun() => this.AdvanceToNextEntry();

    /// <summary>
    /// Advances past the current entry unconditionally. <see cref="GoToNextEntry"/> and
    /// <see cref="SkipThisRun"/> both end up here; only whether a vote is required before doing so
    /// tells them apart.
    /// </summary>
    /// <remarks>
    /// The same reveal-before-checkpoint order as <see cref="Vote"/>, for the same reason.
    /// </remarks>
    private async Task AdvanceToNextEntry()
    {
        this.currentEntryIndex++;
        this.RebuildShownAnswers();
        await this.RefreshAssistantUIAsync();
        await this.CheckpointAssistantSession();
    }

    /// <summary>
    /// Stops asking for a vote on every remaining run of this batch. A run still resolves itself as
    /// it arrives, but none of them are shown individually anymore: the summary appears once the
    /// whole batch has settled, which may already be the case right now.
    /// </summary>
    private async Task SkipRemainingVotes()
    {
        this.skippingRemainingVotes = true;

        // Nothing left in flight to flip this later, so it has to happen here instead:
        if (!this.isComparing)
            this.currentEntryIndex = this.runCount;

        this.RebuildShownAnswers();
        await this.RefreshAssistantUIAsync();
        await this.CheckpointAssistantSession();
    }

    /// <summary>
    /// How a model did in the vote, as a colour: green when its answer was preferred, red when the
    /// other one was, orange when the user found them equally good.
    /// </summary>
    /// <param name="column">The column the model's answer stood in.</param>
    private Color GetVerdictColor(ModelComparisonVote column) => this.CurrentVote switch
    {
        ModelComparisonVote.TIE => Color.Warning,
        { } vote when vote == column => Color.Success,

        _ => Color.Error,
    };

    /// <summary>
    /// The classes of the card around an answer: it fills the height of its column, and once the
    /// vote is in, it has a border in the colour which goes with <see cref="GetVerdictColor"/>.
    /// </summary>
    /// <remarks>
    /// The classes are the ones of MudBlazor: a border two pixels wide, in the colour of the outcome.
    /// They override the thin grey line of the card, which is why they are told to win. Before the
    /// vote, the answers stand in the plain frame of the chat.
    /// </remarks>
    /// <param name="column">The column the model's answer stood in.</param>
    private string GetAnswerCardClass(ModelComparisonVote column)
    {
        const string FILL_THE_COLUMN = "flex-grow-1";
        if (this.CurrentVote is null)
            return FILL_THE_COLUMN;

        var colour = this.GetVerdictColor(column) switch
        {
            Color.Success => "mud-border-success",
            Color.Warning => "mud-border-warning",

            _ => "mud-border-error",
        };

        return $"{FILL_THE_COLUMN} border-2 border-solid {colour}";
    }

    /// <summary>
    /// The icon which goes with <see cref="GetVerdictColor"/>. The colour alone would not tell
    /// apart who is colour blind, so every outcome has a shape of its own as well.
    /// </summary>
    /// <param name="column">The column the model's answer stood in.</param>
    private string GetVerdictIcon(ModelComparisonVote column) => this.CurrentVote switch
    {
        ModelComparisonVote.TIE => Icons.Material.Filled.DragHandle,
        { } vote when vote == column => Icons.Material.Filled.CheckCircle,

        _ => Icons.Material.Filled.Cancel,
    };

    /// <summary>
    /// Names what the judge preferred, by the model's revealed name, and says whether that matches
    /// the user's own vote.
    /// </summary>
    /// <remarks>
    /// Only called once the vote is in, so the model names of <see cref="CurrentEntry"/> are the ones
    /// the user is meant to see by now.
    /// </remarks>
    private string DescribeJudgePreference(ModelComparisonJudgeVerdict judge)
    {
        if (judge.Preferred is not { } preferred || this.CurrentEntry is not { } entry)
            return string.Empty;

        var agreesWithVote = this.CurrentVote == preferred;
        if (preferred is ModelComparisonVote.TIE)
            return agreesWithVote
                ? T("The judge saw both answers as equally good, matching your own vote.")
                : T("The judge saw both answers as equally good, while you preferred one of them.");

        var preferredAnswer = preferred is ModelComparisonVote.COLUMN_A
            ? entry.PresentationOrder.InColumnA(entry.Result)
            : entry.PresentationOrder.InColumnB(entry.Result);

        return agreesWithVote
            ? string.Format(DisplayCulture, T("The judge preferred {0}, matching your own vote."), preferredAnswer.Label)
            : string.Format(DisplayCulture, T("The judge preferred {0}, differing from your own vote."), preferredAnswer.Label);
    }

    /// <summary>
    /// Whether the judge's badge belongs on the answer in this column: either it is the one the
    /// judge preferred, or the judge saw a tie, in which case both columns carry the badge.
    /// </summary>
    /// <remarks>
    /// Shown next to <see cref="AnswerCaption"/>, which already gates on the vote being in, so this
    /// never has to check that itself.
    /// </remarks>
    private bool JudgePreferredThisColumn(ModelComparisonVote column)
    {
        if (this.CurrentEntry is not { Result.Judge: { Completed: true, Preferred: { } preferred } })
            return false;

        return preferred is ModelComparisonVote.TIE || preferred == column;
    }

    /// <summary>
    /// Describes how long an answer took: until the first piece arrived, and until it was complete.
    /// Shown together with the name of the model, once the vote is in.
    /// </summary>
    private string DescribeTimes(ModelComparisonAnswer answer) => string.Format(
        DisplayCulture,
        T("First token after {0}, complete after {1}"),
        FormatDuration(answer.FirstTokenTime),
        FormatDuration(answer.TotalTime));

    private static string FormatDuration(TimeSpan? duration) => duration is { } value
        ? $"{value.TotalSeconds.ToString("0.0", DisplayCulture)} s"
        : "-";

    /// <summary>
    /// The culture of the active language. Written out in full because the namespace of the
    /// localization assistant, AIStudio.Assistants.I18N, hides the class I18N from every assistant.
    /// </summary>
    private static System.Globalization.CultureInfo DisplayCulture => AIStudio.Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// The active language's own English name, without a region ("German", not "German (Germany)"),
    /// for a prompt instruction the models and the judge understand regardless of their own default
    /// language. <see cref="DisplayCulture"/> carries a region, so its neutral parent is what names
    /// the language alone -- except for the invariant culture, which has no parent to ask.
    /// </summary>
    private static string ActiveLanguageName => DisplayCulture.IsNeutralCulture || DisplayCulture.Equals(System.Globalization.CultureInfo.InvariantCulture)
        ? DisplayCulture.EnglishName
        : DisplayCulture.Parent.EnglishName;

    /// <summary>
    /// Only the question is required. A comparison without a context is a comparison of how two
    /// models answer a plain question, which is a fair thing to want to know.
    /// </summary>
    private string? ValidatingQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return T("Please provide a question or task for both models.");

        return null;
    }

    /// <summary>
    /// Checks the second model. The selection only offers providers which meet the confidence
    /// requirements, but it keeps what was chosen earlier: a provider selected before the
    /// requirements were raised must not be sent a request all the same.
    /// </summary>
    /// <remarks>
    /// <see cref="ModelComparisonProviderAdapter"/> refuses such a provider as well, but only once
    /// the run has started, and then the model shows up as one which did not answer. Here the
    /// user is told before that.
    /// </remarks>
    private string? ValidatingSecondProvider(AIStudio.Settings.Provider provider)
    {
        var selectionIssue = this.ValidatingProvider(provider);
        if (selectionIssue is not null)
            return selectionIssue;

        if (!this.SettingsManager.IsProviderConfident(provider, this.Component))
            return T("This provider does not meet the confidence requirements of this assistant. Please choose another one.");

        return null;
    }

    /// <summary>
    /// Checks the judge model. It may be the same provider as either compared model, the same as
    /// the two compared models may be the same provider as each other: nothing here rules out a
    /// strong model judging one it would also have been a fair pick for, or a model being compared
    /// against itself to see how consistent it is with its own answers.
    /// </summary>
    /// <remarks>
    /// Silent until <see cref="hasAttemptedCompare"/>: this field mounts only once the judge is
    /// switched on, often well after the form first rendered, and MudForm would otherwise flag it as
    /// soon as it exists, before the user had any chance to fill it in.
    /// </remarks>
    private string? ValidatingJudgeProvider(AIStudio.Settings.Provider provider)
    {
        if (!this.hasAttemptedCompare)
            return null;

        var selectionIssue = this.ValidatingProvider(provider);
        if (selectionIssue is not null)
            return selectionIssue;

        if (!this.SettingsManager.IsProviderConfident(provider, this.Component))
            return T("This provider does not meet the confidence requirements of this assistant. Please choose another one.");

        return null;
    }
}
