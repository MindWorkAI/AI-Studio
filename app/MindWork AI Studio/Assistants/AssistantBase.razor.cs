using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Dialogs.Settings;
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
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }

    protected abstract Tools.Components Component { get; }
    
    protected virtual Func<string> Result2Copy => () => this.resultingContentBlock is null ? string.Empty : this.resultingContentBlock.Content switch
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
    
    protected AIStudio.Settings.Provider ProviderSettings = Settings.Provider.NONE;
    protected MudForm? Form;
    protected bool InputIsValid;
    protected Profile CurrentProfile = Profile.NO_PROFILE;
    protected ChatTemplate CurrentChatTemplate = ChatTemplate.NO_CHAT_TEMPLATE;
    protected ChatThread? ChatThread;
    protected IContent? LastUserPrompt;
    protected CancellationTokenSource? CancellationTokenSource;
    
    private readonly Timer formChangeTimer = new(TimeSpan.FromSeconds(1.6));

    private ContentBlock? resultingContentBlock;
    private string[] inputIssues = [];
    private bool isProcessing;
    
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
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    private string TB(string fallbackEN) => this.T(fallbackEN, typeof(AssistantBase<TSettings>).Namespace, nameof(AssistantBase<TSettings>));

    private string SubmitButtonStyle => this.SettingsManager.ConfigurationData.LLMProviders.ShowProviderConfidence ? this.ProviderSettings.UsedLLMProvider.GetConfidence(this.SettingsManager).StyleBorder(this.SettingsManager) : string.Empty;

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
        using (this.CancellationTokenSource = new())
        {
            await this.SubmitAction();
        }
        
        this.CancellationTokenSource = null;
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
        Array.Resize(ref this.inputIssues, this.inputIssues.Length + 1);
        this.inputIssues[^1] = issue;
        this.InputIsValid = false;
        this.StateHasChanged();
    }
    
    /// <summary>
    /// Clear all input issues.
    /// </summary>
    protected void ClearInputIssues()
    {
        this.inputIssues = [];
        this.InputIsValid = true;
        this.StateHasChanged();
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

        this.resultingContentBlock = new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
            HideFromUser = hideContentFromUser,
        };

        if (this.ChatThread is not null)
        {
            this.ChatThread.Blocks.Add(this.resultingContentBlock);
            this.ChatThread.SelectedProvider = this.ProviderSettings.Id;
        }

        this.isProcessing = true;
        this.StateHasChanged();
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        this.ChatThread = await aiText.CreateFromProviderAsync(this.ProviderSettings.CreateProvider(), this.ProviderSettings.Model, this.LastUserPrompt, this.ChatThread, this.CancellationTokenSource!.Token);
        
        this.isProcessing = false;
        this.StateHasChanged();
        
        if(manageCancellationLocally)
        {
            this.CancellationTokenSource.Dispose();
            this.CancellationTokenSource = null;
        }
        
        // Return the AI response:
        return aiText.Text;
    }
    
    private async Task CancelStreaming()
    {
        if (this.CancellationTokenSource is not null)
            if(!this.CancellationTokenSource.IsCancellationRequested)
                await this.CancellationTokenSource.CancelAsync();
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
    
    protected Task SendToAssistant(Tools.Components destination, SendToButton sendToButton)
    {
        if (!this.CanSendToAssistant(destination))
            return Task.CompletedTask;
        
        var contentToSend = sendToButton == default ? string.Empty : sendToButton.UseResultingContentBlockData switch
        {
            false => sendToButton.GetText(),
            true => this.resultingContentBlock?.Content switch
            {
                ContentText textBlock => textBlock.Text,
                _ => string.Empty,
            },
        };

        var sendToData = destination.GetData();
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
        return Task.CompletedTask;
    }

    private bool CanSendToAssistant(Tools.Components component)
    {
        if (!component.AllowSendTo())
            return false;

        return this.SettingsManager.IsAssistantVisible(component, withLogging: false);
    }
    
    private async Task InnerResetForm()
    {
        this.resultingContentBlock = null;
        this.ProviderSettings = Settings.Provider.NONE;
        
        await this.JsRuntime.ClearDiv(RESULT_DIV_ID);
        await this.JsRuntime.ClearDiv(AFTER_RESULT_DIV_ID);
        
        this.ResetForm();
        this.ResetProviderAndProfileSelection();
        
        this.InputIsValid = false;
        this.inputIssues = [];
        
        this.Form?.ResetValidation();
        this.StateHasChanged();
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
}
