using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public abstract partial class AssistantBase : ComponentBase
{
    [Inject]
    protected SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    protected IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    protected Random RNG { get; set; } = null!;
    
    protected string Title { get; init; } = string.Empty;
    
    protected string Description { get; init; } = string.Empty;
    
    protected abstract string SystemPrompt { get; }
    
    private protected virtual RenderFragment? Body => null;

    protected AIStudio.Settings.Provider selectedProvider;
    protected MudForm? form;
    protected bool inputIsValid;
    
    private ChatThread? chatThread;
    private ContentBlock? resultingContentBlock;
    private string[] inputIssues = [];
    
    #region Overrides of ComponentBase

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(firstRender)
            this.form?.ResetValidation();
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion
    
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
        await aiText.CreateFromProviderAsync(this.selectedProvider.UsedProvider.CreateProvider(this.selectedProvider.InstanceName, this.selectedProvider.Hostname), this.JsRuntime, this.SettingsManager, this.selectedProvider.Model, this.chatThread);
    }
}