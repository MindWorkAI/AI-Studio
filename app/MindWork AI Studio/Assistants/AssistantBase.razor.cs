using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants;

public abstract partial class AssistantBase : ComponentBase
{
    [Inject]
    protected SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    protected IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    protected ThreadSafeRandom RNG { get; init; } = null!;
    
    [Inject]
    protected ISnackbar Snackbar { get; init; } = null!;
    
    [Inject]
    protected Rust Rust { get; init; } = null!;
    
    [Inject]
    protected NavigationManager NavigationManager { get; init; } = null!;
    
    internal const string AFTER_RESULT_DIV_ID = "afterAssistantResult";
    internal const string RESULT_DIV_ID = "assistantResult";
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }

    protected abstract void ResetFrom();

    protected abstract bool MightPreselectValues();
    
    private protected virtual RenderFragment? Body => null;

    protected virtual bool ShowResult => true;
    
    protected virtual bool ShowDedicatedProgress => false;

    protected virtual ChatThread ConvertToChatThread => this.chatThread ?? new();

    protected virtual IReadOnlyList<IButtonData> FooterButtons => [];

    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    protected AIStudio.Settings.Provider providerSettings;
    protected MudForm? form;
    protected bool inputIsValid;
    
    protected ChatThread? chatThread;
    private ContentBlock? resultingContentBlock;
    private string[] inputIssues = [];
    private bool isProcessing;
    
    #region Overrides of ComponentBase

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
    
    protected string? ValidatingProvider(AIStudio.Settings.Provider provider)
    {
        if(provider.UsedProvider == Providers.NONE)
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
            SystemPrompt = this.SystemPrompt,
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
        await aiText.CreateFromProviderAsync(this.providerSettings.CreateProvider(), this.JsRuntime, this.SettingsManager, this.providerSettings.Model, this.chatThread);
        
        this.isProcessing = false;
        this.StateHasChanged();
        
        // Return the AI response:
        return aiText.Text;
    }
    
    protected async Task CopyToClipboard(string text)
    {
        await this.Rust.CopyText2Clipboard(this.JsRuntime, this.Snackbar, text);
    }
    
    private static string? GetButtonIcon(string icon)
    {
        if(string.IsNullOrWhiteSpace(icon))
            return null;
        
        return icon;
    }
    
    private Task SendToAssistant(SendTo destination, SendToButton sendToButton)
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

        var (eventItem, path) = destination switch
        {
            SendTo.AGENDA_ASSISTANT => (Event.SEND_TO_AGENDA_ASSISTANT, Routes.ASSISTANT_AGENDA),
            SendTo.CODING_ASSISTANT => (Event.SEND_TO_CODING_ASSISTANT, Routes.ASSISTANT_CODING),
            SendTo.REWRITE_ASSISTANT => (Event.SEND_TO_REWRITE_ASSISTANT, Routes.ASSISTANT_REWRITE),
            SendTo.EMAIL_ASSISTANT => (Event.SEND_TO_EMAIL_ASSISTANT, Routes.ASSISTANT_EMAIL),
            SendTo.TRANSLATION_ASSISTANT => (Event.SEND_TO_TRANSLATION_ASSISTANT, Routes.ASSISTANT_TRANSLATION),
            SendTo.ICON_FINDER_ASSISTANT => (Event.SEND_TO_ICON_FINDER_ASSISTANT, Routes.ASSISTANT_ICON_FINDER),
            SendTo.GRAMMAR_SPELLING_ASSISTANT => (Event.SEND_TO_GRAMMAR_SPELLING_ASSISTANT, Routes.ASSISTANT_GRAMMAR_SPELLING),
            SendTo.TEXT_SUMMARIZER_ASSISTANT => (Event.SEND_TO_TEXT_SUMMARIZER_ASSISTANT, Routes.ASSISTANT_SUMMARIZER),
            
            SendTo.CHAT => (Event.SEND_TO_CHAT, Routes.CHAT),
            
            _ => (Event.NONE, Routes.ASSISTANTS),
        };

        switch (destination)
        {
            case SendTo.CHAT:
                MessageBus.INSTANCE.DeferMessage(this, eventItem, this.ConvertToChatThread);
                break;
            
            default:
                MessageBus.INSTANCE.DeferMessage(this, eventItem, contentToSend);
                break;
        }

        this.NavigationManager.NavigateTo(path);
        return Task.CompletedTask;
    }
    
    private async Task InnerResetForm()
    {
        this.resultingContentBlock = null;
        this.providerSettings = default;
        
        await this.JsRuntime.ClearDiv(RESULT_DIV_ID);
        await this.JsRuntime.ClearDiv(AFTER_RESULT_DIV_ID);
        
        this.ResetFrom();
        
        this.inputIsValid = false;
        this.inputIssues = [];
        
        this.form?.ResetValidation();
        this.StateHasChanged();
        this.form?.ResetValidation();
    }
}