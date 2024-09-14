using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

using RustService = AIStudio.Tools.RustService;

namespace AIStudio.Assistants;

public abstract partial class AssistantBase : ComponentBase
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
    
    internal const string AFTER_RESULT_DIV_ID = "afterAssistantResult";
    internal const string RESULT_DIV_ID = "assistantResult";
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }
    
    public abstract Tools.Components Component { get; }
    
    protected virtual Func<string> Result2Copy => () => this.resultingContentBlock is null ? string.Empty : this.resultingContentBlock.Content switch
    {
        ContentText textBlock => textBlock.Text,
        _ => string.Empty,
    };

    protected abstract void ResetFrom();

    protected abstract bool MightPreselectValues();
    
    protected abstract string SubmitText { get; }
    
    protected abstract Func<Task> SubmitAction { get; }

    protected virtual bool SubmitDisabled => false;
    
    private protected virtual RenderFragment? Body => null;

    protected virtual bool ShowResult => true;

    protected virtual bool AllowProfiles => true;

    protected virtual bool ShowProfileSelection => true;
    
    protected virtual bool ShowDedicatedProgress => false;

    protected virtual ChatThread ConvertToChatThread => this.chatThread ?? new();

    protected virtual IReadOnlyList<IButtonData> FooterButtons => [];

    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    protected AIStudio.Settings.Provider providerSettings;
    protected MudForm? form;
    protected bool inputIsValid;
    protected Profile currentProfile = Profile.NO_PROFILE;
    
    protected ChatThread? chatThread;
    private ContentBlock? resultingContentBlock;
    private string[] inputIssues = [];
    private bool isProcessing;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MightPreselectValues();
        this.providerSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        this.currentProfile = this.SettingsManager.GetPreselectedProfile(this.Component);
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

    private string SubmitButtonStyle => this.SettingsManager.ConfigurationData.LLMProviders.ShowProviderConfidence ? this.providerSettings.UsedLLMProvider.GetConfidence(this.SettingsManager).StyleBorder() : string.Empty;
    
    protected string? ValidatingProvider(AIStudio.Settings.Provider provider)
    {
        if(provider.UsedLLMProvider == LLMProviders.NONE)
            return "Please select a provider.";
        
        return null;
    }
    
    protected void CreateChatThread()
    {
        this.chatThread = new()
        {
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
    
    protected DateTimeOffset AddUserRequest(string request)
    {
        var time = DateTimeOffset.Now;
        this.chatThread!.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
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
        
        this.chatThread?.Blocks.Add(this.resultingContentBlock);
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
    
    private Task SendToAssistant(Tools.Components destination, SendToButton sendToButton)
    {
        var contentToSend = sendToButton.UseResultingContentBlockData switch
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
                MessageBus.INSTANCE.DeferMessage(this, sendToData.Event, this.ConvertToChatThread);
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
        
        this.ResetFrom();
        this.providerSettings = this.SettingsManager.GetPreselectedProvider(this.Component);
        
        this.inputIsValid = false;
        this.inputIssues = [];
        
        this.form?.ResetValidation();
        this.StateHasChanged();
        this.form?.ResetValidation();
    }
}