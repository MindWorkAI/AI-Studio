using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Pages.IconFinder;

public partial class AssistantIconFinder : ComponentBase
{
    [Inject]
    private SettingsManager SettingsManager { get; set; } = null!;
    
    [Inject]
    public IJSRuntime JsRuntime { get; init; } = null!;

    [Inject]
    public Random RNG { get; set; } = null!;
    
    private ChatThread? chatThread;
    private ContentBlock? resultingContentBlock;
    private AIStudio.Settings.Provider selectedProvider;
    private MudForm form = null!;
    private bool inputIsValid;
    private string[] inputIssues = [];
    private string inputContext = string.Empty;
    private IconSources selectedIconSource;

    #region Overrides of ComponentBase

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(firstRender)
            this.form.ResetValidation();
        
        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    private string? ValidatingContext(string context)
    {
        if(string.IsNullOrWhiteSpace(context))
            return "Please provide a context. This will help the AI to find the right icon. You might type just a keyword or copy a sentence from your text, e.g., from a slide where you want to use the icon.";
        
        return null;
    }
    
    private string? ValidatingProvider(AIStudio.Settings.Provider provider)
    {
        if(provider.UsedProvider == Providers.NONE)
            return "Please select a provider.";
        
        return null;
    }

    private async Task FindIcon()
    {
        await this.form.Validate();
        if (!this.inputIsValid)
            return;
        
        //
        // Create a new chat thread:
        //
        this.chatThread = new()
        {
            WorkspaceId = Guid.Empty,
            ChatId = Guid.NewGuid(),
            Name = string.Empty,
            Seed = this.RNG.Next(),
            SystemPrompt = SYSTEM_PROMPT,
            Blocks = [],
        };
        
        //
        // Add the user's request to the thread:
        //
        var time = DateTimeOffset.Now;
        this.chatThread.Blocks.Add(new ContentBlock
        {
            Time = time,
            ContentType = ContentType.TEXT,
            Role = ChatRole.USER,
            Content = new ContentText
            {
                Text =
                $"""
                   {this.selectedIconSource.Prompt()} I search for an icon for the following context:
                   
                   ```
                   {this.inputContext}
                   ```
                """,
            },
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
    
    private const string SYSTEM_PROMPT = 
        """
        I can search for icons using US English keywords. Please help me come up with the right search queries.
        I don't want you to translate my requests word-for-word into US English. Instead, you should provide keywords
        that are likely to yield suitable icons. For example, I might ask for an icon about departments, but icons
        related to the keyword "buildings" might be the best match. Provide your keywords in a Markdown list without
        quotation marks.
        """;
}