using AIStudio.AIOperations;
using AIStudio.AIOperations.TextContentCleaning;
using AIStudio.Provider;
using AIStudio.Tools.Imports;
using AIStudio.Tools.Web;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Loads the content of a web page into a text field of an assistant.
/// </summary>
/// <remarks>
/// The loading itself runs in WebContentImportService, so that it goes on while the user is elsewhere: this
/// reader goes away together with its assistant. The reader starts the import, shows its progress, and takes
/// its result, also when it is new and the import ran while the user was away.
/// </remarks>
public partial class ReadWebContent : MSGComponentBase
{
    /// <summary>
    /// The owner of the imports of a reader outside an assistant, which cascades no owner.
    /// </summary>
    private readonly ImportOwner fallbackImportOwner = new(ImportOwnerKind.ASSISTANT, $"read-web-content:{Guid.NewGuid():N}");

    [Inject]
    private WebContentImportService WebContentImportService { get; init; } = null!;

    [Inject]
    private AIOperationProviderResolver ProviderResolver { get; init; } = null!;

    /// <summary>
    /// The assistant around this reader, which owns its imports.
    /// </summary>
    [CascadingParameter]
    private ImportOwner? ImportOwner { get; set; }

    /// <summary>
    /// Tells the readers of one assistant apart, e.g., by the name of the component of an assistant plugin.
    /// </summary>
    /// <remarks>
    /// An assistant with a single reader keeps the default.
    /// </remarks>
    [Parameter]
    public string ImportTargetId { get; set; } = "primary";

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
    public bool CleanerIsRunning { get; set; }
    
    [Parameter]
    public EventCallback<bool> CleanerIsRunningChanged { get; set; }
    
    [Parameter]
    public bool Preselect { get; set; }
    
    [Parameter]
    public EventCallback<bool> PreselectChanged { get; set; }
    
    [Parameter]
    public bool PreselectContentCleaner { get; set; }
    
    [Parameter]
    public EventCallback<bool> PreselectContentCleanerChanged { get; set; }

    private readonly Process<ReadWebContentSteps> process = Process<ReadWebContentSteps>.INSTANCE;

    /// <summary>
    /// The model the content cleaner would run with, and why others were not chosen.
    /// </summary>
    /// <remarks>
    /// This is a resolved value, not a chosen one: the reader has no model selection of its own,
    /// it takes what the assistant around it uses, unless a dedicated one for the cleaner takes
    /// precedence, or the app-wide default fills in. Because the assistant's model can change at any
    /// moment, this is resolved again on every render instead of being remembered from the first one.
    /// </remarks>
    private AIOperationProviderChoice cleanerChoice = new(AIStudio.Settings.Provider.NONE, AIOperationProviderIssue.NOT_SET, AIOperationProviderIssue.NOT_SET);

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.WebContentImportService.StateChanged += this.OnImportStateChanged;
        this.ApplyFilters([], [ Event.CONFIGURATION_CHANGED ]);
        this.ResolveProvider();

        await base.OnInitializedAsync();
        await this.SyncWithImportAsync();
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

    /// <summary>
    /// Stops following the imports. An import keeps running, and its result waits for the next reader of this field.
    /// </summary>
    protected override void DisposeResources()
    {
        this.WebContentImportService.StateChanged -= this.OnImportStateChanged;
        base.DisposeResources();
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
    private void ResolveProvider() => this.cleanerChoice = this.ProviderResolver.Preview(TextContentCleaningOperation.GetProviderSettings(this.SettingsManager), ConfidenceLevel.NONE, WebContentImportService.GetCleanerProviderOptions(this.SettingsManager, this.ProviderSettings));

    private ImportOwner EffectiveImportOwner => this.ImportOwner ?? this.fallbackImportOwner;

    private ImportTarget EffectiveImportTarget => new(this.EffectiveImportOwner, this.ImportTargetId);

    /// <summary>
    /// The import which fills this field, while it runs.
    /// </summary>
    private WebContentImportSnapshot? CurrentImport => this.WebContentImportService.GetSnapshot(this.EffectiveImportTarget);

    /// <summary>
    /// Whether the content of this field is being loaded. The controls of the reader wait for the whole import,
    /// from loading the page to cleaning it.
    /// </summary>
    private bool IsImporting => this.CurrentImport is not null;

    private ProcessStepValue ProcessStep => this.process[this.CurrentImport?.Step ?? ReadWebContentSteps.START];

    private void LoadFromWeb()
    {
        if(!this.IsReady || this.IsImporting)
            return;

        this.WebContentImportService.TryStart(this.EffectiveImportTarget, new(this.URL, this.PreselectContentCleaner, this.ProviderSettings));
    }

    private void OnImportStateChanged(ImportOwner owner)
    {
        if (owner == this.EffectiveImportOwner)
            this.InvokeAsync(async () =>
            {
                await this.SyncWithImportAsync();
                this.StateHasChanged();
            }).Observe($"{nameof(ReadWebContent)}: following the web content import");
    }

    /// <summary>
    /// Takes over what the import of this field reached: its options while it runs, its result once it ended, and
    /// whether it cleans.
    /// </summary>
    /// <remarks>
    /// This runs when the reader starts, too: the user may have left the assistant while the import ran. The
    /// assistant restores its draft in OnInitializedAsync, before its first render creates this reader, so the
    /// result lands on the draft and not under it, as the transcripts of ReadFileContent do. The assistant hears
    /// about the cleaning only, as before, e.g., to lock its input field while the cleaner prepares the text
    /// which replaces it.
    /// </remarks>
    private async Task SyncWithImportAsync()
    {
        var result = this.WebContentImportService.TryTakeResult(this.EffectiveImportTarget);
        var currentImport = this.CurrentImport;
        if (currentImport is not null)
            await this.ShowRunningImportAsync(currentImport);

        var isCleaning = currentImport?.Step is ReadWebContentSteps.CLEANING;
        if (isCleaning != this.CleanerIsRunning)
        {
            this.CleanerIsRunning = isCleaning;
            await this.CleanerIsRunningChanged.InvokeAsync(this.CleanerIsRunning);
        }

        if (result is not null)
            await this.ApplyResultAsync(result);
    }

    /// <summary>
    /// Shows the options with which the import of this field runs: the reader expanded, the address it loads,
    /// and whether it cleans the page.
    /// </summary>
    /// <remarks>
    /// An assistant which starts anew when the user comes back, see AssistantInputsWhenLeaving.DISCARD_INPUTS,
    /// holds its preselected values instead. The reader would then stay collapsed, with its switch locked and
    /// the progress of the import hidden. The options belong to the assistant, so they go there as well.
    /// </remarks>
    private async Task ShowRunningImportAsync(WebContentImportSnapshot import)
    {
        if (!this.Preselect)
        {
            this.Preselect = true;
            await this.PreselectChanged.InvokeAsync(this.Preselect);
        }

        if (this.URL != import.Url)
        {
            this.URL = import.Url;
            await this.URLChanged.InvokeAsync(this.URL);
        }

        if (this.PreselectContentCleaner != import.CleanContent)
        {
            this.PreselectContentCleaner = import.CleanContent;
            await this.PreselectContentCleanerChanged.InvokeAsync(this.PreselectContentCleaner);
        }
    }

    private async Task ApplyResultAsync(WebContentImportResult result)
    {
        switch (result.Issue)
        {
            //
            // Say that the cleaning did not happen. The user asked for it, the page arrives,
            // and without a word they would take the raw markdown -- navigation, cookie banner
            // and advertising included -- for the cleaned result.
            //
            case WebContentImportIssue.NOT_CLEANED_NO_MODEL:
                await this.MessageBus.SendError(new(AppIcons.AI_OPERATION, T("The content was loaded, but not cleaned: no model is available for the content cleaner.")));
                break;

            case WebContentImportIssue.NOT_CLEANED_NO_ANSWER:
                await this.MessageBus.SendWarning(new(AppIcons.AI_OPERATION, T("The content was loaded, but not cleaned: the model of the content cleaner gave no answer.")));
                break;

            //
            // Say why nothing was loaded. An empty text field looks like a page without content,
            // and the reasons a page cannot be read are things the user can act on: a link to a
            // PDF rather than a page, a host that does not answer, a server refusing the request.
            //
            case WebContentImportIssue.LOAD_FAILED:
                await this.MessageBus.SendError(new(AppIcons.PROVIDER_ERROR, string.Format(T("The content of '{0}' could not be loaded: {1}"), result.Url, result.ErrorMessage)));
                break;
        }

        this.Content = result.Markdown;
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

    private async Task URLValueChanged(string? url)
    {
        await this.URLChanged.InvokeAsync(url ?? string.Empty);
    }

    private async Task ShowWebContentReaderChanged(bool state)
    {
        await this.PreselectChanged.InvokeAsync(state);
    }
    
    private async Task UseContentCleanerChanged(bool state)
    {
        await this.PreselectContentCleanerChanged.InvokeAsync(state);
    }
    
    /// <summary>
    /// Says why the content cleaner has no model, or nothing when it has one.
    /// </summary>
    /// <remarks>
    /// This is a hint, not a validation: the cleaner is an option of the reader, and an option
    /// nobody can use yet must not keep the assistant around it from running. It is also stated
    /// rather than remembered, so that choosing a model below makes it disappear at once.
    /// The causes lead to different places, which is why they are told apart: no model was chosen
    /// at all, the organization does not allow the chosen one, or it is not trusted enough for the
    /// cleaner. The reason comes from the choice itself, where the first named fallback is the model
    /// of the assistant, or the app-wide default when the assistant has none.
    /// </remarks>
    private string? ContentCleanerHint
    {
        get
        {
            if(!this.PreselectContentCleaner || this.cleanerChoice.HasProvider)
                return null;

            return this.cleanerChoice.FallbackIssue switch
            {
                AIOperationProviderIssue.NOT_SET => TA("The content cleaner uses the model of this assistant. Please select one below."),
                AIOperationProviderIssue.PROHIBITED => TA("Your organization does not allow the selected model. Please select another model, or configure an eligible one in the app settings."),
                _ => TA("The selected model does not meet the confidence requirements of the content cleaner. Please select another model, or configure an eligible one in the app settings."),
            };
        }
    }

    private string? ValidateURL(string url)
    {
        if(string.IsNullOrWhiteSpace(url))
            return TA("Please provide a URL to load the content from.");

        var urlParsingResult = Uri.TryCreate(url, UriKind.Absolute, out var uriResult);
        if(!urlParsingResult)
            return TA("Please provide a valid URL.");

        if(uriResult is not { Scheme: "http" or "https" })
            return TA("Please provide a valid HTTP or HTTPS URL.");

        return null;
    }
}