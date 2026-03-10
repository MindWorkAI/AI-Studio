using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using AIStudio.Dialogs.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Lua;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace AIStudio.Assistants.Dynamic;

public partial class AssistantDynamic : AssistantBaseCore<SettingsDialogDynamic>
{
    [Parameter] 
    public AssistantForm? RootComponent { get; set; } = null!;
    
    protected override string Title => this.title;
    protected override string Description => this.description;
    protected override string SystemPrompt => this.systemPrompt;
    protected override bool AllowProfiles => this.allowProfiles;
    protected override bool ShowProfileSelection => this.showFooterProfileSelection;
    protected override string SubmitText => this.submitText;
    protected override Func<Task> SubmitAction => this.Submit;
    // Dynamic assistants do not have dedicated settings yet.
    // Reuse chat-level provider filtering/preselection instead of NONE.
    protected override Tools.Components Component => Tools.Components.CHAT;

    private string? inputText;
    private string title = string.Empty;
    private string description = string.Empty;
    private string systemPrompt = string.Empty;
    private bool allowProfiles = true;
    private string submitText = string.Empty;
    private string selectedTargetLanguage = string.Empty;
    private string customTargetLanguage = string.Empty;
    private bool showFooterProfileSelection = true;
    private PluginAssistants? assistantPlugin;
    
    private readonly Dictionary<string, string> inputFields = new();
    private readonly Dictionary<string, string> dropdownFields = new();
    private readonly Dictionary<string, bool> switchFields = new();
    private readonly Dictionary<string, WebContentState> webContentFields = new();
    private readonly Dictionary<string, FileContentState> fileContentFields = new();
    private readonly Dictionary<string, string> colorPickerFields = new();
    private readonly Dictionary<string, string> imageCache = new();
    private readonly HashSet<string> executingButtonActions = [];
    private string pluginPath = string.Empty;
    private const string PLUGIN_SCHEME = "plugin://";
    private const string ASSISTANT_QUERY_KEY = "assistantId";
    
    protected override void OnInitialized()
    {
        var assistantPlugin = this.ResolveAssistantPlugin();
        if (assistantPlugin is null)
        {
            this.Logger.LogWarning("AssistantDynamic could not resolve a registered assistant plugin.");
            base.OnInitialized();
            return;
        }

        this.assistantPlugin = assistantPlugin;
        this.RootComponent = assistantPlugin.RootComponent;
        this.title = assistantPlugin.AssistantTitle;
        this.description = assistantPlugin.AssistantDescription;
        this.systemPrompt = assistantPlugin.SystemPrompt;
        this.submitText = assistantPlugin.SubmitText;
        this.allowProfiles = assistantPlugin.AllowProfiles;
        this.showFooterProfileSelection = !assistantPlugin.HasEmbeddedProfileSelection;
        this.pluginPath = assistantPlugin.PluginPath;

        var rootComponent = this.RootComponent;
        if (rootComponent is not null)
        {
            foreach (var component in rootComponent.Children)
            {
                switch (component.Type)
                {
                    case AssistantComponentType.TEXT_AREA:
                        if (component is AssistantTextArea textArea)
                        {
                            this.inputFields.Add(textArea.Name, textArea.PrefillText);
                        }
                        break;
                    case AssistantComponentType.DROPDOWN:
                        if (component is AssistantDropdown dropdown)
                        {
                            this.dropdownFields.Add(dropdown.Name, dropdown.Default.Value);
                        }
                        break;
                    case AssistantComponentType.SWITCH:
                        if (component is AssistantSwitch switchComponent)
                        {
                            this.switchFields.Add(switchComponent.Name, switchComponent.Value);
                        }
                        break;
                    case AssistantComponentType.WEB_CONTENT_READER:
                        if (component is AssistantWebContentReader webContent)
                        {
                            this.webContentFields.Add(webContent.Name, new WebContentState
                            {
                                Preselect = webContent.Preselect,
                                PreselectContentCleanerAgent = webContent.PreselectContentCleanerAgent,
                            });
                        }
                        break;
                    case AssistantComponentType.FILE_CONTENT_READER:
                        if (component is AssistantFileContentReader fileContent)
                        {
                            this.fileContentFields.Add(fileContent.Name, new FileContentState());
                        }
                        break;
                    case AssistantComponentType.COLOR_PICKER:
                        if (component is AssistantColorPicker assistantColorPicker)
                        {
                            this.colorPickerFields.Add(assistantColorPicker.Name, assistantColorPicker.Placeholder);
                        }
                        break;
                }
            }
        }

        base.OnInitialized();
    }

    private PluginAssistants? ResolveAssistantPlugin()
    {
        var assistantPlugins = PluginFactory.RunningPlugins.OfType<PluginAssistants>().ToList();
        if (assistantPlugins.Count == 0)
            return null;

        var requestedPluginId = this.TryGetAssistantIdFromQuery();
        if (requestedPluginId is not { } id) return assistantPlugins.First();
        
        var requestedPlugin = assistantPlugins.FirstOrDefault(p => p.Id == id);
        return requestedPlugin ?? assistantPlugins.First();
    }

    private Guid? TryGetAssistantIdFromQuery()
    {
        var uri = this.NavigationManager.ToAbsoluteUri(this.NavigationManager.Uri);
        if (string.IsNullOrWhiteSpace(uri.Query))
            return null;

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue(ASSISTANT_QUERY_KEY, out var values))
            return null;

        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Guid.TryParse(value, out var assistantId))
            return assistantId;

        this.Logger.LogWarning("AssistantDynamic query parameter '{Parameter}' is not a valid GUID.", value);
        return null;
    }

    protected override void ResetForm()
    {
        foreach (var entry in this.inputFields)
        {
            this.inputFields[entry.Key] = string.Empty;
        }
        foreach (var entry in this.webContentFields)
        {
            entry.Value.Content = string.Empty;
            entry.Value.AgentIsRunning = false;
        }
        foreach (var entry in this.fileContentFields)
        {
            entry.Value.Content = string.Empty;
        }
        foreach (var entry in this.colorPickerFields)
        {
            this.colorPickerFields[entry.Key] = string.Empty;
        }
    }

    protected override bool MightPreselectValues()
    {
        // Dynamic assistants have arbitrary fields supplied via plugins, so there
        // isn't a built-in settings section to prefill values. Always return
        // false to keep the plugin-specified defaults.
        return false;
    }

    private string ResolveImageSource(AssistantImage image)
    {
        if (string.IsNullOrWhiteSpace(image.Src))
            return string.Empty;

        if (this.imageCache.TryGetValue(image.Src, out var cached) && !string.IsNullOrWhiteSpace(cached))
            return cached;

        var resolved = image.Src;

        if (resolved.StartsWith(PLUGIN_SCHEME, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(this.pluginPath))
        {
            var relative = resolved[PLUGIN_SCHEME.Length..].TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var filePath = Path.Join(this.pluginPath, relative);
            if (File.Exists(filePath))
            {
                var mime = GetImageMimeType(filePath);
                var data = Convert.ToBase64String(File.ReadAllBytes(filePath));
                resolved = $"data:{mime};base64,{data}";
            }
            else
            {
                resolved = string.Empty;
            }
        }
        else if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme is not ("http" or "https" or "data"))
                resolved = string.Empty;
        }
        else
        {
            resolved = string.Empty;
        }

        this.imageCache[image.Src] = resolved;
        return resolved;
    }

    private static string GetImageMimeType(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "svg" => "image/svg+xml",
            "png" => "image/png",
            "jpg" => "image/jpeg",
            "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "webp" => "image/webp",
            "bmp" => "image/bmp",
            _ => "image/png",
        };
    }

    private async Task<string> CollectUserPromptAsync()
    {
        if (this.assistantPlugin?.HasCustomPromptBuilder != true) return this.CollectUserPromptFallback();
        
        var input = this.BuildPromptInput();
        var prompt = await this.assistantPlugin.TryBuildPromptAsync(input, this.cancellationTokenSource?.Token ?? CancellationToken.None);
        return !string.IsNullOrWhiteSpace(prompt) ? prompt : this.CollectUserPromptFallback();
    }

    private LuaTable BuildPromptInput()
    {
        var input = new LuaTable();

        var fields = new LuaTable();
        foreach (var entry in this.inputFields)
            fields[entry.Key] = entry.Value ?? string.Empty;
        foreach (var entry in this.dropdownFields)
            fields[entry.Key] = entry.Value ?? string.Empty;
        foreach (var entry in this.switchFields)
            fields[entry.Key] = entry.Value;
        foreach (var entry in this.webContentFields)
            fields[entry.Key] = entry.Value.Content ?? string.Empty;
        foreach (var entry in this.fileContentFields)
            fields[entry.Key] = entry.Value.Content ?? string.Empty;
        foreach (var entry in this.colorPickerFields)
            fields[entry.Key] = entry.Value ?? string.Empty;

        input["fields"] = fields;

        var meta = new LuaTable();
        var rootComponent = this.RootComponent;
        if (rootComponent is not null)
        {
            foreach (var component in rootComponent.Children)
            {
                switch (component)
                {
                    case AssistantTextArea textArea:
                        this.AddMetaEntry(meta, textArea.Name, component.Type, textArea.Label, textArea.UserPrompt);
                        break;
                    case AssistantDropdown dropdown:
                        this.AddMetaEntry(meta, dropdown.Name, component.Type, dropdown.Label, dropdown.UserPrompt);
                        break;
                    case AssistantSwitch switchComponent:
                        this.AddMetaEntry(meta, switchComponent.Name, component.Type, switchComponent.Label, switchComponent.UserPrompt);
                        break;
                    case AssistantWebContentReader webContent:
                        this.AddMetaEntry(meta, webContent.Name, component.Type, null, webContent.UserPrompt);
                        break;
                    case AssistantFileContentReader fileContent:
                        this.AddMetaEntry(meta, fileContent.Name, component.Type, null, fileContent.UserPrompt);
                        break;
                    case AssistantColorPicker colorPicker:
                        this.AddMetaEntry(meta, colorPicker.Name, component.Type, colorPicker.Label, colorPicker.UserPrompt);
                        break;
                }
            }
        }

        input["meta"] = meta;

        var profile = new LuaTable
        {
            ["Name"] = this.currentProfile.Name,
            ["NeedToKnow"] = this.currentProfile.NeedToKnow,
            ["Actions"] = this.currentProfile.Actions,
            ["Num"] = this.currentProfile.Num,
        };
        input["profile"] = profile;

        return input;
    }

    private void AddMetaEntry(LuaTable meta, string name, AssistantComponentType type, string? label, string? userPrompt)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var entry = new LuaTable
        {
            ["Type"] = type.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(label))
            entry["Label"] = label!;
        if (!string.IsNullOrWhiteSpace(userPrompt))
            entry["UserPrompt"] = userPrompt!;

        meta[name] = entry;
    }

    private string CollectUserPromptFallback()
    {
        var prompt = string.Empty;
        var rootComponent = this.RootComponent;
        if (rootComponent is null)
            return prompt;

        foreach (var component in rootComponent.Children)
        {
            var userInput = string.Empty;
            var userDecision = false;
            switch (component.Type)
            {
                case AssistantComponentType.TEXT_AREA:
                    if (component is AssistantTextArea textArea)
                    {
                        prompt += $"context:{Environment.NewLine}{textArea.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.inputFields.TryGetValue(textArea.Name, out userInput))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                        }
                    }
                    break;
                case AssistantComponentType.DROPDOWN:
                    if (component is AssistantDropdown dropdown)
                    {
                        prompt += $"{Environment.NewLine}context:{Environment.NewLine}{dropdown.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.dropdownFields.TryGetValue(dropdown.Name, out userInput))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                        }
                    }
                    break;
                case AssistantComponentType.SWITCH:
                    if (component is AssistantSwitch switchComponent)
                    {
                        prompt += $"{Environment.NewLine}context:{Environment.NewLine}{switchComponent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.switchFields.TryGetValue(switchComponent.Name, out userDecision))
                        {
                            prompt += $"user decision:{Environment.NewLine}{userDecision}";
                        }
                    }
                    break;
                case AssistantComponentType.WEB_CONTENT_READER:
                    if (component is AssistantWebContentReader webContent &&
                        this.webContentFields.TryGetValue(webContent.Name, out var webState))
                    {
                        if (!string.IsNullOrWhiteSpace(webContent.UserPrompt))
                        {
                            prompt += $"{Environment.NewLine}context:{Environment.NewLine}{webContent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        }

                        if (!string.IsNullOrWhiteSpace(webState.Content))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{webState.Content}";
                        }
                    }
                    break;
                case AssistantComponentType.FILE_CONTENT_READER:
                    if (component is AssistantFileContentReader fileContent &&
                        this.fileContentFields.TryGetValue(fileContent.Name, out var fileState))
                    {
                        if (!string.IsNullOrWhiteSpace(fileContent.UserPrompt))
                        {
                            prompt += $"{Environment.NewLine}context:{Environment.NewLine}{fileContent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        }

                        if (!string.IsNullOrWhiteSpace(fileState.Content))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{fileState.Content}";
                        }
                    }
                    break;
                case AssistantComponentType.COLOR_PICKER:
                    if (component is AssistantColorPicker colorPicker)
                    {
                        prompt += $"context:{Environment.NewLine}{colorPicker.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.inputFields.TryGetValue(colorPicker.Name, out userInput))
                        {
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                        }
                    }
                    break;
                default:
                    prompt += $"{userInput}{Environment.NewLine}";
                    break;
            }
        }

        return prompt;
    }

    private static string MergeClass(string customClass, string fallback)
    {
        var trimmedCustom = customClass?.Trim() ?? string.Empty;
        var trimmedFallback = fallback?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedCustom))
            return trimmedFallback;

        if (string.IsNullOrEmpty(trimmedFallback))
            return trimmedCustom;

        return $"{trimmedCustom} {trimmedFallback}";
    }

    private string? GetOptionalStyle(string? style) => string.IsNullOrWhiteSpace(style) ? null : style;

    private bool IsButtonActionRunning(string buttonName) => this.executingButtonActions.Contains(buttonName);

    private async Task ExecuteButtonActionAsync(AssistantButton button)
    {
        if (this.assistantPlugin is null || button.Action is null || string.IsNullOrWhiteSpace(button.Name))
            return;

        if (!this.executingButtonActions.Add(button.Name))
            return;

        try
        {
            var input = this.BuildPromptInput();
            var cancellationToken = this.cancellationTokenSource?.Token ?? CancellationToken.None;
            var result = await this.assistantPlugin.TryInvokeButtonActionAsync(button, input, cancellationToken);
            if (result is not null)
                this.ApplyButtonActionResult(result);
        }
        finally
        {
            this.executingButtonActions.Remove(button.Name);
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void ApplyButtonActionResult(LuaTable result)
    {
        if (!result.TryGetValue("fields", out var fieldsValue))
            return;

        if (!fieldsValue.TryRead<LuaTable>(out var fieldsTable))
        {
            this.Logger.LogWarning("Assistant BUTTON action returned a non-table 'fields' value. The result is ignored.");
            return;
        }

        foreach (var pair in fieldsTable)
        {
            if (!pair.Key.TryRead<string>(out var fieldName) || string.IsNullOrWhiteSpace(fieldName))
                continue;

            this.TryApplyFieldUpdate(fieldName, pair.Value);
        }
    }

    private void TryApplyFieldUpdate(string fieldName, LuaValue value)
    {
        if (this.inputFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var textValue))
                this.inputFields[fieldName] = textValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string");
            return;
        }

        if (this.dropdownFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var dropdownValue))
                this.dropdownFields[fieldName] = dropdownValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string");
            return;
        }

        if (this.switchFields.ContainsKey(fieldName))
        {
            if (value.TryRead<bool>(out var boolValue))
                this.switchFields[fieldName] = boolValue;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "boolean");
            return;
        }

        if (this.colorPickerFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var colorValue))
                this.colorPickerFields[fieldName] = colorValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string");
            return;
        }

        if (this.webContentFields.TryGetValue(fieldName, out var webContentState))
        {
            if (value.TryRead<string>(out var webContentValue))
                webContentState.Content = webContentValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string");
            return;
        }

        if (this.fileContentFields.TryGetValue(fieldName, out var fileContentState))
        {
            if (value.TryRead<string>(out var fileContentValue))
                fileContentState.Content = fileContentValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string");
            return;
        }

        this.Logger.LogWarning("Assistant BUTTON action tried to update unknown field '{FieldName}'. The value is ignored.", fieldName);
    }

    private void LogFieldUpdateTypeMismatch(string fieldName, string expectedType)
    {
        this.Logger.LogWarning("Assistant BUTTON action tried to write an invalid value to '{FieldName}'. Expected {ExpectedType}.", fieldName, expectedType);
    }

    private string? ValidateProfileSelection(AssistantProfileSelection profileSelection, Profile? profile)
    {
        if (profile == default || profile == Profile.NO_PROFILE)
        {
            if (!string.IsNullOrWhiteSpace(profileSelection.ValidationMessage))
                return profileSelection.ValidationMessage;

            return this.T("Please select one of your profiles.");
        }

        return null;
    }
    
    private async Task Submit()
    {
        this.CreateChatThread();
        var time = this.AddUserRequest(await this.CollectUserPromptAsync());
        await this.AddAIResponseAsync(time);
    }
}
