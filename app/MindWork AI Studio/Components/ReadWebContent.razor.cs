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

    /// <summary>
    /// The URL the content is loaded from.
    /// </summary>
    /// <remarks>
    /// The URL belongs to the parent, so that it is cleared when the parent resets its form and
    /// is kept when the parent stores its state.
    /// </remarks>
    [Parameter]
    public string URL { get; set; } = string.Empty;

    [Parameter]
    public EventCallback<string> URLChanged { get; set; }

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

    /// <summary>
    /// The model the content cleaner runs with.
    /// </summary>
    /// <remarks>
    /// This is a resolved value, not a chosen one: the reader has no model selection of its own,
    /// it takes what the assistant around it uses, unless a dedicated one for the cleaner or an
    /// app-wide default takes precedence. Because the assistant's model can change at any moment,
    /// this is resolved again on every render instead of being remembered from the first one.
    /// </remarks>
    private AIStudio.Settings.Provider providerSettings = AIStudio.Settings.Provider.NONE;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        this.ResolveProvider();

        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        this.ResolveProvider();
        await base.OnParametersSetAsync();
    }

    #endregion

    #region Overrides of MSGComponentBase

    protected override Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        if (triggeredEvent is Event.CONFIGURATION_CHANGED)
        {
            //
            // A dedicated model for the cleaner, or the app-wide default, may be set while this
            // assistant is open. Nothing about that reaches us as a parameter, so without this the
            // user would have to leave the assistant and come back for it to take effect.
            //
            this.ResolveProvider();
            this.StateHasChanged();
        }

        return Task.CompletedTask;
    }

    #endregion

    /// <summary>
    /// Determines the model the content cleaner runs with.
    /// </summary>
    /// <remarks>
    /// Called from both lifecycle methods, and with the same arguments: the assistant's model is a
    /// parameter, and a parameter arrives whenever the parent renders. Resolving only once would
    /// leave the cleaner with whatever was set the first time this component was built.
    /// </remarks>
    private void ResolveProvider() => this.providerSettings = this.SettingsManager.GetPreselectedProvider(Tools.Components.AGENT_TEXT_CONTENT_CLEANER, this.ProviderSettings.Id, true);

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
                new Uri(this.URL),
                new WebPageRetrievalOptions
                {
                    TimeoutSeconds = TIMEOUT_SECONDS,
                    TargetChosenByUser = true,
                });

            this.processStep = this.process[ReadWebContentSteps.PARSING];
            this.StateHasChanged();
            markdown = retrievedPage.ExtractedPage.Markdown;
            markdown = await this.PromptInjectionGuardService.SanitizeAsync(markdown, PromptInjectionSource.WebContent(this.URL));
            
            if (this.PreselectContentCleanerAgent && this.providerSettings == AIStudio.Settings.Provider.NONE)
            {
                //
                // Say that the cleaning did not happen. The user asked for it, the page arrives,
                // and without a word they would take the raw markdown -- navigation, cookie banner
                // and advertising included -- for the cleaned result.
                //
                await this.MessageBus.SendError(new(Icons.Material.Filled.SettingsSuggest, T("The content was loaded, but not cleaned: no model is available for the content cleaner.")));
            }

            if (this.PreselectContentCleanerAgent && this.providerSettings != AIStudio.Settings.Provider.NONE)
            {
                this.AgentTextContentCleaner.ProviderSettings = this.providerSettings;
                var additionalData = new Dictionary<string, string>
                {
                    { "sourceURL", this.URL },
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
            this.Logger.LogWarning(exception, "Could not load the web content from '{ProvidedUrl}'.", this.URL);
            await this.MessageBus.SendError(new(Icons.Material.Filled.CloudOff, string.Format(this.T("The content of '{0}' could not be loaded: {1}"), this.URL, exception.Message)));
        }

        this.Content = markdown;
        await this.ContentChanged.InvokeAsync(this.Content);
    }

    /// <summary>
    /// Whether the content can be fetched.
    /// </summary>
    /// <remarks>
    /// A missing model for the content cleaner is deliberately not part of this. Cleaning is an
    /// option of the fetch, not a condition for it: making it one would leave the user with a
    /// switch they turned on, no way to get their page, and a dead button to explain it. The page
    /// is fetched, and LoadFromWeb says that it arrived uncleaned.
    /// </remarks>
    private bool IsReady => this.UrlIsValid;

    /// <summary>
    /// Whether the current URL can be loaded.
    /// </summary>
    /// <remarks>
    /// Asked of the current value instead of remembered from the last validation run: the parent
    /// clears the URL when it resets its form, and the form validation does not run again at that
    /// point. The fetch button would otherwise stay enabled with an empty field.
    /// </remarks>
    private bool UrlIsValid => this.ValidateURL(this.URL) is null;

    private async Task URLValueChanged(string url)
    {
        await this.URLChanged.InvokeAsync(url);
    }

    private async Task ShowWebContentReaderChanged(bool state)
    {
        await this.PreselectChanged.InvokeAsync(state);
    }
    
    private async Task UseContentCleanerAgentChanged(bool state)
    {
        await this.PreselectContentCleanerAgentChanged.InvokeAsync(state);
    }
    
    /// <summary>
    /// Says why the content cleaner has no model, or nothing when it has one.
    /// </summary>
    /// <remarks>
    /// This is a hint, not a validation: the cleaner is an option of the reader, and an option
    /// nobody can use yet must not keep the assistant around it from running. It is also stated
    /// rather than remembered, so that choosing a model below makes it disappear at once.
    /// The two causes lead to different places, which is why they are told apart: either no model
    /// was chosen at all, or the chosen one is not trusted enough for this agent.
    /// </remarks>
    private string? ContentCleanerHint
    {
        get
        {
            if(!this.PreselectContentCleanerAgent || this.providerSettings != AIStudio.Settings.Provider.NONE)
                return null;

            if(this.ProviderSettings == AIStudio.Settings.Provider.NONE)
                return T("The content cleaner uses the model of this assistant. Please select one below.");

            return T("The selected model does not meet the confidence requirements of the content cleaner. Please select another model, or configure an eligible one in the app settings.");
        }
    }

    private string? ValidateURL(string url)
    {
        if(string.IsNullOrWhiteSpace(url))
            return T("Please provide a URL to load the content from.");

        var urlParsingResult = Uri.TryCreate(url, UriKind.Absolute, out var uriResult);
        if(!urlParsingResult)
            return T("Please provide a valid URL.");

        if(uriResult is not { Scheme: "http" or "https" })
            return T("Please provide a valid HTTP or HTTPS URL.");

        return null;
    }
}