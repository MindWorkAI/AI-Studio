using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public abstract partial class AssistantBase : ComponentBase
{
    [Inject]
    protected SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    protected IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    protected ThreadSafeRandom RNG { get; init; } = null!;
    
    protected abstract string Title { get; }
    
    protected abstract string Description { get; }
    
    protected abstract string SystemPrompt { get; }
    
    private protected virtual RenderFragment? Body => null;

    protected virtual bool ShowResult => true;

    protected static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    protected AIStudio.Settings.Provider providerSettings;
    protected MudForm? form;
    protected bool inputIsValid;
    
    private ChatThread? chatThread;
    private ContentBlock? resultingContentBlock;
    private string[] inputIssues = [];
    
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

    protected async Task AddAIResponseAsync(DateTimeOffset time)
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
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        await aiText.CreateFromProviderAsync(this.providerSettings.CreateProvider(), this.JsRuntime, this.SettingsManager, this.providerSettings.Model, this.chatThread);
    }
}