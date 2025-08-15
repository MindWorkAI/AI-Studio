using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Provider;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using Timer = System.Timers.Timer;

namespace AIStudio.Pages;

public partial class Writer : MSGComponentBase
{
    [Inject]
    private ILogger<Chat> Logger { get; init; } = null!;
    
    private static readonly Dictionary<string, object?> USER_INPUT_ATTRIBUTES = new();
    private readonly Timer typeTimer = new(TimeSpan.FromMilliseconds(1_500));
    
    private MudTextField<string> textField = null!;
    private AIStudio.Settings.Provider providerSettings;
    private ChatThread? chatThread;
    private bool isStreaming;
    private string userInput = string.Empty;
    private string userDirection = string.Empty;
    private string suggestion = string.Empty;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.SettingsManager.InjectSpellchecking(USER_INPUT_ATTRIBUTES);
        this.typeTimer.Elapsed += async (_, _) => await this.InvokeAsync(this.GetSuggestions);
        this.typeTimer.AutoReset = false;
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private bool IsProviderSelected => this.providerSettings.UsedLLMProvider != LLMProviders.NONE;
    
    private async Task InputKeyEvent(KeyboardEventArgs keyEvent)
    {
        var key = keyEvent.Code.ToLowerInvariant();
        var isTab = key is "tab";
        var isModifier = keyEvent.AltKey || keyEvent.CtrlKey || keyEvent.MetaKey || keyEvent.ShiftKey;

        if (isTab && !isModifier)
        {
            await this.textField.FocusAsync();
            this.AcceptNextWord();
            return;
        }

        if (isTab && isModifier)
        {
            await this.textField.FocusAsync();
            this.AcceptEntireSuggestion();
            return;
        }
        
        if(!isModifier)
        {
            this.typeTimer.Stop();
            this.typeTimer.Start();
        }
    }

    private async Task GetSuggestions()
    {
        if (!this.IsProviderSelected)
            return;
        
        this.chatThread ??= new()
        {
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = string.Empty,
            Seed = 798798,
            SystemPrompt =  """
                            You are an assistant who helps with writing documents. You receive a sample
                            from a document as input. As output, you provide how the begun sentence could
                            continue. You give exactly one variant, not multiple. If the current sentence
                            is complete, you provide an empty response. You do not ask questions, and you
                            do not repeat the task.
                            """,
            Blocks = [],
        };
        
        var time = DateTimeOffset.Now;
        var lastUserPrompt = new ContentText
        {
            // We use the maximum 160 characters from the end of the text:
            Text = this.userInput.Length > 160 ? this.userInput[^160..] : this.userInput,
        };
        
        this.chatThread.Blocks.Clear();
        this.chatThread.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = lastUserPrompt,
        });
        
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
        
        this.isStreaming = true;
        this.StateHasChanged();
        
        this.chatThread = await aiText.CreateFromProviderAsync(this.providerSettings.CreateProvider(this.Logger), this.providerSettings.Model, lastUserPrompt, this.chatThread);
        this.suggestion = aiText.Text;
        
        this.isStreaming = false;
        this.StateHasChanged();
    }
    
    private void AcceptEntireSuggestion()
    {
        if(this.userInput.Last() != ' ')
            this.userInput += ' ';
        
        this.userInput += this.suggestion;
        this.suggestion = string.Empty;
        this.StateHasChanged();
    }
    
    private void AcceptNextWord()
    {
        var words = this.suggestion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if(words.Length == 0)
            return;
     
        if(this.userInput.Last() != ' ')
            this.userInput += ' ';
        
        this.userInput += words[0] + ' ';
        this.suggestion = string.Join(' ', words.Skip(1));
        this.StateHasChanged();
    }

    #region Overrides of MSGComponentBase

    protected override void DisposeResources()
    {
        try
        {
            this.typeTimer.Stop();
            this.typeTimer.Dispose();
        }
        catch
        {
            // ignore
        }
        
        base.DisposeResources();
    }

    #endregion
}