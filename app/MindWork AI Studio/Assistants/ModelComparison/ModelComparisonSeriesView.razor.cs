using AIStudio.Chat;
using AIStudio.Chat.Embedded;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Rust;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// An opened comparison series: the task the user works on, from the first chat over the second chat to the decision,
/// or the start of the next task.
/// </summary>
/// <remarks>
/// The series is stored after every step, so the user continues at the same place after a restart, e.g., days later.
/// Which model answers in the first chat is drawn at random for every task. The second chat starts with a copy of the
/// first message of the first chat, which it sends by itself, see EmbeddedChat.OpeningMessage.
///
/// Nothing on this view names a model before the result of the series is shown: messages about a provider leave out
/// its name, and a provider which was deleted or now uses another model only stops the series with a note that does
/// not say which one it was.
/// </remarks>
public partial class ModelComparisonSeriesView : MSGComponentBase
{
    private const Tools.Components COMPONENT = Tools.Components.MODEL_COMPARISON_ASSISTANT;

    /// <summary>
    /// What the user may attach when not both models can read images.
    /// </summary>
    private static readonly FileTypeFilter[] FILES_WITHOUT_IMAGES = [FileTypes.DOCUMENT, FileTypes.MAIL, FileTypes.AUDIO, FileTypes.VIDEO];

    /// <summary>
    /// Gets or sets the series.
    /// </summary>
    [Parameter, EditorRequired]
    public ModelComparisonSeries Series { get; set; } = null!;

    [Inject]
    private ModelComparisonStore ModelComparisonStore { get; init; } = null!;

    [Inject]
    private EmbeddedChatService ChatService { get; init; } = null!;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    [Inject]
    private ModelComparisonSeriesJudge SeriesJudge { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<ModelComparisonSeriesView> Logger { get; init; } = null!;

    /// <summary>
    /// The series which the fields below belong to, to notice when the parent shows another one.
    /// </summary>
    private ModelComparisonSeries? shownSeries;

    /// <summary>
    /// Where the chats of the series live.
    /// </summary>
    private IEmbeddedChatStore chatStore = null!;

    /// <summary>
    /// The provider of the first model of the series, or NONE when it is no longer available as it was.
    /// </summary>
    private AIStudio.Settings.Provider firstProvider = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// The provider of the second model of the series, or NONE when it is no longer available as it was.
    /// </summary>
    private AIStudio.Settings.Provider secondProvider = AIStudio.Settings.Provider.NONE;

    /// <summary>
    /// Why the user cannot work with the models of the series, or null when the user can.
    /// </summary>
    private string? seriesIssue;

    /// <summary>
    /// What the user may attach, or null for every file a chat takes. Images only when both models can read them, so
    /// that the second chat can start with the same first message.
    /// </summary>
    private FileTypeFilter[]? allowedFileTypes;

    /// <summary>
    /// The chat the user works in right now, or null while the user decides or no task is open.
    /// </summary>
    private EmbeddedConversation? currentConversation;

    /// <summary>
    /// The chat which shows, with its provider, or null while no chat shows. It is set together with everything else
    /// the chat needs, once all of that is known, see ShowOpenTaskAsync.
    /// </summary>
    private ModelComparisonShownChat? shownChat;

    /// <summary>
    /// Readies a request of the chat the user works in right now, with the provider of that chat.
    /// </summary>
    private Func<ChatThread, Task<bool>> prepareChat = _ => Task.FromResult(true);

    /// <summary>
    /// The copy of the first message of the first chat, with which the second chat starts, or null in the first chat.
    /// </summary>
    private ContentText? openingMessage;

    private bool isSaving;

    /// <summary>
    /// Stops the judge when the user leaves the series: the series might be opened again meanwhile, and two copies of
    /// it must not overwrite each other. The user can ask the judge again later.
    /// </summary>
    private readonly CancellationTokenSource judgeCancellation = new();

    /// <summary>
    /// Whether the judge reads the chats of a task right now.
    /// </summary>
    private bool isJudging;

    /// <summary>
    /// Whether the result of the series shows, which names the models. Shown from the start once the user saw it.
    /// </summary>
    private bool showsResult;

    /// <summary>
    /// Whether the explanation of how a task works shows. Open until the user decided the first task of the series.
    /// </summary>
    private bool explanationExpanded;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ChatService.ConversationChanged += this.OnConversationChangedAsync;
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (ReferenceEquals(this.shownSeries, this.Series))
            return;

        this.shownSeries = this.Series;
        this.showsResult = this.Series.IsResolved;
        this.explanationExpanded = this.DecidedTaskCount == 0;
        this.chatStore = new ModelComparisonSeriesChatStore(this.ModelComparisonStore, this.Series.Id);
        this.CheckModels();
        await this.ShowOpenTaskAsync();
    }

    protected override void DisposeResources()
    {
        this.ChatService.ConversationChanged -= this.OnConversationChangedAsync;
        this.judgeCancellation.Cancel();
        this.judgeCancellation.Dispose();
        base.DisposeResources();
    }

    #endregion

    /// <summary>
    /// Looks up the providers of both models and checks whether the user may still compare them.
    /// </summary>
    private void CheckModels()
    {
        this.firstProvider = ModelComparisonProviderCheck.ProviderAsItWas(this.SettingsManager, this.Series.FirstProviderId, this.Series.First);
        this.secondProvider = ModelComparisonProviderCheck.ProviderAsItWas(this.SettingsManager, this.Series.SecondProviderId, this.Series.Second);

        if (this.firstProvider.UsedLLMProvider is LLMProviders.NONE || this.secondProvider.UsedLLMProvider is LLMProviders.NONE)
            this.seriesIssue = TA("One of the two models is no longer available as it was when the series began: its provider was deleted, or it uses another model now. You cannot work on another task in this series, but you can start a new series with its setup and choose the model anew.");
        else
            this.seriesIssue = ModelComparisonProviderCheck.IssueOf(this.SettingsManager, this.firstProvider) ?? ModelComparisonProviderCheck.IssueOf(this.SettingsManager, this.secondProvider);

        this.allowedFileTypes = this.firstProvider.SupportsImageInput() && this.secondProvider.SupportsImageInput() ? null : FILES_WITHOUT_IMAGES;
    }

    /// <summary>
    /// Shows the task the user works on, as far as it got, and what the chat of that step needs.
    /// </summary>
    /// <remarks>
    /// No chat shows until its provider and its first message are known: the view renders while this method waits,
    /// and also while the series is stored right before, when the task already is in its next chat. A chat shown then
    /// would load without the first message, which it sends only when it opens, or would send it to the model of the
    /// previous chat. The markup therefore shows a chat only when shownChat is the current chat of the task.
    /// </remarks>
    private async Task ShowOpenTaskAsync()
    {
        this.shownChat = null;
        this.currentConversation = null;
        this.openingMessage = null;
        if (this.Series.OpenTask is not { } task || ModelComparisonShownChat.Of(task, this.firstProvider, this.secondProvider) is not { } chat)
            return;

        ContentText? firstMessage = null;
        if (chat.OpensWithFirstMessage)
        {
            var firstChat = await this.ConversationOf(task.ChatOne.ChatId);
            firstMessage = firstChat.FirstUserMessage?.Content?.DeepClone() as ContentText;
        }

        var conversation = await this.ConversationOf(chat.ChatId);
        var provider = chat.Provider;
        this.prepareChat = thread => this.PrepareAsync(thread, provider);
        this.openingMessage = firstMessage;
        this.currentConversation = conversation;
        this.shownChat = chat;
    }

    /// <summary>
    /// Gets a chat of the series from the service which runs it.
    /// </summary>
    /// <param name="chatId">The ID of the chat.</param>
    private Task<EmbeddedConversation> ConversationOf(Guid chatId) => this.ChatService.GetAsync(this.chatStore, COMPONENT, chatId, this.CreateThread);

    /// <summary>
    /// Creates a chat of the series, for a chat which has no stored thread yet.
    /// </summary>
    private ChatThread CreateThread() => ModelComparisonSeriesThreads.Create(this.Series, this.Series.Name);

    /// <summary>
    /// Readies a request of a chat of the series, see ModelComparisonSeriesThreads.PrepareForRequest.
    /// </summary>
    /// <param name="thread">The chat.</param>
    /// <param name="provider">The provider which answers in the chat.</param>
    /// <returns>Always true: whether the provider may answer, the embedded chat checks itself.</returns>
    private Task<bool> PrepareAsync(ChatThread thread, AIStudio.Settings.Provider provider)
    {
        var toolIds = this.ToolRegistry.FilterToolIdsForProvider(provider, this.Series.ToolIds, thread.RequiredOutboundDataRestriction.Restriction);
        ModelComparisonSeriesThreads.PrepareForRequest(thread, this.Series, provider, toolIds);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Renders again when the chat the user works in changed, e.g., once an answer is complete.
    /// </summary>
    /// <param name="chatId">The ID of the chat which changed.</param>
    private Task OnConversationChangedAsync(Guid chatId) => this.currentConversation?.ChatId == chatId
        ? this.InvokeAsync(this.StateHasChanged)
        : Task.CompletedTask;

    /// <summary>
    /// Whether the user may be done with the current chat: the model answered at least once, and no answer runs.
    /// </summary>
    private bool CanFinishChat => this.currentConversation is { IsRunning: false, HasAnswer: true } && !this.isSaving;

    private int DecidedTaskCount => this.Series.Tasks.Count(task => task.State is ModelComparisonSeriesTaskState.DONE);

    private ModelComparisonSeriesTask? LastDecidedTask => this.Series.Tasks.LastOrDefault(task => task.State is ModelComparisonSeriesTaskState.DONE);

    private string NextTaskText => this.DecidedTaskCount == 0
        ? TA("Start your first task. You work on it with one of the two models first, and then with the other one. AI Studio chooses at random which model you meet first.")
        : string.Format(ModelComparisonFormat.Culture, TA("Decided tasks so far: {0}. Do you want to test another task?"), this.DecidedTaskCount);

    private string TaskHeading(ModelComparisonSeriesTask task)
    {
        var number = this.Series.Tasks.IndexOf(task) + 1;
        var text = task.State switch
        {
            ModelComparisonSeriesTaskState.FIRST_CHAT => T("Task {0}: chat 1"),
            ModelComparisonSeriesTaskState.SECOND_CHAT => T("Task {0}: chat 2"),

            _ => TA("Task {0}: your decision"),
        };

        return string.Format(ModelComparisonFormat.Culture, text, number);
    }

    private string ChatHint(ModelComparisonSeriesTask task) => task.State is ModelComparisonSeriesTaskState.FIRST_CHAT
        ? TA("Work on your task with this model, as you would in a chat. Once you are done, the other model gets the same first message, and you continue with it.")
        : TA("Now work on the same task with the other model. It got your first message already. Once you are done, you decide which chat helped you more.");

    /// <summary>
    /// Begins a new task, with a model drawn at random for the first chat. Once the user saw the result, it asks each
    /// time: the user knows the models then, so the task is no longer blind.
    /// </summary>
    private async Task StartTaskAsync()
    {
        if (this.seriesIssue is not null || this.Series.OpenTask is not null)
            return;

        if (this.Series.IsResolved && !await this.ConfirmTaskWhichIsNotBlindAsync())
            return;

        var task = ModelComparisonSeriesTask.Begin(DateTimeOffset.UtcNow, Random.Shared.Next(2) == 0);
        this.Series.Tasks.Add(task);
        if (!await this.SaveAsync())
        {
            this.Series.Tasks.Remove(task);
            return;
        }

        await this.ShowOpenTaskAsync();
    }

    /// <summary>
    /// Asks the user whether to test another task in a series whose result they saw.
    /// </summary>
    /// <returns>True when the user wants to.</returns>
    private async Task<bool> ConfirmTaskWhichIsNotBlindAsync()
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, TA("You have seen the result of this series, so you know which model answers in which chat. Another task therefore no longer counts as blind: the result shows it apart, and the results of all comparisons leave it out. To compare blindly again, start a new series with the setup of this one. Do you want to test another task in this series anyway?") },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Test another task"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        return result is { Canceled: false };
    }

    /// <summary>
    /// Ends the work in the current chat: after the first chat, the second one begins; after the second one, the user
    /// decides.
    /// </summary>
    private async Task FinishChatAsync()
    {
        if (!this.CanFinishChat || this.Series.OpenTask is not { } task || this.currentConversation is not { FirstUserMessage: { } firstMessage } conversation)
            return;

        var startedAtUtc = firstMessage.Time.ToUniversalTime();
        var finishedAtUtc = DateTimeOffset.UtcNow;
        var isFinished = task.State is ModelComparisonSeriesTaskState.FIRST_CHAT
            ? task.FinishFirstChat(startedAtUtc, finishedAtUtc, conversation.UserMessageCount, ModelComparisonQuestionPreview.Of((firstMessage.Content as ContentText)?.Text ?? string.Empty))
            : task.FinishSecondChat(startedAtUtc, finishedAtUtc, conversation.UserMessageCount);

        if (!isFinished)
            return;

        // Shown even when saving fails: the series is saved again with the next step.
        await this.SaveAsync();
        await this.ShowOpenTaskAsync();
    }

    /// <summary>
    /// Takes the decision of the user, and then asks the judge, so that its verdict cannot anchor the decision.
    /// </summary>
    /// <param name="vote">The chat the user chose: COLUMN_A for the first chat, COLUMN_B for the second one.</param>
    private async Task DecideAsync(ModelComparisonVote vote)
    {
        if (this.Series.OpenTask is not { } task || !task.Decide(vote, DateTimeOffset.UtcNow, !this.Series.IsResolved))
            return;

        await this.SaveAsync();
        await this.ShowOpenTaskAsync();
        await this.AskJudgeAsync(task);
    }

    /// <summary>
    /// Asks the judge of the series about a decided task and stores its verdict.
    /// </summary>
    /// <param name="task">The decided task.</param>
    private async Task AskJudgeAsync(ModelComparisonSeriesTask task)
    {
        if (this.isJudging || this.Series.Judge is null || task.State is not ModelComparisonSeriesTaskState.DONE)
            return;

        this.isJudging = true;
        this.StateHasChanged();
        try
        {
            var chatOne = await this.ConversationOf(task.ChatOne.ChatId);
            var chatTwo = await this.ConversationOf(task.ChatTwo.ChatId);
            await this.SeriesJudge.JudgeAsync(this.Series, task, chatOne.Thread, chatTwo.Thread, ModelComparisonMode.TASKS.PageTitle, this.judgeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // The user left the series; the task waits for the judge until the user asks again.
            return;
        }
        finally
        {
            this.isJudging = false;
        }

        await this.SaveAsync();
    }

    /// <summary>
    /// Shows or hides the result of the series. The first time, it asks: the result names the models, which ends the
    /// blind phase of the series for good.
    /// </summary>
    private async Task ToggleResultAsync()
    {
        if (this.showsResult || this.Series.IsResolved)
        {
            this.showsResult = !this.showsResult;
            return;
        }

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, TA("The result tells you which model answered in which chat. Tasks you decide afterward no longer count as blind: the result shows them apart, and the results of all comparisons leave them out. Do you want to see the result?") },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Show the result"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        this.Series.IsResolved = true;
        this.Series.ResolvedAtUtc = DateTimeOffset.UtcNow;
        if (!await this.SaveAsync())
        {
            this.Series.IsResolved = false;
            this.Series.ResolvedAtUtc = null;
            return;
        }

        this.showsResult = true;
    }

    /// <summary>
    /// Discards the open task with both its chats, after asking. It does not count toward the result.
    /// </summary>
    private async Task DiscardTaskAsync()
    {
        if (this.Series.OpenTask is not { } task)
            return;

        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, TA("Are you sure you want to discard this task with both its chats? It does not count toward the result. This cannot be undone.") },
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Discard task"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        // An answer which still runs would otherwise store its chat again:
        await this.ChatService.ForgetAsync(task.ChatOne.ChatId);
        await this.ChatService.ForgetAsync(task.ChatTwo.ChatId);

        var index = this.Series.Tasks.IndexOf(task);
        this.Series.Tasks.RemoveAt(index);
        if (!await this.SaveAsync())
        {
            this.Series.Tasks.Insert(index, task);
            return;
        }

        try
        {
            await this.ModelComparisonStore.DeleteSeriesChatAsync(this.Series.Id, task.ChatOne.ChatId);
            await this.ModelComparisonStore.DeleteSeriesChatAsync(this.Series.Id, task.ChatTwo.ChatId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The series no longer knows the chats, so they only take some space:
            this.Logger.LogWarning(exception, "Could not delete the chats of a discarded task of a comparison series.");
        }

        await this.ShowOpenTaskAsync();
    }

    /// <summary>
    /// Stores the series and tells the user when that fails.
    /// </summary>
    /// <returns>Whether the series was stored.</returns>
    private async Task<bool> SaveAsync()
    {
        this.isSaving = true;
        try
        {
            await this.ModelComparisonStore.SaveSeriesAsync(this.Series);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not store a comparison series of the Model Comparison assistant.");
            await this.MessageBus.SendError(new(AppIcons.SAVE_FAILED, TA("AI Studio could not save the comparison series. Please try again.")));
            return false;
        }
        finally
        {
            this.isSaving = false;
        }
    }
}