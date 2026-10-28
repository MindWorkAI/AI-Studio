using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.AssistantLaunch;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.Media;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.AspNetCore.Components;

using MudBlazor.Utilities;

using Timer = System.Timers.Timer;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants;

public abstract partial class AssistantBase<TSettings> : AssistantLowerBase where TSettings : IComponent
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;
    
    [Inject]
    protected IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    protected RustService RustService { get; init; } = null!;

    [Inject]
    protected AIGeneratedContentDisclosureService DisclosureService { get; init; } = null!;

    [Inject]
    protected ToolRegistry ToolRegistry { get; init; } = null!;
    
    [Inject]
    protected NavigationManager NavigationManager { get; init; } = null!;
    
    [Inject]
    protected ILogger<AssistantBase<TSettings>> Logger { get; init; } = null!;
    
    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;

    [Inject]
    protected AssistantSessionService AssistantSessionService { get; init; } = null!;

    [Inject]
    private AssistantLaunchService AssistantLaunchService { get; init; } = null!;

    /// <summary>
    /// Gets the job service used to run assistant-created chats independently from the assistant UI.
    /// </summary>
    [Inject]
    protected AIJobService AIJobService { get; init; } = null!;

    [Inject]
    protected MediaTranscriptionService MediaTranscriptionService { get; init; } = null!;
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }

    protected abstract Tools.Components Component { get; }

    /// <summary>
    /// The name of the assistant plugin, which is only set by plugin-provided assistants.
    /// </summary>
    protected virtual string RuntimeAssistantName => string.Empty;
    
    protected virtual Func<string> Result2Copy => () => this.ResultingContentBlock is null ? string.Empty : this.ResultingContentBlock.Content switch
    {
        ContentText textBlock => textBlock.Text,
        _ => string.Empty,
    };

    /// <summary>
    /// What an export of the result is named after, which the save dialog suggests as file name.
    /// An assistant whose result is about something more specific than the assistant itself names that.
    /// </summary>
    protected virtual string ExportFileName => this.Title;

    protected abstract void ResetForm();

    protected abstract bool MightPreselectValues();
    
    protected abstract string SubmitText { get; }
    
    protected abstract Func<Task> SubmitAction { get; }
    
    protected virtual bool SubmitDisabled => false;
    
    private protected virtual RenderFragment? Body => null;

    private protected virtual RenderFragment? AfterSubmitContent => null;

    private protected virtual RenderFragment? BelowSubmitContent => null;

    protected virtual bool ShowResult => true;
    
    protected virtual bool ShowEntireChatThread => false;

    protected virtual bool AllowProfiles => true;

    protected virtual bool ShowProfileSelection => true;
    
    protected virtual bool ShowDedicatedProgress => false;

    /// <summary>
    /// What the assistant is doing right now, shown below the dedicated progress bar; empty for no text.
    /// </summary>
    protected virtual string ProgressText => string.Empty;
    
    protected virtual bool ShowSendTo => true;
    
    protected virtual bool ShowCopyResult => true;

    /// <summary>
    /// Whether "Copy result", and copying or exporting the answers this assistant shows, end them with the
    /// notice that AI generated them, when that is switched on.
    /// </summary>
    /// <remarks>
    /// An assistant whose result is meant for a program or another model rather than for a reader turns
    /// this off, because one sentence more would change what the result does.
    /// </remarks>
    protected virtual bool AddsAIGeneratedContentDisclosure => true;

    protected virtual bool ShowReset => true;

    /// <summary>
    /// Whether the system prompt starts with the current date and time.
    /// </summary>
    /// <remarks>
    /// Off for most assistants, whose results do not depend on the date. An assistant which writes
    /// the date into its result, or plans around it, turns this on rather than guessing the date.
    /// </remarks>
    protected virtual bool IncludeDateTime => false;

    protected virtual string? SendToChatVisibleUserPromptPrefix => null;

    protected virtual string? SendToChatVisibleUserPromptContent => null;

    protected virtual string? SendToChatVisibleUserPromptText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(this.SendToChatVisibleUserPromptPrefix))
                return null;

            if (string.IsNullOrWhiteSpace(this.SendToChatVisibleUserPromptContent))
                return this.SendToChatVisibleUserPromptPrefix;

            return $"""
                    {this.SendToChatVisibleUserPromptPrefix}
                    
                    {this.SendToChatVisibleUserPromptContent}
                    """;
        }
    }

    protected virtual ChatThread ConvertToChatThread => this.CreateSendToChatThread();

    /// <summary>
    /// Takes over a text which another assistant or a chat sent to this assistant.
    /// </summary>
    /// <remarks>
    /// Every assistant which content may be sent to puts the text into its main field, see
    /// ComponentsExtensions.AllowSendTo. It comes before the preselected values, so a field which those only fill
    /// when empty keeps the text. An assistant which allows sending to it but takes nothing would leave the text
    /// lost without a word, which is why a test checks that each one overrides this.
    /// </remarks>
    /// <param name="payload">The text sent.</param>
    protected virtual void ApplyTextLaunchPayload(TextLaunchPayload payload) => this.Logger.LogError("The assistant '{Component}' allows sending text to it, but it does not take the text.", this.Component);

    /// <summary>
    /// What the text sent to this assistant demands, because of where it came from.
    /// </summary>
    /// <remarks>
    /// An answer written from mails demands here what it demanded in its chat. The thread of the
    /// assistant is created anew with every run, so the restrictions live here and go onto every thread
    /// this assistant runs, see AssignRuntimeIdentity, and onto every tool list, see GetRunnableToolIds.
    /// They stay until the form is reset, which removes the text as well.
    /// </remarks>
    private ChatRestrictions transferredRestrictions = ChatRestrictions.NONE;

    /// <summary>
    /// What everything this assistant holds demands: the text sent to it and what its current thread read.
    /// </summary>
    private ChatRestrictions CurrentRestrictions => this.ChatThread?.CaptureRestrictions().StricterOf(this.transferredRestrictions) ?? this.transferredRestrictions;

    /// <summary>
    /// Keeps what the current result demands, before the assistant puts that result into its own input.
    /// </summary>
    /// <remarks>
    /// Call this before clearing the thread: the result was written from what this thread read, and the
    /// web addresses in it were written by a model. Both stay with the input until the form is reset.
    /// </remarks>
    /// <param name="resultText">The result which becomes the new input.</param>
    protected void TakeOverRestrictionsOfResult(string resultText) => this.transferredRestrictions = this.CurrentRestrictions.WithWebAddressesWrittenByModelIn(resultText);

    private protected virtual RenderFragment? HeaderActions => null;

    private protected virtual RenderFragment? AfterResultContent => null;

    protected virtual IReadOnlyList<IButtonData> FooterButtons => [];

    protected virtual bool HasSettingsPanel => typeof(TSettings) != typeof(NoSettingsPanel);
    
    protected HashSet<string> SelectedToolIds = [];

    private readonly Timer formChangeTimer = new(TimeSpan.FromSeconds(1.6));

    protected MudForm? Form;
    protected CancellationTokenSource? CancellationTokenSource;
    private bool isDisposed;
    private AssistantSessionKey assistantSessionKey;
    private MediaImportOwner CurrentMediaImportOwner => MediaImportOwner.ForAssistant(this.assistantSessionKey);
    private Guid? assistantSessionId;

    /// <summary>
    /// When the running session started, which the time below the progress bar counts from; null while
    /// no session runs. It comes from the session, so an instance which returns to a running session
    /// goes on counting where the first one was.
    /// </summary>
    private DateTimeOffset? processingStartedAt;

    private AssistantSessionSnapshot? pendingRenderedAssistantSessionSnapshot;
    private AssistantSessionSnapshot? lastAttachedAssistantSessionSnapshot;

    /// <summary>
    /// Gets whether the Blazor component instance has already been disposed.
    /// </summary>
    protected bool IsAssistantComponentDisposed => this.isDisposed;

    /// <summary>
    /// Gets whether this component has attached an assistant session snapshot.
    /// </summary>
    protected bool HasAssistantSession => this.assistantSessionId is not null;

    /// <summary>Gets whether this assistant currently owns active media work.</summary>
    protected bool IsMediaImportBusy => this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner);

    /// <summary>
    /// Gets the assistant-specific identifier used to distinguish session slots.
    /// </summary>
    protected virtual string AssistantSessionInstanceId => this.GetType().FullName ?? this.Component.ToString();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // A text sent here comes first, so that the preselected values below only fill what it left empty:
        if (AssistantLaunchService.TakeTextPayload(this.Component) is { } textLaunchPayload)
        {
            this.transferredRestrictions = textLaunchPayload.Restrictions;
            this.ApplyTextLaunchPayload(textLaunchPayload);
        }

        this.MediaTranscriptionService.StateChanged += this.OnMediaImportStateChanged;
        await base.OnInitializedAsync();

        if (!this.SettingsManager.IsAssistantVisible(this.Component, assistantName: this.Title))
        {
            this.Logger.LogInformation("Assistant '{AssistantTitle}' is hidden. Redirecting to the assistants overview.", this.Title);
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
            return;
        }
        
        this.formChangeTimer.AutoReset = false;
        //
        // Mind the missing async here: a timer hands its elapsed event to a thread pool thread, where an
        // async handler has nobody to hand its exception to. Such an exception is not merely unobserved,
        // it is unhandled, and it takes the app down with it. Observing the task keeps it contained.
        //
        this.formChangeTimer.Elapsed += (_, _) =>
        {
            this.formChangeTimer.Stop();
            this.InvokeAsync(this.OnFormChange).Observe($"{nameof(AssistantBase<TSettings>)}: handling a form change");
        };
        
        this.MightPreselectValues();
        this.ProviderSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        this.CurrentProfile = this.SettingsManager.GetPreselectedProfile(this.Component);
        this.CurrentChatTemplate = this.SettingsManager.GetPreselectedChatTemplate(this.Component);
        this.SelectedToolIds = this.ToolRegistry.GetDefaultToolIds(this.Component);
        await this.OnDefaultsAppliedAsync();
        this.assistantSessionKey = new(this.Component, this.AssistantSessionInstanceId);
        await this.AttachAssistantSessionIfAvailable();
        await this.ConsumeMediaOutcomeAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        
        await base.OnParametersSetAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(firstRender)
            this.Form?.ResetValidation();

        if (this.pendingRenderedAssistantSessionSnapshot is { } snapshot)
        {
            this.pendingRenderedAssistantSessionSnapshot = null;
            await this.OnAssistantSessionRenderedAsync(snapshot);
        }
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    private string TB(string fallbackEN) => this.T(fallbackEN, typeof(AssistantBase<TSettings>).Namespace, nameof(AssistantBase<TSettings>));

    private string SubmitButtonStyle => this.SettingsManager.ConfigurationData.Confidence.ShowProviderConfidence ? this.ProviderSettings.UsedLLMProvider.GetConfidence(this.SettingsManager).StyleBorder(this.SettingsManager) : string.Empty;

    private IReadOnlyList<Tools.Components> VisibleSendToAssistants => this.AssistantLaunchService.GetDestinationsFor(this.Component);
    
    protected string? ValidatingProvider(AIStudio.Settings.Provider provider)
    {
        if(provider.UsedLLMProvider == LLMProviders.NONE)
            return this.TB("Please select a provider.");

        // A provider which nothing else rules out may still be kept from the text sent here, because of where it came from:
        if (!this.transferredRestrictions.IsLLMProviderAllowed(provider) && ChatRestrictions.NONE.IsLLMProviderAllowed(provider))
            return this.TB("This provider may not see the text sent to this assistant, because of where the text came from. Please select another provider.");

        return null;
    }

    private async Task Start()
    {
        //
        // A provider which may not see the text sent here would fail the run without a word, see
        // ContentText.CreateFromProviderAsync, so the user hears about it before the run starts:
        //
        await this.RefreshProviderSelectionFromConfigurationAsync();
        if (this.ProviderSettings == Settings.Provider.NONE || this.ValidatingProvider(this.ProviderSettings) is not null)
        {
            await this.ReportProviderIssueAsync();
            return;
        }

        if (this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner))
            return;

        var activeSession = this.AssistantSessionService.TryGetSnapshot(this.assistantSessionKey);
        if (activeSession?.IsActive ?? false)
        {
            await this.AttachAssistantSession(activeSession, restoreClientOnlyContent: true);
            return;
        }

        this.CancellationTokenSource = new();
        this.IsProcessing = true;
        var startedSession = await this.AssistantSessionService.TryBeginAsync(this.assistantSessionKey, this.Title, this.CancellationTokenSource, this.ChatThread, this.CaptureAssistantSessionState(), this);
        if (startedSession.IsActive is not true || startedSession.Key != this.assistantSessionKey)
        {
            this.CancellationTokenSource.Dispose();
            this.CancellationTokenSource = null;
            return;
        }

        this.assistantSessionId = startedSession.SessionId;
        this.processingStartedAt = startedSession.StartedAt;
        await this.RefreshAssistantUIAsync();

        var sessionStatus = AssistantSessionStatus.COMPLETED;
        var errorMessage = string.Empty;
        try
        {
            await this.SubmitAction();

            if (this.CancellationTokenSource?.IsCancellationRequested ?? false)
                sessionStatus = AssistantSessionStatus.CANCELED;
        }
        catch (OperationCanceledException)
        {
            sessionStatus = AssistantSessionStatus.CANCELED;
        }
        catch (ProviderRequestException e)
        {
            sessionStatus = AssistantSessionStatus.FAILED;
            errorMessage = e.UserMessage;
            this.Logger.LogError(e, "The provider request failed for assistant '{AssistantTitle}'. Status={StatusCode}, Reason='{ReasonPhrase}', Body='{ResponseBody}'", this.Title, e.StatusCode, e.ReasonPhrase, e.ResponseBody);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, e.UserMessage));
        }
        catch (Exception e)
        {
            sessionStatus = AssistantSessionStatus.FAILED;
            errorMessage = e.Message;
            this.Logger.LogError(e, "The assistant session '{AssistantTitle}' failed.", this.Title);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Stream, string.Format(this.TB("The assistant failed. The message is: '{0}'"), e.Message)));
        }
        finally
        {
            this.IsProcessing = false;
            this.processingStartedAt = null;
            var sessionCancellationTokenSource = this.CancellationTokenSource;
            this.CancellationTokenSource = null;
            if (this.assistantSessionId is { } sessionId)
            {
                await this.AssistantSessionService.CompleteAsync(this.assistantSessionKey, sessionId, sessionStatus, errorMessage, this.ChatThread, this.CaptureAssistantSessionState(), this);
                if (!this.isDisposed)
                    _ = this.AssistantSessionService.TryTakeInactiveSnapshot(this.assistantSessionKey);
            }
            sessionCancellationTokenSource?.Dispose();
            await this.RefreshAssistantUIAsync();
        }
    }

    /// <summary>
    /// Tells the user that the assistant cannot start with the selected provider, together with whatever else is missing.
    /// </summary>
    /// <remarks>
    /// Either no provider is selected, or the selected one may not see the text sent to this assistant,
    /// see ValidatingProvider. Most assistants validate their form only in their submit action, which
    /// never runs without a usable provider. Validating here marks the provider selection and lists the
    /// other issues as well. The form does not report the issue when it holds no provider selection, as
    /// with an assistant plugin without one, or when its selection still shows the provider which the
    /// refresh at the start has just dropped. Then the issue is added on its own.
    /// </remarks>
    private async Task ReportProviderIssueAsync()
    {
        if (this.Form is not null)
            await this.Form.Validate();

        if (this.ValidatingProvider(this.ProviderSettings) is { } issue && !this.InputIssues.Contains(issue))
            this.AddInputIssue(issue);
    }

    private void TriggerFormChange(FormFieldChangedEventArgs _)
    {
        this.formChangeTimer.Stop();
        this.formChangeTimer.Start();
    }
    
    /// <summary>
    /// This method is called after any form field has changed.
    /// </summary>
    /// <remarks>
    /// This method is called after a delay of 1.6 seconds. This is to prevent
    /// the method from being called too often. This method is called after
    /// the user has stopped typing or selecting options.
    /// </remarks>
    protected virtual Task OnFormChange() => Task.CompletedTask;

    /// <summary>
    /// Allows assistants to finish asynchronous work after their configured defaults were applied.
    /// </summary>
    protected virtual Task OnDefaultsAppliedAsync() => Task.CompletedTask;
    
    /// <summary>
    /// Add an issue to the UI.
    /// </summary>
    /// <param name="issue">The issue to add.</param>
    protected void AddInputIssue(string issue)
    {
        Array.Resize(ref this.InputIssues, this.InputIssues.Length + 1);
        this.InputIssues[^1] = issue;
        this.InputIsValid = false;
        this.RefreshAssistantUIAsync().Observe($"{nameof(AssistantBase<TSettings>)}: rendering an added input issue");
    }
    
    /// <summary>
    /// Clear all input issues.
    /// </summary>
    protected void ClearInputIssues()
    {
        this.InputIssues = [];
        this.InputIsValid = true;
        this.RefreshAssistantUIAsync().Observe($"{nameof(AssistantBase<TSettings>)}: rendering cleared input issues");
    }

    protected void CreateChatThread()
    {
        this.ChatThread = new()
        {
            IncludeDateTime = this.IncludeDateTime,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = this.AllowProfiles ? this.CurrentProfile.Id : Profile.NO_PROFILE.Id,
            SystemPrompt = this.SystemPrompt,
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = string.Format(this.TB("Assistant - {0}"), this.Title),
            Blocks = [],
        };

        this.AssignRuntimeIdentity(this.ChatThread);
    }

    protected Guid CreateChatThread(Guid workspaceId, string name)
    {
        var chatId = Guid.NewGuid();
        this.ChatThread = new()
        {
            IncludeDateTime = this.IncludeDateTime,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = this.AllowProfiles ? this.CurrentProfile.Id : Profile.NO_PROFILE.Id,
            SystemPrompt = this.SystemPrompt,
            WorkspaceId = workspaceId,
            ChatId = chatId,
            Name = name,
            Blocks = [],
        };
        
        this.AssignRuntimeIdentity(this.ChatThread);
        return chatId;
    }

    /// <summary>
    /// Sets who runs the given thread: this assistant's component and, for an assistant plugin, its name.
    /// It also puts the restrictions of a text sent to this assistant on the thread.
    /// </summary>
    /// <remarks>
    /// Both decide which tools the thread may use and what its requests to self-hosted servers name. Every
    /// way an assistant creates or sends a thread goes through here, so that none of them can forget one.
    /// That is why the restrictions of a text sent here go onto the thread here, too: every thread of this
    /// assistant may hold that text, its private threads for research included.
    /// </remarks>
    /// <param name="chatThread">The thread this assistant runs.</param>
    protected void AssignRuntimeIdentity(ChatThread chatThread)
    {
        chatThread.RuntimeComponent = this.Component;
        chatThread.RuntimeAssistantName = this.RuntimeAssistantName;
        chatThread.RequireRestrictions(this.transferredRestrictions);
    }

    private Task RefreshProviderSelectionFromConfigurationAsync()
    {
        this.ProviderSettings = this.SettingsManager.GetPreselectedProvider(this.Component, this.ProviderSettings.Id);
        return Task.CompletedTask;
    }

    protected virtual void ResetProviderAndProfileSelection()
    {
        this.ProviderSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        this.CurrentProfile = this.SettingsManager.GetPreselectedProfile(this.Component);
        this.CurrentChatTemplate = this.SettingsManager.GetPreselectedChatTemplate(this.Component);
        this.SelectedToolIds = this.ToolRegistry.GetDefaultToolIds(this.Component);
    }

    /// <summary>
    /// The tools this assistant runs with when its own rules name them, instead of asking the user.
    /// </summary>
    /// <remarks>
    /// Null is the normal case: the user picks the tools. An assistant whose configuration already
    /// says which tools belong to a run — a document analysis policy, for instance — returns them
    /// here. Its tool selection then disappears from the footer, because there is nothing left to
    /// choose: whoever wrote the policy has decided, and a user working with a policy rolled out by
    /// their organization gets it as configured.
    /// </remarks>
    protected virtual IReadOnlySet<string>? AssistantManagedToolIds => null;

    /// <summary>
    /// The tools this assistant may hand to a model with the provider it currently uses.
    /// </summary>
    /// <remarks>
    /// Whether the tools come from the assistant's own rules or from the user, the provider filter
    /// always has the last word: a tool asking for more confidence than the selected provider has
    /// never reaches the model, no matter who put it on the list. That filter belongs here rather
    /// than into the stored selection, because a provider with too little confidence must not cost
    /// the user a tool for good. The same goes for a tool which a mailbox the thread read from
    /// keeps back.
    /// </remarks>
    /// <param name="outboundDataRestriction">Where the thread the tools run in may still send data, see ChatThread.RequiredOutboundDataRestriction. A text sent to this assistant may demand more, and then that counts.</param>
    protected HashSet<string> GetRunnableToolIds(OutboundDataRestriction outboundDataRestriction)
    {
        // Every thread of this assistant may hold the text sent to it, so what that text demands holds whoever asks:
        outboundDataRestriction = outboundDataRestriction.StricterOf(this.transferredRestrictions.OutboundData.Restriction);

        if (this.AssistantManagedToolIds is not null)
            return this.ToolRegistry.FilterToolIdsForProvider(this.ProviderSettings, this.AssistantManagedToolIds, outboundDataRestriction);

        // What the user cannot see, the assistant does not use:
        if (!this.SettingsManager.IsToolSelectionVisible(this.Component))
            return [];

        return this.ToolRegistry.FilterToolIdsForProvider(this.ProviderSettings, this.SelectedToolIds, outboundDataRestriction);
    }

    /// <summary>
    /// Takes over a changed tool selection, no matter where the user made it.
    /// </summary>
    /// <remarks>
    /// The footer offers one; an assistant may instead put the tools next to the setting they
    /// belong to, as the batch processing does with its instructions. Both end up here.
    /// </remarks>
    protected Task SelectedToolIdsChanged(HashSet<string> updatedToolIds)
    {
        this.SelectedToolIds = this.ToolRegistry.NormalizeSelection(updatedToolIds);
        return Task.CompletedTask;
    }
    
    protected DateTimeOffset AddUserRequest(string request, bool hideContentFromUser = false, params List<FileAttachment> attachments)
    {
        var time = DateTimeOffset.Now;
        this.LastUserPrompt = new ContentText
        {
            Text = request,
            FileAttachments = attachments,
        };
        
        this.ChatThread!.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            HideFromUser = hideContentFromUser,
            Role = ChatRole.USER,
            Content = this.LastUserPrompt,
        });
        
        return time;
    }

    /// <summary>
    /// Asks the model for an answer to the conversation so far, and adds it as the result of this assistant.
    /// </summary>
    /// <param name="time">When the answer was asked for.</param>
    /// <param name="hideContentFromUser">Whether the answer is hidden from the user, e.g., because it is JSON; while
    /// it streams, only a changed progress text renders the form anew.</param>
    /// <param name="useSelectedTools">Whether the model may use the tools the user selected. False for a request
    /// which must rely on what the conversation holds already, e.g., one which reconciles results the tools
    /// gathered before.</param>
    /// <returns>The answer, or an empty string when the provider failed.</returns>
    protected async Task<string> AddAIResponseAsync(DateTimeOffset time, bool hideContentFromUser = false, bool useSelectedTools = true)
    {
        var manageCancellationLocally = this.CancellationTokenSource is null;
        this.CancellationTokenSource ??= new CancellationTokenSource();
        
        var aiText = new ContentText
        {
            // We have to wait for the remote
            // for the content stream: 
            InitialRemoteWait = true,
        };

        //
        // The stream calls this for every chunk and waits for it. A checkpoint captures the whole
        // state of the assistant and makes the main layout render as well, so both happen only now
        // and then. When the stream is done, both happen in any case:
        //
        var lastRendering = DateTimeOffset.MinValue;
        var lastCheckpoint = DateTimeOffset.MinValue;
        string? lastProgressText = null;
        aiText.StreamingEvent = async () =>
        {
            var now = DateTimeOffset.Now;
            if (now - lastCheckpoint >= STREAMING_CHECKPOINT_MIN_TIME)
            {
                lastCheckpoint = now;
                await this.CheckpointAssistantSession();
            }

            if (now - lastRendering < STREAMING_RENDER_MIN_TIME)
                return;

            lastRendering = now;
            if (!hideContentFromUser)
            {
                await this.RefreshAssistantUIAsync();
                return;
            }

            //
            // A hidden answer shows nothing but the progress text, so the whole form renders only
            // when that text changed. Thinking models stream for minutes before they write a word:
            //
            await this.RefreshAssistantUIAsync(() =>
            {
                var progressText = this.ProgressText;
                if (progressText == lastProgressText)
                    return false;

                lastProgressText = progressText;
                return true;
            });
        };

        aiText.StreamingDone = async () =>
        {
            await this.CheckpointAssistantSession();
            await this.RefreshAssistantUIAsync();
        };

        this.ResultingContentBlock = new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
            HideFromUser = hideContentFromUser,
        };

        if (this.ChatThread is not null)
        {
            this.ChatThread.Blocks.Add(this.ResultingContentBlock);
            this.ChatThread.SelectedProvider = this.ProviderSettings.Id;
            this.AssignRuntimeIdentity(this.ChatThread);
            this.ChatThread.SelectedToolIds = [..this.SelectedToolIds];
            this.ChatThread.RuntimeSelectedToolIds = useSelectedTools ? this.GetRunnableToolIds(this.ChatThread.RequiredOutboundDataRestriction.Restriction) : [];
            this.ChatThread.RuntimeToolsAreAssistantManaged = this.AssistantManagedToolIds is not null;
        }

        this.IsProcessing = true;
        await this.CheckpointAssistantSession();
        await this.RefreshAssistantUIAsync();
        
        try
        {
            // Use the selected provider to get the AI response.
            // By awaiting this line, we wait for the entire
            // content to be streamed.
            this.ChatThread = await aiText.CreateFromProviderAsync(this.ProviderSettings.CreateProvider(), this.ProviderSettings.Model, this.LastUserPrompt, this.ChatThread, this.CancellationTokenSource!.Token);

            // Return the AI response:
            return aiText.Text;
        }
        catch (ProviderRequestException e)
        {
            this.Logger.LogError(e, "The provider request failed for assistant '{AssistantTitle}'. Status={StatusCode}, Reason='{ReasonPhrase}', Body='{ResponseBody}'", this.Title, e.StatusCode, e.ReasonPhrase, e.ResponseBody);
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.CloudOff, e.UserMessage));

            if (this.ResultingContentBlock is not null && string.IsNullOrWhiteSpace(aiText.Text))
            {
                this.ChatThread?.Blocks.Remove(this.ResultingContentBlock);
                this.ResultingContentBlock = null;
            }

            return string.Empty;
        }
        finally
        {
            this.IsProcessing = this.assistantSessionId is not null && (this.AssistantSessionService.TryGetSnapshot(this.assistantSessionKey)?.IsActive ?? false);
            await this.CheckpointAssistantSession();
            await this.RefreshAssistantUIAsync();
        
            if(manageCancellationLocally)
            {
                this.CancellationTokenSource?.Dispose();
                this.CancellationTokenSource = null;
            }

            //
            // The handlers above close over this assistant, and the content stays in the chat
            // thread. The stream is over by now, so nothing has to listen to it anymore:
            //
            aiText.ResetStreamingHandlers();
        }
    }

    /// <summary>
    /// Starts the current assistant chat thread as a regular background-capable chat generation job.
    /// </summary>
    /// <remarks>
    /// Use this when an assistant creates a chat and hands it over to the chat page instead of
    /// rendering the answer inside the assistant UI.
    /// </remarks>
    /// <param name="time">The timestamp to use for the AI response block.</param>
    /// <param name="hideContentFromUser">Whether the AI response block should be hidden from the user.</param>
    /// <param name="isForeground">Whether the chat job should start as the current foreground job.</param>
    /// <returns>A task that completes after the chat job was registered.</returns>
    protected async Task StartChatGenerationJobAsync(DateTimeOffset time, bool hideContentFromUser = false, bool isForeground = true)
    {
        if (this.ChatThread is null)
            return;

        var aiText = new ContentText
        {
            InitialRemoteWait = true,
        };

        this.ResultingContentBlock = new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
            HideFromUser = hideContentFromUser,
        };

        this.ChatThread.Blocks.Add(this.ResultingContentBlock);
        this.ChatThread.SelectedProvider = this.ProviderSettings.Id;
        this.AssignRuntimeIdentity(this.ChatThread);

        await this.CheckpointAssistantSession();
        await this.AIJobService.TryStartChatGenerationAsync(new ChatGenerationRequest
        {
            ChatThread = this.ChatThread,
            AIText = aiText,
            LastUserPrompt = this.LastUserPrompt,
            ProviderSettings = this.ProviderSettings,
            IsForeground = isForeground,
        });
    }
    
    private Task CancelStreaming() => this.CancelAssistantSessionAsync();

    /// <summary>
    /// Requests cancellation of the active assistant session.
    /// </summary>
    /// <remarks>
    /// Derived assistants should use this method instead of accessing their local
    /// cancellation token source. A component which reattaches after navigation
    /// does not own that source, while the session service still does.
    /// </remarks>
    /// <returns>A task that completes after cancellation was requested.</returns>
    protected Task CancelAssistantSessionAsync() => this.AssistantSessionService.CancelAsync(this.assistantSessionKey, this);
    
    protected async Task CopyToClipboard()
    {
        var result = this.Result2Copy();
        if (this.AddsAIGeneratedContentDisclosure)
            result = this.DisclosureService.AddDisclosureToMarkdown(result);

        await this.RustService.CopyText2Clipboard(result);
    }

    private ChatThread CreateSendToChatThread()
    {
        var originalChatThread = this.ChatThread ?? new ChatThread();
        if (string.IsNullOrWhiteSpace(this.SendToChatVisibleUserPromptText))
            return originalChatThread with
            {
                SystemPrompt = SystemPrompts.DEFAULT,
            };

        var transferredBlocks = originalChatThread.Blocks
            .Select(block => block.Role is ChatRole.USER
                ? block.DeepClone(changeHideState: true)
                : block.DeepClone())
            .ToList();

        transferredBlocks.Insert(0, this.CreateSendToChatPrompt(originalChatThread.Blocks));
        return originalChatThread with
        {
            SystemPrompt = SystemPrompts.DEFAULT,
            Blocks = transferredBlocks,
        };
    }

    /// <summary>
    /// Creates the visible message of the user which opens a chat started from this assistant.
    /// </summary>
    /// <param name="blocks">The blocks which follow the message in the chat; it comes right before the earliest of them.</param>
    /// <returns>The message, with SendToChatVisibleUserPromptText as its text.</returns>
    private protected ContentBlock CreateSendToChatPrompt(IReadOnlyCollection<ContentBlock> blocks)
    {
        var earliestBlock = blocks.MinBy(x => x.Time);
        var visiblePromptTime = earliestBlock is null
            ? DateTimeOffset.Now
            : earliestBlock.Time == DateTimeOffset.MinValue
                ? earliestBlock.Time
                : earliestBlock.Time.AddTicks(-1);

        return new ContentBlock
        {
            Time = visiblePromptTime,
            ContentType = ContentType.TEXT,
            HideFromUser = false,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = this.SendToChatVisibleUserPromptText ?? string.Empty,
            },
        };
    }
    
    private static string? GetButtonIcon(string icon)
    {
        if(string.IsNullOrWhiteSpace(icon))
            return null;
        
        return icon;
    }
    
    protected async Task OpenSettingsDialog()
    {
        if (!this.HasSettingsPanel)
            return;

        var dialogParameters = new DialogParameters();
        await this.DialogService.ShowAsync<TSettings>(null, dialogParameters, DialogOptions.FULLSCREEN);
    }
    
    protected async Task SendToAssistant(Tools.Components destination, SendToButton sendToButton)
    {
        if (!this.VisibleSendToAssistants.Contains(destination))
            return;
        
        var contentToSend = sendToButton == default ? string.Empty : sendToButton.UseResultingContentBlockData switch
        {
            false => sendToButton.GetText(),
            true => this.ResultingContentBlock?.Content switch
            {
                ContentText textBlock => textBlock.Text,
                _ => string.Empty,
            },
        };

        // The chat starts from the conversation of this assistant, unless it only takes the text for the next message:
        if (destination is Tools.Components.CHAT && !sendToButton.SendToChatAsInput)
        {
            var convertedChatThread = this.ConvertToChatThread;
            convertedChatThread = convertedChatThread with { SelectedProvider = this.ProviderSettings.Id };

            //
            // The chat holds what this assistant held, whichever thread the assistant builds it from: some
            // assistants start it from a new thread, which has read nothing, or from none at all. The copy
            // above is the chat's own, so tightening it leaves the thread of this assistant as it is:
            //
            convertedChatThread.RequireRestrictions(this.CurrentRestrictions);

            // Some assistants start the chat under an id of its own, so what their answers received goes along. A chat without an id received nothing:
            if (this.ChatThread is { } assistantChat && convertedChatThread.ChatId != Guid.Empty && assistantChat.ChatId != convertedChatThread.ChatId)
                await ChatProvenance.CopyChatAsync(assistantChat.ChatId, convertedChatThread.ChatId);

            MessageBus.INSTANCE.DeferMessage(this, Event.SEND_TO_CHAT, new ChatStartRequest(convertedChatThread));
            this.NavigationManager.NavigateTo(Routes.CHAT);
            return;
        }

        // A model wrote what an assistant sends, even when it falls back to the input, so it gets filtered, and none of its addresses counts as given:
        var payload = await this.AssistantLaunchService.CreateTextPayloadAsync(contentToSend, writtenByModel: true, this.CurrentRestrictions, this.Title);
        await this.AssistantLaunchService.LaunchAsync(destination, payload);
    }

    private async Task InnerResetForm()
    {
        if ((this.AssistantSessionService.TryGetSnapshot(this.assistantSessionKey)?.IsActive ?? false)
            || this.MediaTranscriptionService.IsBusy(this.CurrentMediaImportOwner))
            return;

        await this.AssistantSessionService.ClearAsync(this.assistantSessionKey);
        this.MediaTranscriptionService.ClearOwnerState(this.CurrentMediaImportOwner);
        this.assistantSessionId = null;
        this.ClearConversationState();

        // The text sent here is gone with the form, and what it demanded goes with it:
        this.transferredRestrictions = ChatRestrictions.NONE;
        this.ProviderSettings = Settings.Provider.NONE;
        
        await this.JsRuntime.ClearDiv(BEFORE_RESULT_DIV_ID);
        await this.JsRuntime.ClearDiv(RESULT_DIV_ID);
        await this.JsRuntime.ClearDiv(AFTER_RESULT_DIV_ID);
        
        this.ResetForm();
        this.ResetProviderAndProfileSelection();
        await this.OnDefaultsAppliedAsync();
        
        this.InputIsValid = false;
        this.InputIssues = [];
        
        this.Form?.ResetValidation();
        await this.RefreshAssistantUIAsync();
        this.Form?.ResetValidation();
    }

    private string GetResetColor() => this.SettingsManager.IsDarkMode switch
    {
        true => $"background-color: #804000",
        false => $"background-color: {this.ColorTheme.GetCurrentPalette(this.SettingsManager).Warning.Value}",
    };
    
    private string GetSendToColor() => this.SettingsManager.IsDarkMode switch
    {
        true => $"background-color: #004080",
        false => $"background-color: {this.ColorTheme.GetCurrentPalette(this.SettingsManager).InfoLighten}",
    };

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        this.MediaTranscriptionService.StateChanged -= this.OnMediaImportStateChanged;
        this.isDisposed = true;
        try
        {
            this.formChangeTimer.Stop();
            this.formChangeTimer.Dispose();
        }
        catch
        {
            // ignore
        }
        
        base.DisposeResources();
    }

    /// <summary>Refreshes assistant actions when the shared import lane changes.</summary>
    private void OnMediaImportStateChanged(MediaImportOwner owner)
    {
        if (owner == this.CurrentMediaImportOwner)
            this.InvokeAsync(async () =>
            {
                await this.ConsumeMediaOutcomeAsync();
                this.StateHasChanged();
            }).Observe($"{nameof(AssistantBase<TSettings>)}: consuming a media import outcome");
    }

    /// <summary>Consumes a terminal media notification when this assistant is visible.</summary>
    private async Task ConsumeMediaOutcomeAsync()
    {
        var outcome = this.MediaTranscriptionService.TryConsumeOutcome(this.CurrentMediaImportOwner);
        if (outcome is null)
            return;

        if (outcome.Failures.Count > 0)
        {
            var message = string.Join(Environment.NewLine, outcome.Failures.Select(failure => $"{failure.FileName}: {failure.UserMessage}"));
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, message));
        }
        else if (outcome.Status is MediaImportStatus.FAILED)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.VoiceChat, this.TB("The media file could not be transcribed.")));
        }

        if (outcome.Warnings.Count > 0)
        {
            var message = string.Join(Environment.NewLine, outcome.Warnings.Select(warning => $"{warning.FileName}: {warning.UserMessage}"));
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, message));
        }

        if (outcome.Status is MediaImportStatus.CANCELLED)
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.VoiceChat, this.TB("The media transcription was canceled.")));
        }
    }

    #endregion

    #region Assistant sessions

    /// <summary>
    /// Stores the current assistant UI and chat state in the active assistant session.
    /// </summary>
    /// <returns>A task that completes after the checkpoint was stored and published.</returns>
    protected Task CheckpointAssistantSession()
    {
        if (this.assistantSessionId is null)
            return Task.CompletedTask;

        return this.AssistantSessionService.CheckpointAsync(this.assistantSessionKey, this.assistantSessionId.Value, this.Title, this.ChatThread, this.CaptureAssistantSessionState(), this);
    }

    /// <summary>
    /// Allows derived assistants to restore client-only UI after a session was attached.
    /// </summary>
    /// <param name="snapshot">The assistant session snapshot that was attached.</param>
    /// <returns>A task that completes after derived UI restore work has finished.</returns>
    protected virtual Task OnAssistantSessionAttachedAsync(AssistantSessionSnapshot snapshot) => Task.CompletedTask;

    /// <summary>
    /// Allows derived assistants to restore DOM-dependent client-only UI after an attached session was rendered.
    /// </summary>
    /// <param name="snapshot">The assistant session snapshot that was rendered.</param>
    /// <returns>A task that completes after derived UI restore work has finished.</returns>
    protected virtual Task OnAssistantSessionRenderedAsync(AssistantSessionSnapshot snapshot) => Task.CompletedTask;

    /// <summary>
    /// Handles assistant session change events for the current assistant instance.
    /// </summary>
    /// <typeparam name="T">The message payload type.</typeparam>
    /// <param name="sendingComponent">The component that sent the message, if any.</param>
    /// <param name="triggeredEvent">The event that was triggered.</param>
    /// <param name="data">The message payload.</param>
    /// <returns>A task that completes after the message was processed.</returns>
    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (ReferenceEquals(sendingComponent, this))
            return;

        switch (triggeredEvent)
        {
            case Event.ASSISTANT_SESSION_CHANGED:
            case Event.ASSISTANT_SESSION_FINISHED:
                if (data is AssistantSessionSnapshot snapshot && snapshot.Key == this.assistantSessionKey)
                {
                    await this.AttachAssistantSession(snapshot, restoreClientOnlyContent: triggeredEvent is Event.ASSISTANT_SESSION_FINISHED);
                    if (triggeredEvent is Event.ASSISTANT_SESSION_FINISHED)
                        _ = this.AssistantSessionService.TryTakeInactiveSnapshot(this.assistantSessionKey);
                }
                break;
        }
    }

    /// <summary>
    /// Attaches the component to an existing assistant session if one is available.
    /// </summary>
    /// <returns>A task that completes after the session was attached.</returns>
    private async Task AttachAssistantSessionIfAvailable()
    {
        var snapshot = this.AssistantSessionService.TryGetSnapshot(this.assistantSessionKey);
        if (snapshot?.IsActive ?? false)
        {
            await this.AttachAssistantSession(snapshot, restoreClientOnlyContent: true);
            return;
        }

        snapshot = this.AssistantSessionService.TryTakeInactiveSnapshot(this.assistantSessionKey);
        if (snapshot is null)
            return;

        await this.AttachAssistantSession(snapshot, restoreClientOnlyContent: true);
    }

    /// <summary>
    /// Applies an assistant session snapshot to this component instance.
    /// </summary>
    /// <remarks>
    /// When the user leaves an assistant while it runs and comes back, this component only shows the run.
    /// The component which started it keeps working out of sight and reports every few seconds, yet its
    /// form stays as it was when the user left. So once this component has taken over a run, it takes over
    /// only what the running assistant changed since, such as the progress and the result. What the user
    /// changes here stays, just as it would in the component which started the run.
    /// </remarks>
    /// <param name="snapshot">The snapshot to attach.</param>
    /// <param name="restoreClientOnlyContent">Whether derived assistants should restore client-only UI state.</param>
    /// <returns>A task that completes after the component was refreshed.</returns>
    private async Task AttachAssistantSession(AssistantSessionSnapshot snapshot, bool restoreClientOnlyContent)
    {
        this.assistantSessionId = snapshot.SessionId;
        this.ImportAssistantSessionState(snapshot.StateChangedSince(this.lastAttachedAssistantSessionSnapshot));
        this.lastAttachedAssistantSessionSnapshot = snapshot;
        this.ChatThread = snapshot.ChatThread ?? this.ChatThread;
        this.IsProcessing = snapshot.IsActive;
        this.processingStartedAt = snapshot.IsActive ? snapshot.StartedAt : null;

        if (!snapshot.IsActive)
            this.CancellationTokenSource = null;

        if (restoreClientOnlyContent)
            await this.OnAssistantSessionAttachedAsync(snapshot);

        if (restoreClientOnlyContent)
            this.pendingRenderedAssistantSessionSnapshot = snapshot;

        await this.RefreshAssistantUIAsync();
    }

    /// <summary>
    /// Refreshes the component when it is still mounted.
    /// </summary>
    /// <param name="hasChanged">Decides whether anything visible changed, asked on the thread of the renderer, so it may read what the rendering reads; null to render in any case.</param>
    /// <returns>A task that completes after the renderer was notified.</returns>
    protected async Task RefreshAssistantUIAsync(Func<bool>? hasChanged = null)
    {
        if (this.isDisposed)
            return;

        try
        {
            await this.InvokeAsync(() =>
            {
                if (hasChanged is null || hasChanged())
                    this.StateHasChanged();
            });
        }
        catch (InvalidOperationException)
        {
            // The component may already have left the renderer while a background session is finishing.
        }
    }

    /// <summary>
    /// Captures the base assistant state and assistant-specific typed state values for session restore.
    /// </summary>
    /// <returns>A dictionary containing the current assistant state.</returns>
    private Dictionary<string, IAssistantSessionSnapshotField> CaptureAssistantSessionState()
    {
        var state = new AssistantSessionStateWriter();
        state.Set(PROVIDER_SETTINGS_STATE_KEY, this.ProviderSettings);
        state.Set(INPUT_IS_VALID_STATE_KEY, this.InputIsValid);
        state.Set(CURRENT_PROFILE_STATE_KEY, this.CurrentProfile);
        state.Set(CURRENT_CHAT_TEMPLATE_STATE_KEY, this.CurrentChatTemplate);
        state.Set(CHAT_THREAD_STATE_KEY, this.ChatThread);
        state.Set(LAST_USER_PROMPT_STATE_KEY, this.LastUserPrompt);
        state.Set(RESULTING_CONTENT_BLOCK_STATE_KEY, this.ResultingContentBlock);
        state.Set(INPUT_ISSUES_STATE_KEY, this.InputIssues);
        state.Set(IS_PROCESSING_STATE_KEY, this.IsProcessing);
        state.Set(SELECTED_TOOL_IDS_STATE_KEY, this.SelectedToolIds);
        state.Set(TRANSFERRED_RESTRICTIONS_STATE_KEY, this.transferredRestrictions);
        this.CaptureCustomAssistantSessionState(state);

        return state.ToDictionary();
    }

    /// <summary>
    /// Captures assistant-specific state values.
    /// </summary>
    /// <param name="state">The typed state writer to update.</param>
    protected virtual void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state) { }

    /// <summary>
    /// Restores the base assistant state and assistant-specific typed state values from a session snapshot.
    /// </summary>
    /// <param name="state">The captured assistant state to import.</param>
    private void ImportAssistantSessionState(IReadOnlyDictionary<string, IAssistantSessionSnapshotField> state)
    {
        var reader = new AssistantSessionStateReader(state, this.Title);
        reader.Restore(PROVIDER_SETTINGS_STATE_KEY, value => this.ProviderSettings = value);
        reader.Restore(INPUT_IS_VALID_STATE_KEY, value => this.InputIsValid = value);
        reader.Restore(CURRENT_PROFILE_STATE_KEY, value => this.CurrentProfile = value);
        reader.Restore(CURRENT_CHAT_TEMPLATE_STATE_KEY, value => this.CurrentChatTemplate = value);
        reader.Restore(CHAT_THREAD_STATE_KEY, value => this.ChatThread = value);
        reader.Restore(LAST_USER_PROMPT_STATE_KEY, value => this.LastUserPrompt = value);
        reader.Restore(RESULTING_CONTENT_BLOCK_STATE_KEY, value => this.ResultingContentBlock = value);
        reader.Restore(INPUT_ISSUES_STATE_KEY, value => this.InputIssues = value);
        reader.Restore(IS_PROCESSING_STATE_KEY, value => this.IsProcessing = value);
        reader.Restore(SELECTED_TOOL_IDS_STATE_KEY, value => this.SelectedToolIds = this.ToolRegistry.NormalizeSelection(value));
        reader.Restore(TRANSFERRED_RESTRICTIONS_STATE_KEY, value => this.transferredRestrictions = value);
        this.RestoreCustomAssistantSessionState(reader);
    }

    /// <summary>
    /// Restores assistant-specific state values.
    /// </summary>
    /// <param name="state">The typed state reader to read from.</param>
    protected virtual void RestoreCustomAssistantSessionState(AssistantSessionStateReader state) { }

    #endregion
}
