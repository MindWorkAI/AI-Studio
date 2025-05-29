using System.Diagnostics;
using System.Text;

using AIStudio.Dialogs.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.Extensions.FileProviders;

using SharedTools;

#if RELEASE
using System.Reflection;
#endif

namespace AIStudio.Assistants.I18N;

public partial class AssistantI18N : AssistantBaseCore<SettingsDialogI18N>
{
    public override Tools.Components Component => Tools.Components.I18N_ASSISTANT;
    
    protected override string Title => T("Localization");
    
    protected override string Description => T("Translate MindWork AI Studio text content into another language.");
    
    protected override string SystemPrompt => 
        $"""
        # Assignment
        You are an expert in professional translations from English (US) to {this.SystemPromptLanguage()}.
        You translate the texts without adding any new information. When necessary, you correct 
        spelling and grammar.
        
        # Context
        The texts to be translated come from the open source app "MindWork AI Studio". The goal
        is to localize the app so that it can be offered in other languages. You will always
        receive one text at a time. A text may be, for example, for a button, a label, or an
        explanation within the app. The app "AI Studio" is a desktop app for macOS, Linux,
        and Windows. Users can use Large Language Models (LLMs) in practical ways in their
        daily lives with it. The app offers the regular chat mode for which LLMs have become
        known. However, AI Studio also offers so-called assistants, where users no longer
        have to prompt.
        
        # Target Audience
        The app is intended for everyone, not just IT specialists or scientists. When translating,
        make sure the texts are easy for everyone to understand.
        """;
    
    protected override bool AllowProfiles => false;

    protected override bool ShowResult => false;
    
    protected override bool ShowCopyResult => false;

    protected override bool ShowSendTo => false;
    
    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new ButtonData
        {
            Text = T("Copy Lua code to clipboard"),
            Icon = Icons.Material.Filled.Extension,
            Color = Color.Default,
            AsyncAction = async () => await this.RustService.CopyText2Clipboard(this.Snackbar, this.finalLuaCode.ToString()),
            DisabledActionParam = () => this.finalLuaCode.Length == 0,
        },
    ];
    
    protected override string SubmitText => T("Localize AI Studio & generate the Lua code");
    
    protected override Func<Task> SubmitAction => this.LocalizeTextContent;

    protected override bool SubmitDisabled => !this.localizationPossible;
    
    protected override bool ShowDedicatedProgress => true;
    
    protected override void ResetForm()
    {
        if (!this.MightPreselectValues())
        {
            this.selectedLanguagePluginId = InternalPlugin.LANGUAGE_EN_US.MetaData().Id;
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }

        _ = this.OnChangedLanguage();
    }
    
    protected override bool MightPreselectValues()
    {
        if (this.SettingsManager.ConfigurationData.I18N.PreselectOptions)
        {
            this.selectedLanguagePluginId = this.SettingsManager.ConfigurationData.I18N.PreselectedLanguagePluginId;
            this.selectedTargetLanguage = this.SettingsManager.ConfigurationData.I18N.PreselectedTargetLanguage;
            this.customTargetLanguage = this.SettingsManager.ConfigurationData.I18N.PreselectOtherLanguage;
            return true;
        }
        
        return false;
    }
    
    private CommonLanguages selectedTargetLanguage;
    private string customTargetLanguage = string.Empty;
    private bool isLoading = true;
    private string loadingIssue = string.Empty;
    private bool localizationPossible;
    private string searchString = string.Empty;
    private Guid selectedLanguagePluginId;
    private ILanguagePlugin? selectedLanguagePlugin;
    private Dictionary<string, string> addedContent = [];
    private Dictionary<string, string> removedContent = [];
    private Dictionary<string, string> localizedContent = [];
    private StringBuilder finalLuaCode = new();

    #region Overrides of AssistantBase<SettingsDialogI18N>

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await this.OnLanguagePluginChanged(this.selectedLanguagePluginId);
        await this.LoadData();
    }

    #endregion
    
    private string SystemPromptLanguage() => this.selectedTargetLanguage switch
    {
        CommonLanguages.OTHER => this.customTargetLanguage,
        _ => $"{this.selectedTargetLanguage.Name()}",
    };

    private async Task OnLanguagePluginChanged(Guid pluginId)
    {
        this.selectedLanguagePluginId = pluginId;
        await this.OnChangedLanguage();
    }

    private async Task OnChangedLanguage()
    {
        this.finalLuaCode.Clear();
        this.localizedContent.Clear();
        this.localizationPossible = false;
        if (PluginFactory.RunningPlugins.FirstOrDefault(n => n is PluginLanguage && n.Id == this.selectedLanguagePluginId) is not PluginLanguage comparisonPlugin)
        {
            this.loadingIssue = string.Format(T("Was not able to load the language plugin for comparison ({0}). Please select a valid, loaded & running language plugin."), this.selectedLanguagePluginId);
            this.selectedLanguagePlugin = null;
        }
        else if (comparisonPlugin.IETFTag != this.selectedTargetLanguage.ToIETFTag())
        {
            this.loadingIssue = string.Format(T("The selected language plugin for comparison uses the IETF tag '{0}' which does not match the selected target language '{1}'. Please select a valid, loaded & running language plugin which matches the target language."), comparisonPlugin.IETFTag, this.selectedTargetLanguage.ToIETFTag());
            this.selectedLanguagePlugin = null;
        }
        else
        {
            this.selectedLanguagePlugin = comparisonPlugin;
            this.loadingIssue = string.Empty;
            await this.LoadData();
        }
        
        this.StateHasChanged();
    }
    
    private async Task LoadData()
    {
        if (this.selectedLanguagePlugin is null)
        {
            this.loadingIssue = T("Please select a language plugin for comparison.");
            this.localizationPossible = false;
            this.isLoading = false;
            this.StateHasChanged();
            return;
        }
        
        this.isLoading = true;
        this.StateHasChanged();
        
        //
        // Read the file `Assistants\I18N\allTexts.lua`:
        //
        #if DEBUG
        var filePath = Path.Join(Environment.CurrentDirectory, "Assistants", "I18N");
        var resourceFileProvider = new PhysicalFileProvider(filePath);
        #else
        var resourceFileProvider = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!, "Assistants/I18N");
        #endif

        var file = resourceFileProvider.GetFileInfo("allTexts.lua");
        await using var fileStream = file.CreateReadStream();
        using var reader = new StreamReader(fileStream);
        var newI18NDataLuaCode = await reader.ReadToEndAsync();
        
        //
        // Next, we try to load the text as a language plugin -- without
        // actually starting the plugin:
        //
        var newI18NPlugin = await PluginFactory.Load(null, newI18NDataLuaCode);
        switch (newI18NPlugin)
        {
            case NoPlugin noPlugin when noPlugin.Issues.Any():
                this.loadingIssue = noPlugin.Issues.First();
                break;
            
            case NoPlugin:
                this.loadingIssue = T("Was not able to load the I18N plugin. Please check the plugin code.");
                break;
            
            case { IsValid: false } plugin when plugin.Issues.Any():
                this.loadingIssue = plugin.Issues.First();
                break;
            
            case PluginLanguage pluginLanguage:
                this.loadingIssue = string.Empty;
                var newI18NContent = pluginLanguage.Content;
                
                var currentI18NContent = this.selectedLanguagePlugin.Content;
                this.addedContent = newI18NContent.ExceptBy(currentI18NContent.Keys, n => n.Key).ToDictionary();
                this.removedContent = currentI18NContent.ExceptBy(newI18NContent.Keys, n => n.Key).ToDictionary();
                this.localizationPossible = true;
                break;
        }
        
        this.isLoading = false;
        this.StateHasChanged();
    }
    
    private bool FilterFunc(KeyValuePair<string, string> element)
    {
        if (string.IsNullOrWhiteSpace(this.searchString))
            return true;
        
        if (element.Key.Contains(this.searchString, StringComparison.OrdinalIgnoreCase))
            return true;
        
        if (element.Value.Contains(this.searchString, StringComparison.OrdinalIgnoreCase))
            return true;
        
        return false;
    }

    private string? ValidatingTargetLanguage(CommonLanguages language)
    {
        if(language == CommonLanguages.AS_IS)
            return T("Please select a target language.");
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return T("Please provide a custom language.");
        
        return null;
    }

    private int NumTotalItems => (this.selectedLanguagePlugin?.Content.Count ?? 0) + this.addedContent.Count - this.removedContent.Count;

    private string AddedContentText => string.Format(T("Added Content ({0} entries)"), this.addedContent.Count);
    
    private string RemovedContentText => string.Format(T("Removed Content ({0} entries)"), this.removedContent.Count);

    private string LocalizedContentText => string.Format(T("Localized Content ({0} entries of {1})"), this.localizedContent.Count, this.NumTotalItems);
    
    private async Task LocalizeTextContent()
    {
        await this.form!.Validate();
        if (!this.inputIsValid)
            return;
        
        if(this.selectedLanguagePlugin is null)
            return;
        
        if (this.selectedLanguagePlugin.IETFTag != this.selectedTargetLanguage.ToIETFTag())
            return;
        
        this.localizedContent.Clear();
        if (this.selectedTargetLanguage is not CommonLanguages.EN_US)
        {
            // Phase 1: Translate added content
            await this.Phase1TranslateAddedContent();
        }
        else
        {
            // Case: no translation needed
            this.localizedContent = this.addedContent.ToDictionary();
        }

        if(this.cancellationTokenSource!.IsCancellationRequested)
            return;
        
        //
        // Now, we have localized the added content. Next, we must merge
        // the localized content with the existing content. However, we
        // must skip the removed content. We use the localizedContent
        // dictionary for the final result:
        //
        foreach (var keyValuePair in this.selectedLanguagePlugin.Content)
        {
            if (this.cancellationTokenSource!.IsCancellationRequested)
                break;
            
            if (this.localizedContent.ContainsKey(keyValuePair.Key))
                continue;
            
            if (this.removedContent.ContainsKey(keyValuePair.Key))
                continue;
            
            this.localizedContent.Add(keyValuePair.Key, keyValuePair.Value);
        }
        
        if(this.cancellationTokenSource!.IsCancellationRequested)
            return;
        
        //
        // Phase 2: Create the Lua code. We want to use the base language
        // for the comments, though:
        //
        var commentContent = new Dictionary<string, string>(this.addedContent);
        foreach (var keyValuePair in PluginFactory.BaseLanguage.Content)
        {
            if  (this.cancellationTokenSource!.IsCancellationRequested)  
                break;
            
            if (this.removedContent.ContainsKey(keyValuePair.Key))
                continue;
            
            commentContent.TryAdd(keyValuePair.Key, keyValuePair.Value);
        }
        
        this.Phase2CreateLuaCode(commentContent);
    }

    private async Task Phase1TranslateAddedContent()
    {
        var stopwatch = new Stopwatch();
        var minimumTime = TimeSpan.FromMilliseconds(500);
        foreach (var keyValuePair in this.addedContent)
        {
            if(this.cancellationTokenSource!.IsCancellationRequested)
                break;
            
            //
            // We measure the time for each translation.
            // We do not want to make more than 120 requests
            // per minute, i.e., 2 requests per second.
            //
            stopwatch.Reset();
            stopwatch.Start();
            
            //
            // Translate one text at a time:
            //
            this.CreateChatThread();
            var time = this.AddUserRequest(keyValuePair.Value);
            this.localizedContent.Add(keyValuePair.Key, await this.AddAIResponseAsync(time));
            
            if (this.cancellationTokenSource!.IsCancellationRequested)
                break;
            
            //
            // Ensure that we do not exceed the rate limit of 2 requests per second:
            //
            stopwatch.Stop();
            if (stopwatch.Elapsed < minimumTime)
                await Task.Delay(minimumTime - stopwatch.Elapsed);
        }
    }

    private void Phase2CreateLuaCode(IReadOnlyDictionary<string, string> commentContent)
    {
        this.finalLuaCode.Clear();
        LuaTable.Create(ref this.finalLuaCode, "UI_TEXT_CONTENT", this.localizedContent, commentContent, this.cancellationTokenSource!.Token);
        
        // Next, we must remove the `root::` prefix from the keys:
        this.finalLuaCode.Replace("""UI_TEXT_CONTENT["root::""", """
                                                                 UI_TEXT_CONTENT["
                                                                 """);
    }
}