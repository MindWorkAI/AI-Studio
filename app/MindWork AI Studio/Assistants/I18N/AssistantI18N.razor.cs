using System.Diagnostics;
using System.Text;

using AIStudio.Dialogs.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PluginSystem;

using Microsoft.Extensions.FileProviders;

using SharedTools;

#if RELEASE
using System.Reflection;
#endif

namespace AIStudio.Assistants.I18N;

public partial class AssistantI18N : AssistantBaseCore<SettingsDialogI18N>
{
    protected override Tools.Components Component => Tools.Components.I18N_ASSISTANT;
    
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
            #if DEBUG
            Text = T("Write Lua code to language plugin file"),
            #else
            Text = T("Copy Lua code to clipboard"),
            #endif
            Icon = Icons.Material.Filled.Extension,
            Color = Color.Default,
            #if DEBUG
            AsyncAction = async () => await this.WriteToPluginFile(),
            #else
            AsyncAction = async () => await this.RustService.CopyText2Clipboard(this.Snackbar, this.finalLuaCode.ToString()),
            #endif
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
    private string? activeSystemPromptLanguage;
    private static readonly AssistantSessionStateKey<CommonLanguages> SELECTED_TARGET_LANGUAGE_STATE_KEY = new(nameof(selectedTargetLanguage));
    private static readonly AssistantSessionStateKey<string> CUSTOM_TARGET_LANGUAGE_STATE_KEY = new(nameof(customTargetLanguage));
    private static readonly AssistantSessionStateKey<bool> IS_LOADING_STATE_KEY = new(nameof(isLoading));
    private static readonly AssistantSessionStateKey<string> LOADING_ISSUE_STATE_KEY = new(nameof(loadingIssue));
    private static readonly AssistantSessionStateKey<bool> LOCALIZATION_POSSIBLE_STATE_KEY = new(nameof(localizationPossible));
    private static readonly AssistantSessionStateKey<string> SEARCH_STRING_STATE_KEY = new(nameof(searchString));
    private static readonly AssistantSessionStateKey<Guid> SELECTED_LANGUAGE_PLUGIN_ID_STATE_KEY = new(nameof(selectedLanguagePluginId));
    private static readonly AssistantSessionStateKey<ILanguagePlugin?> SELECTED_LANGUAGE_PLUGIN_STATE_KEY = new(nameof(selectedLanguagePlugin));
    private static readonly AssistantSessionStateKey<Dictionary<string, string>> ADDED_CONTENT_STATE_KEY = new(nameof(addedContent));
    private static readonly AssistantSessionStateKey<Dictionary<string, string>> REMOVED_CONTENT_STATE_KEY = new(nameof(removedContent));
    private static readonly AssistantSessionStateKey<Dictionary<string, string>> LOCALIZED_CONTENT_STATE_KEY = new(nameof(localizedContent));
    private static readonly AssistantSessionStateKey<string> FINAL_LUA_CODE_STATE_KEY = new(nameof(finalLuaCode));

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(SELECTED_TARGET_LANGUAGE_STATE_KEY, this.selectedTargetLanguage);
        state.Set(CUSTOM_TARGET_LANGUAGE_STATE_KEY, this.customTargetLanguage);
        state.Set(IS_LOADING_STATE_KEY, this.isLoading);
        state.Set(LOADING_ISSUE_STATE_KEY, this.loadingIssue);
        state.Set(LOCALIZATION_POSSIBLE_STATE_KEY, this.localizationPossible);
        state.Set(SEARCH_STRING_STATE_KEY, this.searchString);
        state.Set(SELECTED_LANGUAGE_PLUGIN_ID_STATE_KEY, this.selectedLanguagePluginId);
        state.Set(SELECTED_LANGUAGE_PLUGIN_STATE_KEY, this.selectedLanguagePlugin);
        state.SetDictionary(ADDED_CONTENT_STATE_KEY, this.addedContent);
        state.SetDictionary(REMOVED_CONTENT_STATE_KEY, this.removedContent);
        state.SetDictionary(LOCALIZED_CONTENT_STATE_KEY, this.localizedContent);
        state.SetStringBuilder(FINAL_LUA_CODE_STATE_KEY, this.finalLuaCode);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(SELECTED_TARGET_LANGUAGE_STATE_KEY, value => this.selectedTargetLanguage = value);
        state.Restore(CUSTOM_TARGET_LANGUAGE_STATE_KEY, value => this.customTargetLanguage = value);
        state.Restore(IS_LOADING_STATE_KEY, value => this.isLoading = value);
        state.Restore(LOADING_ISSUE_STATE_KEY, value => this.loadingIssue = value);
        state.Restore(LOCALIZATION_POSSIBLE_STATE_KEY, value => this.localizationPossible = value);
        state.Restore(SEARCH_STRING_STATE_KEY, value => this.searchString = value);
        state.Restore(SELECTED_LANGUAGE_PLUGIN_ID_STATE_KEY, value => this.selectedLanguagePluginId = value);
        state.Restore(SELECTED_LANGUAGE_PLUGIN_STATE_KEY, value => this.selectedLanguagePlugin = value);
        state.RestoreDictionary(ADDED_CONTENT_STATE_KEY, this.addedContent);
        state.RestoreDictionary(REMOVED_CONTENT_STATE_KEY, this.removedContent);
        state.RestoreDictionary(LOCALIZED_CONTENT_STATE_KEY, this.localizedContent);
        state.RestoreStringBuilder(FINAL_LUA_CODE_STATE_KEY, this.finalLuaCode);
    }

    #region Overrides of AssistantBase<SettingsDialogI18N>

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (this.HasAssistantSession)
            return;

        await this.OnLanguagePluginChanged(this.selectedLanguagePluginId);
    }

    #endregion
    
    private string SystemPromptLanguage() => this.activeSystemPromptLanguage ?? (this.selectedTargetLanguage switch
    {
        CommonLanguages.OTHER => this.customTargetLanguage,
        _ => $"{this.selectedTargetLanguage.Name()}",
    });

    private async Task OnLanguagePluginChanged(Guid pluginId)
    {
        if (this.IsProcessing)
            return;

        this.selectedLanguagePluginId = pluginId;
        await this.OnChangedLanguage();
    }

    private async Task OnChangedLanguage()
    {
        if (this.IsProcessing)
            return;

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

    /// <summary>
    /// Gets a stable row snapshot for the added-content table.
    /// </summary>
    private KeyValuePair<string, string>[] AddedContentRows => this.addedContent.ToArray();

    /// <summary>
    /// Gets a stable row snapshot for the removed-content table.
    /// </summary>
    private KeyValuePair<string, string>[] RemovedContentRows => this.removedContent.ToArray();

    /// <summary>
    /// Gets a stable row snapshot for the localized-content table.
    /// </summary>
    private KeyValuePair<string, string>[] LocalizedContentRows => this.localizedContent.ToArray();

    private string AddedContentText => string.Format(T("Added Content ({0} entries)"), this.addedContent.Count);
    
    private string RemovedContentText => string.Format(T("Removed Content ({0} entries)"), this.removedContent.Count);

    private string LocalizedContentText => string.Format(T("Localized Content ({0} entries of {1})"), this.localizedContent.Count, this.NumTotalItems);
    
    private async Task LocalizeTextContent()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;
        
        if(this.selectedLanguagePlugin is null)
            return;
        
        if (this.selectedLanguagePlugin.IETFTag != this.selectedTargetLanguage.ToIETFTag())
            return;
        
        var addedContentSnapshot = this.addedContent.ToArray();
        var removedContentSnapshot = this.removedContent.ToArray();
        var removedContentKeys = removedContentSnapshot.Select(keyValuePair => keyValuePair.Key).ToHashSet(StringComparer.Ordinal);
        var selectedLanguageContentSnapshot = this.selectedLanguagePlugin.Content.ToArray();
        var baseLanguageContentSnapshot = PluginFactory.BaseLanguage.Content.ToArray();

        this.localizedContent.Clear();
        this.activeSystemPromptLanguage = this.SystemPromptLanguage();
        try
        {
            if (this.selectedTargetLanguage is not CommonLanguages.EN_US)
            {
                // Phase 1: Translate added content
                await this.Phase1TranslateAddedContent(addedContentSnapshot);
            }
            else
            {
                // Case: no translation needed
                this.localizedContent = addedContentSnapshot.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value, StringComparer.Ordinal);
            }

            if(this.CancellationTokenSource!.IsCancellationRequested)
                return;
            
            //
            // Now, we have localized the added content. Next, we must merge
            // the localized content with the existing content. However, we
            // must skip the removed content. We use the localizedContent
            // dictionary for the final result:
            //
            foreach (var keyValuePair in selectedLanguageContentSnapshot)
            {
                if (this.CancellationTokenSource!.IsCancellationRequested)
                    break;

                if (this.localizedContent.ContainsKey(keyValuePair.Key))
                    continue;

                if (removedContentKeys.Contains(keyValuePair.Key))
                    continue;

                this.localizedContent.Add(keyValuePair.Key, keyValuePair.Value);
            }

            if(this.CancellationTokenSource!.IsCancellationRequested)
                return;
            
            //
            // Phase 2: Create the Lua code. We want to use the base language
            // for the comments, though:
            //
            var commentContent = addedContentSnapshot.ToDictionary(keyValuePair => keyValuePair.Key, keyValuePair => keyValuePair.Value, StringComparer.Ordinal);
            foreach (var keyValuePair in baseLanguageContentSnapshot)
            {
                if (this.CancellationTokenSource!.IsCancellationRequested)
                    break;

                if (removedContentKeys.Contains(keyValuePair.Key))
                    continue;

                commentContent.TryAdd(keyValuePair.Key, keyValuePair.Value);
            }
            
            this.Phase2CreateLuaCode(commentContent);
        }
        finally
        {
            this.activeSystemPromptLanguage = null;
        }
    }

    /// <summary>
    /// Translates the added text content from a stable snapshot.
    /// </summary>
    /// <param name="addedContentSnapshot">The added text entries captured when the job started.</param>
    /// <returns>A task that completes when all added text entries were translated or cancellation was requested.</returns>
    private async Task Phase1TranslateAddedContent(KeyValuePair<string, string>[] addedContentSnapshot)
    {
        var stopwatch = new Stopwatch();
        var minimumTime = TimeSpan.FromMilliseconds(500);
        foreach (var keyValuePair in addedContentSnapshot)
        {
            if(this.CancellationTokenSource!.IsCancellationRequested)
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
            
            if (this.CancellationTokenSource!.IsCancellationRequested)
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
        LuaTable.Create(ref this.finalLuaCode, "UI_TEXT_CONTENT", this.localizedContent, commentContent, this.CancellationTokenSource!.Token);

        // Next, we must remove the `root::` prefix from the keys:
        this.finalLuaCode.Replace("""UI_TEXT_CONTENT["root::""", """
                                                                 UI_TEXT_CONTENT["
                                                                 """);
    }

    #if DEBUG
    private async Task WriteToPluginFile()
    {
        if (this.selectedLanguagePlugin is null)
        {
            this.Snackbar.Add(T("No language plugin selected."), Severity.Error);
            return;
        }

        if (this.finalLuaCode.Length == 0)
        {
            this.Snackbar.Add(T("No Lua code generated yet."), Severity.Error);
            return;
        }

        try
        {
            // Determine the plugin file path based on the selected language plugin:
            var pluginDirectory = Path.Join(Environment.CurrentDirectory, "Plugins", "languages");
            var pluginId = this.selectedLanguagePluginId.ToString();
            var ietfTag = this.selectedLanguagePlugin.IETFTag.ToLowerInvariant();
            var pluginFolderName = $"{ietfTag}-{pluginId}";
            var pluginFilePath = Path.Join(pluginDirectory, pluginFolderName, "plugin.lua");

            if (!File.Exists(pluginFilePath))
            {
                this.Logger.LogError("Plugin file not found: {PluginFilePath}.", pluginFilePath);
                this.Snackbar.Add(T("Plugin file not found."), Severity.Error);
                return;
            }

            // Read the existing plugin file:
            var existingContent = await File.ReadAllTextAsync(pluginFilePath);

            // Find the position of "UI_TEXT_CONTENT = {}":
            const string MARKER = "UI_TEXT_CONTENT = {}";
            var markerIndex = existingContent.IndexOf(MARKER, StringComparison.Ordinal);

            if (markerIndex == -1)
            {
                this.Logger.LogError("Could not find 'UI_TEXT_CONTENT = {{}}' marker in plugin file: {PluginFilePath}", pluginFilePath);
                this.Snackbar.Add(T("Could not find 'UI_TEXT_CONTENT = {}' marker in plugin file."), Severity.Error);
                return;
            }

            // Keep everything before the marker and replace everything from the marker onwards:
            var metadataSection = existingContent[..markerIndex];
            var newContent = metadataSection + this.finalLuaCode;

            // Write the updated content back to the file:
            await File.WriteAllTextAsync(pluginFilePath, newContent);
            this.Snackbar.Add(T("Successfully updated plugin file."), Severity.Success);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Error writing to plugin file.");
            this.Snackbar.Add(T("Error writing to plugin file."), Severity.Error);
        }
    }
    #endif
}