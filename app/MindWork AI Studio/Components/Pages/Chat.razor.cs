using AIStudio.Chat;
using AIStudio.Components.Blocks;
using AIStudio.Components.CommonDialogs;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using DialogOptions = AIStudio.Components.CommonDialogs.DialogOptions;

namespace AIStudio.Components.Pages;

/// <summary>
/// The chat page.
/// </summary>
public partial class Chat : ComponentBase, IAsyncDisposable
{
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    public IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    public Random RNG { get; set; } = null!;
    
    [Inject]
    public IDialogService DialogService { get; set; } = null!;

    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    
    private AIStudio.Settings.Provider selectedProvider;
    private ChatThread? chatThread;
    private bool hasUnsavedChanges;
    private bool isStreaming;
    private string userInput = string.Empty;
    private bool workspacesVisible;
    private Workspaces? workspaces;
    
    // Unfortunately, we need the input field reference to clear it after sending a message.
    // This is necessary because we have to handle the key events ourselves. Otherwise,
    // the clearing would be done automatically.
    private MudTextField<string> inputField = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the user input:
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        
        await base.OnInitializedAsync();
    }

    #endregion

    private bool IsProviderSelected => this.selectedProvider.UsedProvider != Providers.NONE;
    
    private string ProviderPlaceholder => this.IsProviderSelected ? "Type your input here..." : "Select a provider first";

    private string InputLabel => this.IsProviderSelected ? $"Your Prompt (use selected instance '{this.selectedProvider.InstanceName}', provider '{this.selectedProvider.UsedProvider.ToName()}')" : "Select a provider first";
    
    private bool CanThreadBeSaved => this.IsProviderSelected && this.chatThread is not null && this.chatThread.Blocks.Count > 0;
    
    private async Task SendMessage()
    {
        if (!this.IsProviderSelected)
            return;
        
        // Create a new chat thread if necessary:
        var threadName = this.ExtractThreadName(this.userInput);
        this.chatThread ??= new()
        {
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = threadName,
            Seed = this.RNG.Next(),
            SystemPrompt = "You are a helpful assistant!",
            Blocks = [],
        };
        
        //
        // Add the user message to the thread:
        //
        var time = DateTimeOffset.Now;
        this.chatThread?.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text = this.userInput,
            },
        });

        // Save the chat:
        if (this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
            this.StateHasChanged();
        }

        //
        // Add the AI response to the thread:
        //
        var aiText = new ContentText
        {
            // We have to wait for the remote
            // for the content stream: 
            InitialRemoteWait = true,
        };
        
        this.chatThread?.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.AI,
            Content = aiText,
        });
        
        // Clear the input field:
        await this.inputField.Clear();
        this.userInput = string.Empty;
        
        // Enable the stream state for the chat component:
        this.isStreaming = true;
        this.hasUnsavedChanges = true;
        this.StateHasChanged();
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        await aiText.CreateFromProviderAsync(this.selectedProvider.UsedProvider.CreateProvider(this.selectedProvider.InstanceName, this.selectedProvider.Hostname), this.JsRuntime, this.SettingsManager, this.selectedProvider.Model, this.chatThread);
        
        // Save the chat:
        if (this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }

        // Disable the stream state:
        this.isStreaming = false;
        this.StateHasChanged();
    }

    private async Task InputKeyEvent(KeyboardEventArgs keyEvent)
    {
        this.hasUnsavedChanges = true;
        var key = keyEvent.Code.ToLowerInvariant();
        
        // Was the enter key (either enter or numpad enter) pressed?
        var isEnter = key is "enter" or "numpadenter";
        
        // Was a modifier key pressed as well?
        var isModifier = keyEvent.AltKey || keyEvent.CtrlKey || keyEvent.MetaKey || keyEvent.ShiftKey;
        
        // Depending on the user's settings, might react to shortcuts:
        switch (this.SettingsManager.ConfigurationData.ShortcutSendBehavior)
        {
            case SendBehavior.ENTER_IS_SENDING:
                if (!isModifier && isEnter)
                    await this.SendMessage();
                break;
            
            case SendBehavior.MODIFER_ENTER_IS_SENDING:
                if (isEnter && isModifier)
                    await this.SendMessage();
                break;
        }
    }
    
    private void ToggleWorkspaces()
    {
        this.workspacesVisible = !this.workspacesVisible;
    }
    
    private async Task SaveThread()
    {
        if(this.workspaces is null)
            return;
        
        if(this.chatThread is null)
            return;
        
        if (!this.CanThreadBeSaved)
            return;
        
        await this.workspaces.StoreChat(this.chatThread);
        this.hasUnsavedChanges = false;
    }
    
    private string ExtractThreadName(string firstUserInput)
    {
        // We select the first 10 words of the user input:
        var words = firstUserInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var threadName = string.Join(' ', words.Take(10));
        
        // If the thread name is empty, we use a default name:
        if (string.IsNullOrWhiteSpace(threadName))
            threadName = "Thread";
        
        return threadName;
    }

    private async Task StartNewChat()
    {
        if (this.hasUnsavedChanges)
        {
            var dialogParameters = new DialogParameters
            {
                { "Message", "Are you sure you want to start a new chat? All unsaved changes will be lost." },
            };
        
            var dialogReference = await this.DialogService.ShowAsync<ConfirmDialog>("Delete Chat", dialogParameters, DialogOptions.FULLSCREEN);
            var dialogResult = await dialogReference.Result;
            if (dialogResult.Canceled)
                return;
        }
        
        this.chatThread = null;
        this.isStreaming = false;
        this.hasUnsavedChanges = false;
        this.userInput = string.Empty;
        await this.inputField.Clear();
    }

    #region Implementation of IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if(this.SettingsManager.ConfigurationData.WorkspaceStorageBehavior is WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY)
        {
            await this.SaveThread();
            this.hasUnsavedChanges = false;
        }
    }

    #endregion
}