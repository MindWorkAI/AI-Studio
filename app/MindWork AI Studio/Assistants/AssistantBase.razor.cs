using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AIJobs;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.Services;

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
    protected ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;
    
    [Inject]
    protected NavigationManager NavigationManager { get; init; } = null!;
    
    [Inject]
    protected ILogger<AssistantBase<TSettings>> Logger { get; init; } = null!;
    
    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;

    [Inject]
    protected AssistantSessionService AssistantSessionService { get; init; } = null!;

    /// <summary>
    /// Gets the job service used to run assistant-created chats independently from the assistant UI.
    /// </summary>
    [Inject]
    protected AIJobService AIJobService { get; init; } = null!;
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }

    protected abstract Tools.Components Component { get; }
    
    protected virtual Func<string> Result2Copy => () => this.ResultingContentBlock is null ? string.Empty : this.ResultingContentBlock.Content switch
    {
        ContentText textBlock => textBlock.Text,
        _ => string.Empty,
    };

    protected abstract void ResetForm();

    protected abstract bool MightPreselectValues();
    
    protected abstract string SubmitText { get; }
    
    protected abstract Func<Task> SubmitAction { get; }
    
    protected virtual bool SubmitDisabled => false;
    
    private protected virtual RenderFragment? Body => null;

    protected virtual bool ShowResult => true;
    
    protected virtual bool ShowEntireChatThread => false;

    protected virtual bool AllowProfiles => true;

    protected virtual bool ShowProfileSelection => true;
    
    protected virtual bool ShowDedicatedProgress => false;
    
    protected virtual bool ShowSendTo => true;
    
    protected virtual bool ShowCopyResult => true;
    
    protected virtual bool ShowReset => true;

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

    private protected virtual RenderFragment? HeaderActions => null;

    private protected virtual RenderFragment? AfterResultContent => null;

    protected virtual IReadOnlyList<IButtonData> FooterButtons => [];

    protected virtual bool HasSettingsPanel => typeof(TSettings) != typeof(NoSettingsPanel);
    
    private readonly Timer formChangeTimer = new(TimeSpan.FromSeconds(1.6));
    
    protected MudForm? Form;
    protected CancellationTokenSource? CancellationTokenSource;
    private bool isDisposed;
    private AssistantSessionKey assistantSessionKey;
    private Guid? assistantSessionId;
    private AssistantSessionSnapshot? pendingRenderedAssistantSessionSnapshot;

    /// <summary>
    /// Gets whether the Blazor component instance has already been disposed.
    /// </summary>
    protected bool IsAssistantComponentDisposed => this.isDisposed;

    /// <summary>
    /// Gets whether this component has attached an assistant session snapshot.
    /// </summary>
    protected bool HasAssistantSession => this.assistantSessionId is not null;

    /// <summary>
    /// Gets the assistant-specific identifier used to distinguish session slots.
    /// </summary>
    protected virtual string AssistantSessionInstanceId => this.GetType().FullName ?? this.Component.ToString();
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (!this.SettingsManager.IsAssistantVisible(this.Component, assistantName: this.Title))
        {
            this.Logger.LogInformation("Assistant '{AssistantTitle}' is hidden. Redirecting to the assistants overview.", this.Title);
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
            return;
        }
        
        this.formChangeTimer.AutoReset = false;
        this.formChangeTimer.Elapsed += async (_, _) =>
        {
            this.formChangeTimer.Stop();
            await this.OnFormChange();
        };
        
        this.MightPreselectValues();
        this.ProviderSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        this.CurrentProfile = this.SettingsManager.GetPreselectedProfile(this.Component);
        this.CurrentChatTemplate = this.SettingsManager.GetPreselectedChatTemplate(this.Component);
        this.assistantSessionKey = new(this.Component, this.AssistantSessionInstanceId);
        await this.AttachAssistantSessionIfAvailable();
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

    private IReadOnlyList<Tools.Components> VisibleSendToAssistants => Enum.GetValues<AIStudio.Tools.Components>()
        .Where(this.CanSendToAssistant)
        .OrderBy(component => component.Name().Length)
        .ToArray();
    
    protected string? ValidatingProvider(AIStudio.Settings.Provider provider)
    {
        if(provider.UsedLLMProvider == LLMProviders.NONE)
            return this.TB("Please select a provider.");
        
        return null;
    }

    private async Task Start()
    {
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
    /// Add an issue to the UI.
    /// </summary>
    /// <param name="issue">The issue to add.</param>
    protected void AddInputIssue(string issue)
    {
        Array.Resize(ref this.InputIssues, this.InputIssues.Length + 1);
        this.InputIssues[^1] = issue;
        this.InputIsValid = false;
        _ = this.RefreshAssistantUIAsync();
    }
    
    /// <summary>
    /// Clear all input issues.
    /// </summary>
    protected void ClearInputIssues()
    {
        this.InputIssues = [];
        this.InputIsValid = true;
        _ = this.RefreshAssistantUIAsync();
    }

    protected void CreateChatThread()
    {
        this.ChatThread = new()
        {
            IncludeDateTime = false,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = this.AllowProfiles ? this.CurrentProfile.Id : Profile.NO_PROFILE.Id,
            SystemPrompt = this.SystemPrompt,
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = string.Format(this.TB("Assistant - {0}"), this.Title),
            Blocks = [],
        };
    }

    protected Guid CreateChatThread(Guid workspaceId, string name)
    {
        var chatId = Guid.NewGuid();
        this.ChatThread = new()
        {
            IncludeDateTime = false,
            SelectedProvider = this.ProviderSettings.Id,
            SelectedProfile = this.AllowProfiles ? this.CurrentProfile.Id : Profile.NO_PROFILE.Id,
            SystemPrompt = this.SystemPrompt,
            WorkspaceId = workspaceId,
            ChatId = chatId,
            Name = name,
            Blocks = [],
        };
        
        return chatId;
    }

    protected virtual void ResetProviderAndProfileSelection()
    {
        this.ProviderSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        this.CurrentProfile = this.SettingsManager.GetPreselectedProfile(this.Component);
        this.CurrentChatTemplate = this.SettingsManager.GetPreselectedChatTemplate(this.Component);
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

    protected async Task<string> AddAIResponseAsync(DateTimeOffset time, bool hideContentFromUser = false)
    {
        var manageCancellationLocally = this.CancellationTokenSource is null;
        this.CancellationTokenSource ??= new CancellationTokenSource();
        
        var aiText = new ContentText
        {
            // We have to wait for the remote
            // for the content stream: 
            InitialRemoteWait = true,
        };

        aiText.StreamingEvent = async () =>
        {
            await this.CheckpointAssistantSession();
            await this.RefreshAssistantUIAsync();
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
    
    private async Task CancelStreaming()
    {
        await this.AssistantSessionService.CancelAsync(this.assistantSessionKey, this);
    }
    
    protected async Task CopyToClipboard()
    {
        await this.RustService.CopyText2Clipboard(this.Snackbar, this.Result2Copy());
    }

    private ChatThread CreateSendToChatThread()
    {
        var originalChatThread = this.ChatThread ?? new ChatThread();
        if (string.IsNullOrWhiteSpace(this.SendToChatVisibleUserPromptText))
            return originalChatThread with
            {
                SystemPrompt = SystemPrompts.DEFAULT,
            };

        var earliestBlock = originalChatThread.Blocks.MinBy(x => x.Time);
        var visiblePromptTime = earliestBlock is null
            ? DateTimeOffset.Now
            : earliestBlock.Time == DateTimeOffset.MinValue
                ? earliestBlock.Time
                : earliestBlock.Time.AddTicks(-1);

        var transferredBlocks = originalChatThread.Blocks
            .Select(block => block.Role is ChatRole.USER
                ? block.DeepClone(changeHideState: true)
                : block.DeepClone())
            .ToList();

        transferredBlocks.Insert(0, new ContentBlock
        {
            Time = visiblePromptTime,
            ContentType = ContentType.TEXT,
            HideFromUser = false,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = this.SendToChatVisibleUserPromptText,
            },
        });

        return originalChatThread with
        {
            SystemPrompt = SystemPrompts.DEFAULT,
            Blocks = transferredBlocks,
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
        if (!this.CanSendToAssistant(destination))
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

        var sendToData = destination.GetData();
        if (destination is not Tools.Components.CHAT && this.AssistantSessionService.GetSnapshots().Any(snapshot => snapshot.IsActive && snapshot.Key.Component == destination))
        {
            await this.MessageBus.SendWarning(new(Icons.Material.Filled.Apps, this.TB("This assistant is already running. AI Studio opens the running session instead.")));
            this.NavigationManager.NavigateTo(sendToData.Route);
            return;
        }

        if (destination is not Tools.Components.CHAT)
            await this.AssistantSessionService.ClearInactiveSessionsForComponentAsync(destination);

        switch (destination)
        {
            case Tools.Components.CHAT:
                if (sendToButton.SendToChatAsInput)
                    MessageBus.INSTANCE.DeferMessage(this, Event.SEND_TO_CHAT_INPUT, contentToSend);
                else
                {
                    var convertedChatThread = this.ConvertToChatThread;
                    convertedChatThread = convertedChatThread with { SelectedProvider = this.ProviderSettings.Id };
                    MessageBus.INSTANCE.DeferMessage(this, sendToData.Event, convertedChatThread);
                }
                break;
            
            default:
                MessageBus.INSTANCE.DeferMessage(this, sendToData.Event, contentToSend);
                break;
        }

        this.NavigationManager.NavigateTo(sendToData.Route);
    }

    private bool CanSendToAssistant(Tools.Components component)
    {
        if (!component.AllowSendTo())
            return false;

        return this.SettingsManager.IsAssistantVisible(component, withLogging: false);
    }
    
    private async Task InnerResetForm()
    {
        if (this.AssistantSessionService.TryGetSnapshot(this.assistantSessionKey)?.IsActive ?? false)
            return;

        await this.AssistantSessionService.ClearAsync(this.assistantSessionKey);
        this.assistantSessionId = null;
        this.ResultingContentBlock = null;
        this.ProviderSettings = Settings.Provider.NONE;
        
        await this.JsRuntime.ClearDiv(RESULT_DIV_ID);
        await this.JsRuntime.ClearDiv(AFTER_RESULT_DIV_ID);
        
        this.ResetForm();
        this.ResetProviderAndProfileSelection();
        
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

    #endregion

    #region Assistant sessions

    /// <summary>
    /// Stores the current assistant UI and chat state in the active assistant session.
    /// </summary>
    /// <returns>A task that completes after the checkpoint was stored and published.</returns>
    private Task CheckpointAssistantSession()
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
    /// <param name="snapshot">The snapshot to attach.</param>
    /// <param name="restoreClientOnlyContent">Whether derived assistants should restore client-only UI state.</param>
    /// <returns>A task that completes after the component was refreshed.</returns>
    private async Task AttachAssistantSession(AssistantSessionSnapshot snapshot, bool restoreClientOnlyContent)
    {
        this.assistantSessionId = snapshot.SessionId;
        this.ImportAssistantSessionState(snapshot.State);
        this.ChatThread = snapshot.ChatThread ?? this.ChatThread;
        this.IsProcessing = snapshot.IsActive;

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
    /// <returns>A task that completes after the renderer was notified.</returns>
    private async Task RefreshAssistantUIAsync()
    {
        if (this.isDisposed)
            return;

        try
        {
            await this.InvokeAsync(this.StateHasChanged);
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
        this.RestoreCustomAssistantSessionState(reader);
    }

    /// <summary>
    /// Restores assistant-specific state values.
    /// </summary>
    /// <param name="state">The typed state reader to read from.</param>
    protected virtual void RestoreCustomAssistantSessionState(AssistantSessionStateReader state) { }

    #endregion
}