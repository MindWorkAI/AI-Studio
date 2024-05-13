using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using MudBlazor;

namespace AIStudio.Components.Pages;

/// <summary>
/// The chat page.
/// </summary>
public partial class Chat : ComponentBase
{
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    public IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    public Random RNG { get; set; } = null!;
    
    private AIStudio.Settings.Provider selectedProvider;
    private ChatThread? chatThread;
    private bool isStreaming;
    private string userInput = string.Empty;
    
    // Unfortunately, we need the input field reference to clear it after sending a message.
    // This is necessary because we have to handle the key events ourselves. Otherwise,
    // the clearing would be done automatically.
    private MudTextField<string> inputField = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Ensure that the settings are loaded:
        await this.SettingsManager.LoadSettings();
        
        // For now, we just create a new chat thread.
        // Later we want the chats to be persisted
        // across page loads and organize them in
        // a chat history & workspaces.
        this.chatThread = new("Thread 1", this.RNG.Next(), "You are a helpful assistant!", []);
        await base.OnInitializedAsync();
    }

    #endregion

    private bool IsProviderSelected => this.selectedProvider.UsedProvider != Providers.NONE;
    
    private string ProviderPlaceholder => this.IsProviderSelected ? "Type your input here..." : "Select a provider first";

    private string InputLabel => this.IsProviderSelected ? $"Your Prompt (use selected instance '{this.selectedProvider.InstanceName}', provider '{this.selectedProvider.UsedProvider.ToName()}')" : "Select a provider first";
    
    private async Task SendMessage()
    {
        if (!this.IsProviderSelected)
            return;
        
        //
        // Add the user message to the thread:
        //
        var time = DateTimeOffset.Now;
        this.chatThread?.Blocks.Add(new ContentBlock(time, ContentType.TEXT, new ContentText
        {
            // Text content properties:
            Text = this.userInput,
        })
        {
            // Content block properties:
            Role = ChatRole.USER,
        });

        //
        // Add the AI response to the thread:
        //
        var aiText = new ContentText
        {
            // We have to wait for the remote
            // for the content stream: 
            InitialRemoteWait = true,
        };
        
        this.chatThread?.Blocks.Add(new ContentBlock(time, ContentType.TEXT, aiText)
        {
            Role = ChatRole.AI,
        });
        
        // Clear the input field:
        await this.inputField.Clear();
        this.userInput = string.Empty;
        
        // Enable the stream state for the chat component:
        this.isStreaming = true;
        this.StateHasChanged();
        
        // Use the selected provider to get the AI response.
        // By awaiting this line, we wait for the entire
        // content to be streamed.
        await aiText.CreateFromProviderAsync(this.selectedProvider.UsedProvider.CreateProvider(), this.JsRuntime, this.SettingsManager, new Model("gpt-4o"), this.chatThread);
        
        // Disable the stream state:
        this.isStreaming = false;
        this.StateHasChanged();
    }

    private async Task InputKeyEvent(KeyboardEventArgs keyEvent)
    {
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
}