using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Dialogs;
using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// The chat of one step of the BeyondFacts Lab, below its form "Your details".
/// </summary>
/// <remarks>
/// The chat itself lives in BflConversationService, so an answer goes on while the person looks elsewhere; this
/// component shows it and renders whenever the service says it changed. It offers no "send to": the lab neither
/// receives content from other assistants nor hands its answers on.
/// </remarks>
public partial class BflStepChat : MSGComponentBase
{
    /// <summary>
    /// Gets or sets the step of the preparation whose chat to show.
    /// </summary>
    [Parameter, EditorRequired]
    public BflThreadLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the provider which answers.
    /// </summary>
    [Parameter, EditorRequired]
    public AIStudio.Settings.Provider Provider { get; set; } = Settings.Provider.NONE;

    /// <summary>
    /// Gets or sets why the provider cannot answer, or null when it can.
    /// </summary>
    [Parameter]
    public string? ProviderIssue { get; set; }

    [Inject]
    private BflConversationService ConversationService { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Inject]
    private ILogger<BflStepChat> Logger { get; init; } = null!;

    [Inject]
    private ConversationTokenCounter TokenCounter { get; init; } = null!;

    /// <summary>
    /// Ends a count when the component goes away.
    /// </summary>
    private readonly CancellationTokenSource countCancellation = new();

    /// <summary>
    /// What the next request carries, as far as it was counted.
    /// </summary>
    private ConversationTokens tokens = ConversationTokens.UNAVAILABLE;

    /// <summary>
    /// The provider the tokens were counted for, to count again when the parent chooses another one.
    /// </summary>
    private string countedProviderId = string.Empty;

    /// <summary>
    /// The chat shown, or null while it loads.
    /// </summary>
    private BflConversation? conversation;

    /// <summary>
    /// The step whose chat is shown, to notice when the parent shows another one.
    /// </summary>
    private BflThreadLocation? shownLocation;

    /// <summary>
    /// The message the person writes.
    /// </summary>
    private string userInput = string.Empty;

    private bool IsRunning => this.conversation?.IsRunning ?? false;

    private BflTokenBudgetState Budget => BflTokenBudget.Of(this.tokens);

    private bool CanSend => this.conversation is { IsAvailable: true, IsRunning: false } && this.ProviderIssue is null && this.Budget is not BflTokenBudgetState.FULL;

    private bool CanSendMessage => this.CanSend && !string.IsNullOrWhiteSpace(this.userInput);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        this.ConversationService.ConversationChanged += this.OnConversationChangedAsync;
        await base.OnInitializedAsync();
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        if (this.shownLocation != this.Location)
        {
            this.shownLocation = this.Location;
            this.conversation = null;
            this.userInput = string.Empty;
            this.conversation = await this.ConversationService.GetAsync(this.Location);
            await this.RecountAsync();
        }
        else if (this.countedProviderId != this.Provider.Id)
            await this.RecountAsync();
    }

    /// <inheritdoc />
    protected override void DisposeResources()
    {
        this.ConversationService.ConversationChanged -= this.OnConversationChangedAsync;
        this.countCancellation.Cancel();
        this.countCancellation.Dispose();
        base.DisposeResources();
    }

    /// <summary>
    /// Renders the chat again when the service changed it, and counts again once an answer is complete.
    /// </summary>
    /// <param name="location">The step whose chat changed.</param>
    private Task OnConversationChangedAsync(BflThreadLocation location)
    {
        if (location != this.Location)
            return Task.CompletedTask;

        return this.InvokeAsync(async () =>
        {
            if (!this.IsRunning)
                await this.RecountAsync();

            this.StateHasChanged();
        });
    }

    /// <summary>
    /// Counts what the next request of the chat carries: the system prompt with the guide, and the conversation.
    /// </summary>
    /// <remarks>
    /// Only while no answer runs: the request appends to the conversation which the count reads. The message being
    /// written is left out; the reserve of the budget keeps room for it.
    /// </remarks>
    private async Task RecountAsync()
    {
        this.countedProviderId = this.Provider.Id;
        if (this.conversation is not { Thread: { } thread, IsRunning: false } || this.ProviderIssue is not null)
        {
            this.tokens = ConversationTokens.UNAVAILABLE;
            return;
        }

        try
        {
            var systemPrompt = await this.ConversationService.ComposeSystemPromptAsync(this.Location);
            var parts = ConversationParts.Of(thread, systemPrompt, string.Empty, null, false, null);
            this.tokens = await this.TokenCounter.CountAsync(this.Provider, parts, thread.ReportedHistoryFor(this.Provider.Model), this.countCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // The component went away.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // Sending reports the same problem; the count only stays unknown:
            this.Logger.LogDebug(exception, "Could not count the tokens of a chat in the BeyondFacts Lab.");
            this.tokens = ConversationTokens.UNAVAILABLE;
        }
    }

    /// <summary>
    /// Gets how full the context of the model is, or null when nothing could be counted.
    /// </summary>
    private string? TokenBudgetText
    {
        get
        {
            if (!this.tokens.IsKnown)
                return null;

            var culture = Tools.PluginSystem.I18N.I.Culture;
            var counted = TokenAmount.Format(this.tokens.Tokens, culture);
            if (!this.tokens.Window.IsKnown)
                return string.Format(T("approx. {0} tokens; the context window of this model is unknown"), counted);

            var window = TokenAmount.Format(this.tokens.Window.DefaultTokens, culture);
            var budget = string.Format(this.tokens.IsEstimate && !this.tokens.HistoryIsReported ? T("approx. {0} of {1} tokens") : T("{0} of {1} tokens"), counted, window);
            return this.Budget switch
            {
                BflTokenBudgetState.FULL => $"{budget}. {TA("This conversation fills the context window of the model. Start it over, or choose a provider whose model reads more at once.")}",
                BflTokenBudgetState.WARNING => $"{budget}. {TA("This conversation fills most of the context window of the model. Soon, you will have to start it over.")}",
                _ => budget,
            };
        }
    }

    private Color TokenBudgetColor => this.Budget switch
    {
        BflTokenBudgetState.FULL => Color.Error,
        BflTokenBudgetState.WARNING => Color.Warning,
        _ => Color.Default,
    };

    /// <summary>
    /// Lets the model open the conversation of the step.
    /// </summary>
    private async Task StartAsync()
    {
        if (this.CanSend)
            await this.ConversationService.StartAsync(this.Location, this.Provider);
    }

    /// <summary>
    /// Sends the message of the person.
    /// </summary>
    private async Task SendAsync()
    {
        if (!this.CanSendMessage)
            return;

        var text = this.userInput;
        this.userInput = string.Empty;
        await this.ConversationService.SendAsync(this.Location, this.Provider, text);
    }

    /// <summary>
    /// Stops the answer the model writes.
    /// </summary>
    private Task CancelAsync() => this.ConversationService.CancelAsync(this.Location);

    /// <summary>
    /// Deletes the conversation after asking, so that it starts over.
    /// </summary>
    private async Task ResetAsync()
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Message, TA("Delete the conversation of this step and start it over? Your details stay as they are.") }
        };

        var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Start over"), parameters, DialogOptions.FULLSCREEN);
        var result = await reference.Result;
        if (result is null || result.Canceled)
            return;

        try
        {
            await this.ConversationService.ResetAsync(this.Location);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            this.Logger.LogError(exception, "Could not delete the chat of a step in the BeyondFacts Lab.");
            await this.MessageBus.SendError(new(Icons.Material.Filled.DeleteForever, TA("AI Studio could not delete this conversation. Please try again.")));
        }
    }
}