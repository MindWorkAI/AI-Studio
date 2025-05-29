using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.IconFinder;

public partial class AssistantIconFinder : AssistantBaseCore<SettingsDialogIconFinder>
{
    public override Tools.Components Component => Tools.Components.ICON_FINDER_ASSISTANT;

    protected override string Title => T("Icon Finder");
    
    protected override string Description => T("""Finding the right icon for a context, such as for a piece of text, is not easy. The first challenge: You need to extract a concept from your context, such as from a text. Let's take an example where your text contains statements about multiple departments. The sought-after concept could be "departments." The next challenge is that we need to anticipate the bias of the icon designers: under the search term "departments," there may be no relevant icons or only unsuitable ones. Depending on the icon source, it might be more effective to search for "buildings," for instance. LLMs assist you with both steps.""");
    
    protected override string SystemPrompt => 
        """
        I can search for icons using US English keywords. Please help me come up with the right search queries.
        I don't want you to translate my requests word-for-word into US English. Instead, you should provide keywords
        that are likely to yield suitable icons. For example, I might ask for an icon about departments, but icons
        related to the keyword "buildings" might be the best match. Provide your keywords in a Markdown list without
        quotation marks.
        """;
    
    protected override bool AllowProfiles => false;

    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => T("Find Icon");

    protected override Func<Task> SubmitAction => this.FindIcon;

    protected override void ResetForm()
    {
        this.inputContext = string.Empty;
        if (!this.MightPreselectValues())
        {
            this.selectedIconSource = IconSources.GENERIC;
        }
    }

    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.IconFinder.PreselectOptions)
        {
            this.selectedIconSource = this.SettingsManager.ConfigurationData.IconFinder.PreselectedSource;
            return true;
        }
        
        return false;
    }
    
    private string inputContext = string.Empty;
    private IconSources selectedIconSource;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        var deferredContent = MessageBus.INSTANCE.CheckDeferredMessages<string>(Event.SEND_TO_ICON_FINDER_ASSISTANT).FirstOrDefault();
        if (deferredContent is not null)
            this.inputContext = deferredContent;
        
        await base.OnInitializedAsync();
    }

    #endregion

    private string? ValidatingContext(string context)
    {
        if(string.IsNullOrWhiteSpace(context))
            return T("Please provide a context. This will help the AI to find the right icon. You might type just a keyword or copy a sentence from your text, e.g., from a slide where you want to use the icon.");
        
        return null;
    }

    private async Task FindIcon()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        this.CreateChatThread();
        var time = this.AddUserRequest(
        $"""
            {this.selectedIconSource.Prompt()} I search for an icon for the following context:
            
            ```
            {this.inputContext}
            ```
         """);

        await this.AddAIResponseAsync(time);
    }
}