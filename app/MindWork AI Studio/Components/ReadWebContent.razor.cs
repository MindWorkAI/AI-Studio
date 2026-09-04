using AIStudio.Agents;
using AIStudio.Chat;
using AIStudio.Tools.Security;
using AIStudio.Tools.Web;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ReadWebContent : MSGComponentBase
{
    /// <summary>
    /// How long loading one page may take.
    /// </summary>
    /// <remarks>
    /// The user is watching a progress indicator while this runs, so it is shorter than what the
    /// tools allow themselves for a page fetched in the background.
    /// </remarks>
    private const int TIMEOUT_SECONDS = 60;

    [Inject]
    private WebPageRetrievalService WebPageRetrievalService { get; init; } = null!;

    [Inject]
    private ILogger<ReadWebContent> Logger { get; init; } = null!;

    [Inject]
    private AgentTextContentCleaner AgentTextContentCleaner { get; init; } = null!;

    [Inject]
    private PromptInjectionGuardService PromptInjectionGuardService { get; init; } = null!;

    [Parameter]
    public string Content { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> ContentChanged { get; set; }
    
    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; } = AIStudio.Settings.Provider.NONE;
    
    [Parameter]
    public bool AgentIsRunning { get; set; }
    
    [Parameter]
    public EventCallback<bool> AgentIsRunningChanged { get; set; }
    
    [Parameter]
    public bool Preselect { get; set; }
    
    [Parameter]
    public EventCallback<bool> PreselectChanged { get; set; }
    
    [Parameter]
    public bool PreselectContentCleanerAgent { get; set; }
    
    [Parameter]
    public EventCallback<bool> PreselectContentCleanerAgentChanged { get; set; }

    private readonly Process<ReadWebContentSteps> process = Process<ReadWebContentSteps>.INSTANCE;
    private ProcessStepValue processStep;
    
    private string providedURL = string.Empty;
    private bool urlIsValid;
    private bool isProviderValid;

    private AIStudio.Settings.Provider providerSettings = AIStudio.Settings.Provider.NONE;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ProviderSettings = this.SettingsManager.GetPreselectedProvider(Tools.Components.AGENT_TEXT_CONTENT_CLEANER, this.ProviderSettings.Id, true);
        this.providerSettings = this.ProviderSettings;
        this.ValidateProvider(this.PreselectContentCleanerAgent);
        
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!this.SettingsManager.ConfigurationData.TextContentCleaner.PreselectAgentOptions)
            this.providerSettings = this.ProviderSettings;
        
        this.ValidateProvider(this.PreselectContentCleanerAgent);
        await base.OnParametersSetAsync();
    }

    #endregion

    private async Task LoadFromWeb()
    {
        if(!this.IsReady)
            return;
        
        var markdown = string.Empty;
        try
        {
            this.processStep = this.process[ReadWebContentSteps.LOADING];
            this.StateHasChanged();

            //
            // The same retrieval the read web page tool uses, so a page is fetched and read one
            // way throughout AI Studio. The difference is the target policy: here the user typed
            // the URL, so their own network is not off limits.
            //
            var retrievedPage = await this.WebPageRetrievalService.RetrieveAsync(
                new Uri(this.providedURL),
                new WebPageRetrievalOptions
                {
                    TimeoutSeconds = TIMEOUT_SECONDS,
                    TargetChosenByUser = true,
                });

            this.processStep = this.process[ReadWebContentSteps.PARSING];
            this.StateHasChanged();
            markdown = retrievedPage.ExtractedPage.Markdown;
            markdown = await this.PromptInjectionGuardService.SanitizeAsync(markdown, PromptInjectionSource.WebContent(this.providedURL));
            
            if (this.PreselectContentCleanerAgent && this.providerSettings != AIStudio.Settings.Provider.NONE)
            {
                this.AgentTextContentCleaner.ProviderSettings = this.providerSettings;
                var additionalData = new Dictionary<string, string>
                {
                    { "sourceURL", this.providedURL },
                };
            
                this.processStep = this.process[ReadWebContentSteps.CLEANING];
                this.AgentIsRunning = true;
                await this.AgentIsRunningChanged.InvokeAsync(this.AgentIsRunning);
                this.StateHasChanged();
            
                var contentBlock = await this.AgentTextContentCleaner.ProcessInput(new ContentBlock
                {
                    Time = DateTimeOffset.UtcNow,
                    ContentType = ContentType.TEXT,
                    Role = ChatRole.USER,
                    Content = new ContentText
                    {
                        Text = markdown,
                    },
                }, additionalData);
            
                markdown = contentBlock.Content is ContentText text ? text.Text : markdown;
            
                this.processStep = this.process[ReadWebContentSteps.DONE];
                this.AgentIsRunning = false;
                await this.AgentIsRunningChanged.InvokeAsync(this.AgentIsRunning);
                this.StateHasChanged();
            }
        }
        catch (Exception exception)
        {
            if (this.AgentIsRunning)
            {
                this.processStep = this.process[ReadWebContentSteps.START];
                this.AgentIsRunning = false;
                await this.AgentIsRunningChanged.InvokeAsync(this.AgentIsRunning);
                this.StateHasChanged();
            }

            //
            // Say why nothing was loaded. An empty text field looks like a page without content,
            // and the reasons a page cannot be read are things the user can act on: a link to a
            // PDF rather than a page, a host that does not answer, a server refusing the request.
            //
            this.Logger.LogWarning(exception, "Could not load the web content from '{ProvidedUrl}'.", this.providedURL);
            await this.MessageBus.SendError(new(Icons.Material.Filled.CloudOff, string.Format(this.T("The content of '{0}' could not be loaded: {1}"), this.providedURL, exception.Message)));
        }

        this.Content = markdown;
        await this.ContentChanged.InvokeAsync(this.Content);
    }

    private bool IsReady
    {
        get
        {
            if(!this.urlIsValid)
                return false;
            
            if(this.PreselectContentCleanerAgent && !this.isProviderValid)
                return false;
            
            return true;
        }
    }

    private async Task ShowWebContentReaderChanged(bool state)
    {
        await this.PreselectChanged.InvokeAsync(state);
    }
    
    private async Task UseContentCleanerAgentChanged(bool state)
    {
        await this.PreselectContentCleanerAgentChanged.InvokeAsync(state);
    }
    
    private string? ValidateProvider(bool shouldUseAgent)
    {
        if(shouldUseAgent && this.providerSettings == AIStudio.Settings.Provider.NONE)
        {
            this.isProviderValid = false;
            return T("Please select a provider to use the cleanup agent.");
        }

        this.isProviderValid = true;
        return null;
    }
    
    private string? ValidateURL(string url)
    {
        if(string.IsNullOrWhiteSpace(url))
        {
            this.urlIsValid = false;
            return T("Please provide a URL to load the content from.");
        }

        var urlParsingResult = Uri.TryCreate(url, UriKind.Absolute, out var uriResult);
        if(!urlParsingResult)
        {
            this.urlIsValid = false;
            return T("Please provide a valid URL.");
        }

        if(uriResult is not { Scheme: "http" or "https" })
        {
            this.urlIsValid = false;
            return T("Please provide a valid HTTP or HTTPS URL.");
        }

        this.urlIsValid = true;
        return null;
    }
}