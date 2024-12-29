using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using MudBlazor.Utilities;

using RustService = AIStudio.Tools.RustService;
using Timer = System.Timers.Timer;

namespace AIStudio.Assistants;

public abstract partial class AssistantBase : ComponentBase, IMessageBusReceiver, IDisposable
{
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    protected ThreadSafeRandom RNG { get; init; } = null!;
    
    [Inject]
    protected ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    protected RustService RustService { get; init; } = null!;
    
    [Inject]
    protected NavigationManager NavigationManager { get; init; } = null!;
    
    [Inject]
    protected ILogger<AssistantBase> Logger { get; init; } = null!;
    
    [Inject]
    private MudTheme ColorTheme { get; init; } = null!;
    
    [Inject]
    private MessageBus MessageBus { get; init; } = null!;
    
    internal const string RESULT_DIV_ID = "assistantResult";
    internal const string BEFORE_RESULT_DIV_ID = "beforeAssistantResult";
    internal const string AFTER_RESULT_DIV_ID = "afterAssistantResult";
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }
    
    public abstract Tools.Components Component { get; }
    
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

    protected virtual bool AllowProfiles => true;

    protected virtual bool ShowProfileSelection => true;
    
    protected virtual bool ShowDedicatedProgress => false;
    
    protected virtual bool ShowSendTo => true;
    
    protected virtual bool ShowCopyResult => true;
    
    protected virtual bool ShowReset => true;

    protected virtual ChatThread ConvertToChatThread => this.chatThread ?? new();

    protected virtual IReadOnlyList<IButtonData> FooterButtons => [];

    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    protected AIStudio.Settings.Provider providerSettings;
    protected MudForm? form;
    protected bool inputIsValid;
    protected Profile currentProfile = Profile.NO_PROFILE;
    protected ChatThread? chatThread;
    
    private readonly Timer formChangeTimer = new(TimeSpan.FromSeconds(1.6));
    
    private ContentBlock? resultingContentBlock;
    private string[] inputIssues = [];
    private bool isProcessing;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.formChangeTimer.AutoReset = false;
        this.formChangeTimer.Elapsed += async (_, _) =>
        {
            this.formChangeTimer.Stop();
            await this.OnFormChange();
        };
        
        this.MightPreselectValues();
        this.providerSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        this.currentProfile = this.SettingsManager.GetPreselectedProfile(this.Component);
        
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.COLOR_THEME_CHANGED ]);
        
        await base.OnInitializedAsync();
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
            this.form?.ResetValidation();
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
    
    #region Implementation of IMessageBusReceiver

    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.COLOR_THEME_CHANGED:
                this.StateHasChanged();
                break;
        }
        
        return Task.CompletedTask;
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion

    private string SubmitButtonStyle => this.SettingsManager.ConfigurationData.LLMProviders.ShowProviderConfidence ? this.providerSettings.UsedLLMProvider.GetConfidence(this.SettingsManager).StyleBorder(this.SettingsManager) : string.Empty;
    
    protected string? ValidatingProvider(AIStudio.Settings.Provider provider)
    {
        if(provider.UsedLLMProvider == LLMProviders.NONE)
            return "Please select a provider.";
        
        return null;
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
        this.inputIsValid = false;
        this.StateHasChanged();
    }

    protected void CreateChatThread()
    {
        this.chatThread = new()
        {
            SelectedProvider = this.providerSettings.Id,
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = string.Empty,
            Seed = this.RNG.Next(),
            SystemPrompt = !this.AllowProfiles ? this.SystemPrompt :
                $"""
                {this.SystemPrompt}
                
                {this.currentProfile.ToSystemPrompt()}
                """,
            Blocks = [],
        };
    }

    protected Guid CreateChatThread(Guid workspaceId, string name)
    {
        var chatId = Guid.NewGuid();
        this.chatThread = new()
        {
            SelectedProvider = this.providerSettings.Id,
            WorkspaceId = workspaceId,
            ChatId = chatId,
            Name = name,
            Seed = this.RNG.Next(),
            SystemPrompt = !this.AllowProfiles ? this.SystemPrompt :
                $"""
                 {this.SystemPrompt}

                 {this.currentProfile.ToSystemPrompt()}
                 """,
            Blocks = [],
        };
        
        return chatId;
    }
    
    protected DateTimeOffset AddUserRequest(string request, bool hideContentFromUser = false)
    {
        var time = DateTimeOffset.Now;
        this.chatThread!.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            HideFromUser = hideContentFromUser,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = request,
            },
        });
        
        return time;
    }

    protected async Task<string> AddAIResponseAsync(DateTimeOffset time)
    {
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
        };

        if (this.chatThread is not null)
        {
            this.chatThread.Blocks.Add(this.resultingContentBlock);
            this.chatThread.SelectedProvider = this.providerSettings.Id;
        }

        this.isProcessing = true;
        this.StateHasChanged();
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        await aiText.CreateFromProviderAsync(this.providerSettings.CreateProvider(this.Logger), this.SettingsManager, this.providerSettings.Model, this.chatThread);
        
        this.isProcessing = false;
        this.StateHasChanged();
        
        // Return the AI response:
        return aiText.Text;
    }
    
    protected async Task CopyToClipboard()
    {
        await this.RustService.CopyText2Clipboard(this.Snackbar, this.Result2Copy());
    }
    
    private static string? GetButtonIcon(string icon)
    {
        if(string.IsNullOrWhiteSpace(icon))
            return null;
        
        return icon;
    }
    
    protected Task SendToAssistant(Tools.Components destination, SendToButton sendToButton)
    {
        if (!destination.AllowSendTo())
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
                var convertedChatThread = this.ConvertToChatThread;
                convertedChatThread = convertedChatThread with { SelectedProvider = this.providerSettings.Id };
                MessageBus.INSTANCE.DeferMessage(this, sendToData.Event, convertedChatThread);
                break;
            
            default:
                MessageBus.INSTANCE.DeferMessage(this, sendToData.Event, contentToSend);
                break;
        }

        this.NavigationManager.NavigateTo(sendToData.Route);
        return Task.CompletedTask;
    }
    
    private async Task InnerResetForm()
    {
        this.resultingContentBlock = null;
        this.providerSettings = default;
        
        await this.JsRuntime.ClearDiv(RESULT_DIV_ID);
        await this.JsRuntime.ClearDiv(AFTER_RESULT_DIV_ID);
        
        this.ResetForm();
        this.providerSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        
        this.inputIsValid = false;
        this.inputIssues = [];
        
        this.form?.ResetValidation();
        this.StateHasChanged();
        this.form?.ResetValidation();
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

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
        this.formChangeTimer.Dispose();
    }

    #endregion
}