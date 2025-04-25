using AIStudio.Dialogs.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.Extensions.FileProviders;

namespace AIStudio.Assistants.I18N;

public partial class AssistantI18N : AssistantBaseCore<SettingsDialogI18N>
{
    public override Tools.Components Component => Tools.Components.I18N_ASSISTANT;
    
    protected override string Title => "Localization";
    
    protected override string Description =>
        """
        Translate MindWork AI Studio text content into another language.
        """;
    
    protected override string SystemPrompt => 
        """
        TODO
        """;
    
    protected override bool AllowProfiles => false;
    
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    
    protected override string SubmitText => "Localize AI Studio";
    
    protected override Func<Task> SubmitAction => this.LocalizeText;

    protected override bool SubmitDisabled => !this.localizationPossible;
    
    protected override void ResetForm()
    {
        if (!this.MightPreselectValues())
        {
            this.selectedLanguagePluginId = InternalPlugin.LANGUAGE_EN_US.MetaData().Id;
            this.selectedTargetLanguage = CommonLanguages.AS_IS;
            this.customTargetLanguage = string.Empty;
        }
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
    private Dictionary<string, string> addedKeys = [];
    private Dictionary<string, string> removedKeys = [];

    #region Overrides of AssistantBase<SettingsDialogI18N>

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await this.LoadData();
    }

    #endregion

    private async Task OnLanguagePluginChanged(Guid pluginId)
    {
        this.selectedLanguagePluginId = pluginId;
        await this.LoadData();
        this.StateHasChanged();
    }
    
    private async Task LoadData()
    {
        this.isLoading = true;
        this.StateHasChanged();
        
        //
        // Read the file `Assistants\I18N\allTexts.lua`:
        //
        
        #if DEBUG
        var filePath = Path.Join(Environment.CurrentDirectory, "Assistants", "I18N");
        var resourceFileProvider = new PhysicalFileProvider(filePath);
        #else
        var resourceFileProvider = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!, "Assistants.I18N");
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
                this.loadingIssue = "Was not able to load the I18N plugin. Please check the plugin code.";
                break;
            
            case { IsValid: false } plugin when plugin.Issues.Any():
                this.loadingIssue = plugin.Issues.First();
                break;
            
            case PluginLanguage pluginLanguage:
                this.loadingIssue = string.Empty;
                var newI18NContent = pluginLanguage.Content;

                if(PluginFactory.RunningPlugins.FirstOrDefault(n => n is PluginLanguage && n.Id == this.selectedLanguagePluginId) is not PluginLanguage comparisonPlugin)
                {
                    this.loadingIssue = $"Was not able to load the language plugin for comparison ({this.selectedLanguagePluginId}). Please select a valid, loaded & running language plugin.";
                    break;
                }
                
                var currentI18NContent = comparisonPlugin.Content;
                this.addedKeys = newI18NContent.ExceptBy(currentI18NContent.Keys, n => n.Key).ToDictionary();
                this.removedKeys = currentI18NContent.ExceptBy(newI18NContent.Keys, n => n.Key).ToDictionary();
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
            return "Please select a target language.";
        
        return null;
    }
    
    private string? ValidateCustomLanguage(string language)
    {
        if(this.selectedTargetLanguage == CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language))
            return "Please provide a custom language.";
        
        return null;
    }
    
    private async Task LocalizeText()
    {
    }
}