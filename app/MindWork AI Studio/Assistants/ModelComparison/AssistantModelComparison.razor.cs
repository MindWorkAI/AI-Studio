using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.ModelComparison;

public partial class AssistantModelComparison : AssistantBaseCore<NoSettingsPanel>
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private PromptInjectionGuardService PromptInjectionGuard { get; init; } = null!;

    protected override Tools.Components Component => Tools.Components.MODEL_COMPARISON_ASSISTANT;

    protected override string Title => T("Model Comparison");

    protected override string Description => TA("Send the same request to two models and compare their answers side by side. You vote blindly for the better answer and learn at the end which model wrote which answer.");

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
    /// Starts over with the form, both models included: the base class has already cleared the first
    /// one by the time this runs.
    /// </remarks>
    protected override void ResetForm()
    {
        this.inputContext = string.Empty;
        this.inputQuestion = string.Empty;
        this.secondProvider = Settings.Provider.NONE;
        this.judgeEnabled = true;
        this.judgeProvider = Settings.Provider.NONE;
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
    /// What the badge of a run in the progress of the batch means, for the tooltip over it: the badge alone only
    /// shows a colour and a small icon, not enough to be sure what it stands for.
    /// </summary>
    /// <param name="shownBatch">The batch the run belongs to.</param>
    /// <param name="index">The position of the run, zero-based.</param>
    private string DescribeRunStatus(ModelComparisonBatch shownBatch, int index)
    {
        var text = shownBatch.StatusOf(index) switch
        {
            ModelComparisonRunStatus.QUEUED => T("Run {0}: queued, waiting for a free slot."),
            ModelComparisonRunStatus.RUNNING => T("Run {0}: still working."),
            ModelComparisonRunStatus.NO_USABLE_ANSWER => T("Run {0}: no usable answer, nothing to vote on."),
            ModelComparisonRunStatus.VOTED => T("Run {0}: voted on."),
            ModelComparisonRunStatus.VOTE_SKIPPED => TA("Run {0}: your vote was skipped."),

            _ when this.judgeEnabled && !shownBatch.HasJudgeVerdict(index) => TA("Run {0}: the judge gave no usable verdict, your vote is missing."),
            _ when this.judgeEnabled => TA("Run {0}: the models and the judge are done, your vote is missing."),
            _ => TA("Run {0}: the models are done, your vote is missing."),
        };

        return string.Format(DisplayCulture, text, index + 1);
    }

    /// <summary>
    /// How much of the question the summary above the results shows.
    /// </summary>
    private const int QUESTION_PREVIEW_LENGTH = 300;

    /// <summary>
    /// The answers of the batch as the chat component wants them. Each is built once, and not on every render: the
    /// component compares what it is given to decide whether anything has to be drawn again, and a new one would make
    /// it parse the whole Markdown again.
    /// </summary>
    private readonly Dictionary<ModelComparisonAnswer, ContentText> shownAnswers = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Gets the answer as the chat component wants it.
    /// </summary>
    /// <param name="answer">The answer of a model.</param>
    /// <returns>The answer for the chat component, the same one on every render.</returns>
    private ContentText ShownAnswer(ModelComparisonAnswer answer)
    {
        if (this.shownAnswers.TryGetValue(answer, out var content))
            return content;

        content = new ContentText { Text = answer.Text };
        this.shownAnswers[answer] = content;
        return content;
    }

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
    private void ClearBatch()
    {
        this.batch = null;
        this.shownAnswers.Clear();
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
    /// The first model is ProviderSettings of the base class, so that everything the
    /// base class does with a provider keeps working for it. This is the second one.
    /// </summary>
    private AIStudio.Settings.Provider secondProvider = Settings.Provider.NONE;

    /// <summary>
    /// Whether an optional third model judges the two answers. On by default; the user can still
    /// switch it off for a plain two-model comparison.
    /// </summary>
    private bool judgeEnabled = true;

    /// <summary>
    /// The judge model, asked only when judgeEnabled is set.
    /// </summary>
    private AIStudio.Settings.Provider judgeProvider = Settings.Provider.NONE;

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
    /// to pick one. ValidatingJudgeProvider stays silent until this is set, regardless
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
    /// presetsSectionExpanded: useful to check back on, not something to stare at while
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
    /// What the rename field shows for SelectedPreset. Kept apart from the preset's own
    /// name so a half-typed name is not written back on every
    /// keystroke, only once the field loses focus.
    /// </summary>
    private string presetName = string.Empty;

    /// <summary>
    /// The saved preset selectedPresetId points to, or null when nothing is loaded or
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
    /// Captures the form as it stands right now back into SelectedPreset, keeping its
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
    /// Writes a typed-in name back to SelectedPreset, once the field loses focus. An
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
    /// Deletes SelectedPreset, after asking: a preset can carry a long context text
    /// that took real effort to assemble, and one click by mistake would lose it for good.
    /// </summary>
    private async Task DeletePreset()
    {
        var preset = this.SelectedPreset;
        if (preset is null)
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, string.Format(DisplayCulture, TA("Are you sure you want to delete the preset '{0}'? This cannot be undone."), preset.Name) },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Delete preset"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
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
    private static readonly AssistantSessionStateKey<ModelComparisonBatch?> BATCH_STATE_KEY = new(nameof(batch));
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
        state.Set(BATCH_STATE_KEY, this.batch?.Copy());
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
        state.Restore(BATCH_STATE_KEY, value => this.batch = value?.Copy());
        state.Restore(SECOND_PROVIDER_STATE_KEY, value => this.secondProvider = value);
        state.Restore(JUDGE_ENABLED_STATE_KEY, value => this.judgeEnabled = value);
        state.Restore(JUDGE_PROVIDER_STATE_KEY, value => this.judgeProvider = value);
        state.Restore(JUDGE_INSTRUCTIONS_STATE_KEY, value => this.judgeInstructions = value);
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
        if (!this.InputIsValid)
            return;

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
    }

    /// <summary>
    /// How a model did in the vote of the user, as a colour: green when its answer was preferred, red when the other
    /// one was, orange when the user found them equally good.
    /// </summary>
    /// <param name="voteChoice">The model the user preferred in the run, or null without a vote.</param>
    /// <param name="model">The model whose outcome this is.</param>
    private static Color OutcomeColor(ModelComparisonModelChoice? voteChoice, ModelComparisonModelChoice model) => voteChoice switch
    {
        null => Color.Default,
        ModelComparisonModelChoice.TIE => Color.Warning,
        { } chosen when chosen == model => Color.Success,

        _ => Color.Error,
    };

    /// <summary>
    /// The icon which goes with OutcomeColor. The colour alone would not tell apart who is colour blind, so every
    /// outcome has a shape of its own as well.
    /// </summary>
    /// <param name="voteChoice">The model the user preferred in the run, or null without a vote.</param>
    /// <param name="model">The model whose outcome this is.</param>
    private static string OutcomeIcon(ModelComparisonModelChoice? voteChoice, ModelComparisonModelChoice model) => voteChoice switch
    {
        null => AppIcons.VOTE_MISSING,
        ModelComparisonModelChoice.TIE => AppIcons.VOTE_TIE,
        { } chosen when chosen == model => AppIcons.VOTE_WON,

        _ => AppIcons.VOTE_LOST,
    };

    /// <summary>
    /// The classes of the card around an answer: it fills the height of its column, and with a vote of the user, it
    /// has a border in the colour which goes with OutcomeColor.
    /// </summary>
    /// <remarks>
    /// The classes are the ones of MudBlazor: a border two pixels wide, in the colour of the outcome. They override
    /// the thin grey line of the card, which is why they are told to win. While the user votes, and for a run without
    /// a vote, the answers stand in the plain frame of the chat.
    /// </remarks>
    /// <param name="voteChoice">The model the user preferred in the run, or null without a vote.</param>
    /// <param name="model">The model whose answer the card shows.</param>
    private static string AnswerCardClass(ModelComparisonModelChoice? voteChoice, ModelComparisonModelChoice model)
    {
        if (voteChoice is null)
            return FILL_THE_COLUMN;

        var colour = OutcomeColor(voteChoice, model) switch
        {
            Color.Success => "mud-border-success",
            Color.Warning => "mud-border-warning",

            _ => "mud-border-error",
        };

        return $"{FILL_THE_COLUMN} border-2 border-solid {colour}";
    }

    /// <summary>
    /// Makes the card of an answer as high as its column, so that both cards of a run are equally high.
    /// </summary>
    private const string FILL_THE_COLUMN = "flex-grow-1";

    /// <summary>
    /// Names a model of a run, or says that both answers were equally good.
    /// </summary>
    /// <param name="entry">The run.</param>
    /// <param name="choice">A model, or a tie.</param>
    private string NameOf(ModelComparisonBatchEntry entry, ModelComparisonModelChoice choice) => choice switch
    {
        ModelComparisonModelChoice.FIRST => entry.Result.First.Label,
        ModelComparisonModelChoice.SECOND => entry.Result.Second.Label,

        _ => T("Equally good"),
    };

    /// <summary>
    /// Says in the title of a run in the summary what the user chose.
    /// </summary>
    /// <param name="entry">The run.</param>
    /// <param name="index">The position of the run, zero-based.</param>
    /// <param name="voteChoice">The model the user preferred in the run, or null without a vote.</param>
    private string DescribeRun(ModelComparisonBatchEntry entry, int index, ModelComparisonModelChoice? voteChoice)
    {
        var runNumber = index + 1;
        if (!entry.Result.BothCompleted)
            return string.Format(DisplayCulture, T("Run {0}: at least one model did not answer"), runNumber);

        return voteChoice switch
        {
            null => string.Format(DisplayCulture, TA("Run {0}: without your vote"), runNumber),
            ModelComparisonModelChoice.TIE => string.Format(DisplayCulture, TA("Run {0}: you found both answers equally good"), runNumber),

            { } chosen => string.Format(DisplayCulture, TA("Run {0}: you preferred {1}"), runNumber, this.NameOf(entry, chosen)),
        };
    }

    /// <summary>
    /// Names what the judge preferred, by the name of the model, and says whether that matches the vote of the user.
    /// </summary>
    /// <param name="entry">The run.</param>
    /// <param name="judgeChoice">The model the judge preferred, or null without a usable verdict.</param>
    /// <param name="voteChoice">The model the user preferred, or null without a vote.</param>
    private string DescribeJudgePreference(ModelComparisonBatchEntry entry, ModelComparisonModelChoice? judgeChoice, ModelComparisonModelChoice? voteChoice)
    {
        if (judgeChoice is not { } preferred)
            return string.Empty;

        if (preferred is ModelComparisonModelChoice.TIE)
            return voteChoice switch
            {
                null => T("The judge saw both answers as equally good."),
                ModelComparisonModelChoice.TIE => TA("The judge saw both answers as equally good, matching your own vote."),

                _ => TA("The judge saw both answers as equally good, while you preferred one of them."),
            };

        var preferredName = this.NameOf(entry, preferred);
        return voteChoice switch
        {
            null => string.Format(DisplayCulture, T("The judge preferred {0}."), preferredName),
            { } chosen when chosen == preferred => string.Format(DisplayCulture, TA("The judge preferred {0}, matching your own vote."), preferredName),

            _ => string.Format(DisplayCulture, TA("The judge preferred {0}, differing from your own vote."), preferredName),
        };
    }

    /// <summary>
    /// Whether the badge of the judge belongs on the answer of this model: either the judge preferred it, or the judge
    /// saw a tie, in which case both answers carry the badge.
    /// </summary>
    /// <param name="judgeChoice">The model the judge preferred, or null without a usable verdict.</param>
    /// <param name="model">The model whose answer this is.</param>
    private static bool JudgePreferred(ModelComparisonModelChoice? judgeChoice, ModelComparisonModelChoice model) => judgeChoice is ModelComparisonModelChoice.TIE || judgeChoice == model;

    /// <summary>
    /// Describes how long an answer took: until the first piece arrived, and until it was complete.
    /// Shown together with the name of the model in the summary.
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
    /// The culture of the active language. Qualified with its namespace because the namespace of the
    /// localization assistant, AIStudio.Assistants.I18N, hides the class I18N from every assistant.
    /// </summary>
    private static System.Globalization.CultureInfo DisplayCulture => Tools.PluginSystem.I18N.I.Culture;

    /// <summary>
    /// The active language's own English name, without a region ("German", not "German (Germany)"),
    /// for a prompt instruction the models and the judge understand regardless of their own default
    /// language. DisplayCulture carries a region, so its neutral parent is what names
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
        var selectionIssue = this.ValidatingProvider(provider);
        if (selectionIssue is not null)
            return selectionIssue;

        if (this.SettingsManager.IsProviderProhibited(provider.UsedLLMProvider))
            return TA("The use of this provider has been prohibited by your organization. Please select a different provider.");

        if (!this.SettingsManager.IsProviderConfident(provider, this.Component))
            return TA("This provider does not meet the confidence requirements of this assistant. Please choose another one.");

        return null;
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
}
