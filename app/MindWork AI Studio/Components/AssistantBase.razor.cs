using AIStudio.Chat;
using AIStudio.Components.Pages;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

using Path = AIStudio.Components.Pages.Path;

namespace AIStudio.Components;

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
    internal const string ASSISTANT_RESULT_DIV_ID = "assistantResult";
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }
    
    private protected virtual RenderFragment? Body => null;

    protected virtual bool ShowResult => true;
    
    protected virtual bool ShowDedicatedProgress => false;

    protected virtual IReadOnlyList<IButtonData> FooterButtons => [];

    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    protected AIStudio.Settings.Provider providerSettings;
    protected MudForm? form;
    protected bool inputIsValid;
    
    private ChatThread? chatThread;
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
    
    private Task SendToAssistant(SendToAssistant assistant, SendToButton sendToButton)
    {
        var contentToSend = sendToButton.UseResultingContentBlockData switch
        {
            false => sendToButton.GetData(),
            true => this.resultingContentBlock?.Content switch
            {
                ContentText textBlock => textBlock.Text,
                _ => string.Empty,
            },
        };

        var (eventItem, path) = assistant switch
        {
            Pages.SendToAssistant.AGENDA_ASSISTANT => (Event.SEND_TO_AGENDA_ASSISTANT, Path.ASSISTANT_AGENDA),
            Pages.SendToAssistant.CODING_ASSISTANT => (Event.SEND_TO_CODING_ASSISTANT, Path.ASSISTANT_CODING),
            Pages.SendToAssistant.REWRITE_ASSISTANT => (Event.SEND_TO_REWRITE_ASSISTANT, Path.ASSISTANT_REWRITE),
            Pages.SendToAssistant.TRANSLATION_ASSISTANT => (Event.SEND_TO_TRANSLATION_ASSISTANT, Path.ASSISTANT_TRANSLATION),
            Pages.SendToAssistant.ICON_FINDER_ASSISTANT => (Event.SEND_TO_ICON_FINDER_ASSISTANT, Path.ASSISTANT_ICON_FINDER),
            Pages.SendToAssistant.GRAMMAR_SPELLING_ASSISTANT => (Event.SEND_TO_GRAMMAR_SPELLING_ASSISTANT, Path.ASSISTANT_GRAMMAR_SPELLING),
            Pages.SendToAssistant.TEXT_SUMMARIZER_ASSISTANT => (Event.SEND_TO_TEXT_SUMMARIZER_ASSISTANT, Path.ASSISTANT_SUMMARIZER),
            
            _ => (Event.NONE, Path.ASSISTANTS),
        };
        
        MessageBus.INSTANCE.DeferMessage(this, eventItem, contentToSend);
        this.NavigationManager.NavigateTo(path);
        return Task.CompletedTask;
    }
}