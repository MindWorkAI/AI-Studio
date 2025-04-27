using AIStudio.Agents;
using AIStudio.Chat;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ReadWebContent : MSGComponentBase
{
    [Inject]
    private HTMLParser HTMLParser { get; init; } = null!;
    
    [Inject]
    private AgentTextContentCleaner AgentTextContentCleaner { get; init; } = null!;

    [Parameter]
    public string Content { get; set; } = string.Empty;
    
    [Parameter]
    public EventCallback<string> ContentChanged { get; set; }
    
    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; }
    
    [Parameter]
    public bool AgentIsRunning { get; set; }
    
    [Parameter]
    public EventCallback<bool> AgentIsRunningChanged { get; set; }
    
    [Parameter]
    public bool Preselect { get; set; }
    
    [Parameter]
    public bool PreselectContentCleanerAgent { get; set; }

    private Process<ReadWebContentSteps> process = Process<ReadWebContentSteps>.INSTANCE;
    private ProcessStepValue processStep;
    
    private bool showWebContentReader;
    private bool useContentCleanerAgent;
    private string providedURL = string.Empty;
    private bool urlIsValid;
    private bool isProviderValid;

    private AIStudio.Settings.Provider providerSettings;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        if(this.Preselect)
            this.showWebContentReader = true;
        
        if(this.PreselectContentCleanerAgent)
            this.useContentCleanerAgent = true;
        
        this.ProviderSettings = this.SettingsManager.GetPreselectedProvider(Tools.Components.AGENT_TEXT_CONTENT_CLEANER, this.ProviderSettings.Id, true);
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!this.SettingsManager.ConfigurationData.TextContentCleaner.PreselectAgentOptions)
            this.providerSettings = this.ProviderSettings;
        
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
            
            var html = await this.HTMLParser.LoadWebContentHTML(new Uri(this.providedURL));
            
            this.processStep = this.process[ReadWebContentSteps.PARSING];
            this.StateHasChanged();
            markdown = this.HTMLParser.ParseToMarkdown(html);
            
            if (this.useContentCleanerAgent)
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
        catch
        {
            if (this.AgentIsRunning)
            {
                this.processStep = this.process[ReadWebContentSteps.START];
                this.AgentIsRunning = false;
                await this.AgentIsRunningChanged.InvokeAsync(this.AgentIsRunning);
                this.StateHasChanged();
            }
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
            
            if(this.useContentCleanerAgent && !this.isProviderValid)
                return false;
            
            return true;
        }
    }

    private string? ValidateProvider(bool shouldUseAgent)
    {
        if(shouldUseAgent && this.providerSettings == default)
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