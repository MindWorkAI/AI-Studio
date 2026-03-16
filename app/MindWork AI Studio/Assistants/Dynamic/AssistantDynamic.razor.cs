using System;
using System.Collections.Generic;
using System.Globalization;
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

    private string title = string.Empty;
    private string description = string.Empty;
    private string systemPrompt = string.Empty;
    private bool allowProfiles = true;
    private string submitText = string.Empty;
    private bool showFooterProfileSelection = true;
    private PluginAssistants? assistantPlugin;
    
    private readonly Dictionary<string, string> inputFields = new();
    private readonly Dictionary<string, string> dropdownFields = new();
    private readonly Dictionary<string, HashSet<string>> multiselectDropdownFields = new();
    private readonly Dictionary<string, bool> switchFields = new();
    private readonly Dictionary<string, WebContentState> webContentFields = new();
    private readonly Dictionary<string, FileContentState> fileContentFields = new();
    private readonly Dictionary<string, string> colorPickerFields = new();
    private readonly Dictionary<string, string> datePickerFields = new();
    private readonly Dictionary<string, string> dateRangePickerFields = new();
    private readonly Dictionary<string, string> timePickerFields = new();
    private readonly Dictionary<string, string> imageCache = new();
    private readonly HashSet<string> executingButtonActions = [];
    private readonly HashSet<string> executingSwitchActions = [];
    private string pluginPath = string.Empty;
    private const string PLUGIN_SCHEME = "plugin://";
    private const string ASSISTANT_QUERY_KEY = "assistantId";
    private static readonly CultureInfo INVARIANT_CULTURE = CultureInfo.InvariantCulture;
    private static readonly string[] FALLBACK_DATE_FORMATS = ["yyyy-MM-dd", "dd.MM.yyyy", "MM/dd/yyyy"];
    private static readonly string[] FALLBACK_TIME_FORMATS = ["HH:mm", "HH:mm:ss", "hh:mm tt", "h:mm tt"];
    
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
            this.InitializeComponentState(rootComponent.Children);
        }

        base.OnInitialized();
    }

    private PluginAssistants? ResolveAssistantPlugin()
    {
        var assistantPlugins = PluginFactory.RunningPlugins.OfType<PluginAssistants>()
            .Where(plugin => this.SettingsManager.IsPluginEnabled(plugin))
            .ToList();
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
        foreach (var entry in this.datePickerFields)
        {
            this.datePickerFields[entry.Key] = string.Empty;
        }
        foreach (var entry in this.dateRangePickerFields)
        {
            this.dateRangePickerFields[entry.Key] = string.Empty;
        }
        foreach (var entry in this.timePickerFields)
        {
            this.timePickerFields[entry.Key] = string.Empty;
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
        foreach (var entry in this.multiselectDropdownFields)
            fields[entry.Key] = CreateLuaArray(entry.Value);
        foreach (var entry in this.switchFields)
            fields[entry.Key] = entry.Value;
        foreach (var entry in this.webContentFields)
            fields[entry.Key] = entry.Value.Content ?? string.Empty;
        foreach (var entry in this.fileContentFields)
            fields[entry.Key] = entry.Value.Content ?? string.Empty;
        foreach (var entry in this.colorPickerFields)
            fields[entry.Key] = entry.Value ?? string.Empty;
        foreach (var entry in this.datePickerFields)
            fields[entry.Key] = entry.Value ?? string.Empty;
        foreach (var entry in this.dateRangePickerFields)
            fields[entry.Key] = entry.Value ?? string.Empty;
        foreach (var entry in this.timePickerFields)
            fields[entry.Key] = entry.Value ?? string.Empty;

        input["fields"] = fields;

        var meta = new LuaTable();
        var rootComponent = this.RootComponent;
        if (rootComponent is not null)
            this.AddMetaEntries(meta, rootComponent.Children);

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

        return this.CollectUserPromptFallback(rootComponent.Children);
    }

    private void InitializeComponentState(IEnumerable<IAssistantComponent> components)
    {
        foreach (var component in components)
        {
            switch (component.Type)
            {
                case AssistantComponentType.TEXT_AREA:
                    if (component is AssistantTextArea textArea && !this.inputFields.ContainsKey(textArea.Name))
                        this.inputFields.Add(textArea.Name, textArea.PrefillText);
                    break;
                case AssistantComponentType.DROPDOWN:
                    if (component is AssistantDropdown dropdown)
                    {
                        if (dropdown.IsMultiselect)
                        {
                            if (!this.multiselectDropdownFields.ContainsKey(dropdown.Name))
                                this.multiselectDropdownFields.Add(dropdown.Name, CreateInitialMultiselectValues(dropdown));
                        }
                        else if (!this.dropdownFields.ContainsKey(dropdown.Name))
                        {
                            this.dropdownFields.Add(dropdown.Name, dropdown.Default.Value);
                        }
                    }
                    break;
                case AssistantComponentType.SWITCH:
                    if (component is AssistantSwitch switchComponent && !this.switchFields.ContainsKey(switchComponent.Name))
                        this.switchFields.Add(switchComponent.Name, switchComponent.Value);
                    break;
                case AssistantComponentType.WEB_CONTENT_READER:
                    if (component is AssistantWebContentReader webContent && !this.webContentFields.ContainsKey(webContent.Name))
                    {
                        this.webContentFields.Add(webContent.Name, new WebContentState
                        {
                            Preselect = webContent.Preselect,
                            PreselectContentCleanerAgent = webContent.PreselectContentCleanerAgent,
                        });
                    }
                    break;
                case AssistantComponentType.FILE_CONTENT_READER:
                    if (component is AssistantFileContentReader fileContent && !this.fileContentFields.ContainsKey(fileContent.Name))
                        this.fileContentFields.Add(fileContent.Name, new FileContentState());
                    break;
                case AssistantComponentType.COLOR_PICKER:
                    if (component is AssistantColorPicker assistantColorPicker && !this.colorPickerFields.ContainsKey(assistantColorPicker.Name))
                        this.colorPickerFields.Add(assistantColorPicker.Name, assistantColorPicker.Placeholder);
                    break;
                case AssistantComponentType.DATE_PICKER:
                    if (component is AssistantDatePicker datePicker && !this.datePickerFields.ContainsKey(datePicker.Name))
                        this.datePickerFields.Add(datePicker.Name, datePicker.Value);
                    break;
                case AssistantComponentType.DATE_RANGE_PICKER:
                    if (component is AssistantDateRangePicker dateRangePicker && !this.dateRangePickerFields.ContainsKey(dateRangePicker.Name))
                        this.dateRangePickerFields.Add(dateRangePicker.Name, dateRangePicker.Value);
                    break;
                case AssistantComponentType.TIME_PICKER:
                    if (component is AssistantTimePicker timePicker && !this.timePickerFields.ContainsKey(timePicker.Name))
                        this.timePickerFields.Add(timePicker.Name, timePicker.Value);
                    break;
            }

            if (component.Children.Count > 0)
                this.InitializeComponentState(component.Children);
        }
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
    private bool IsSwitchActionRunning(string switchName) => this.executingSwitchActions.Contains(switchName);

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
                this.ApplyActionResult(result, AssistantComponentType.BUTTON);
        }
        finally
        {
            this.executingButtonActions.Remove(button.Name);
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private async Task ExecuteSwitchChangedAsync(AssistantSwitch switchComponent, bool value)
    {
        if (string.IsNullOrWhiteSpace(switchComponent.Name))
            return;

        this.switchFields[switchComponent.Name] = value;

        if (this.assistantPlugin is null || switchComponent.OnChanged is null)
        {
            await this.InvokeAsync(this.StateHasChanged);
            return;
        }

        if (!this.executingSwitchActions.Add(switchComponent.Name))
            return;

        try
        {
            var input = this.BuildPromptInput();
            var cancellationToken = this.cancellationTokenSource?.Token ?? CancellationToken.None;
            var result = await this.assistantPlugin.TryInvokeSwitchChangedAsync(switchComponent, input, cancellationToken);
            if (result is not null)
                this.ApplyActionResult(result, AssistantComponentType.SWITCH);
        }
        finally
        {
            this.executingSwitchActions.Remove(switchComponent.Name);
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    private void ApplyActionResult(LuaTable result, AssistantComponentType sourceType)
    {
        if (!result.TryGetValue("fields", out var fieldsValue))
            return;

        if (!fieldsValue.TryRead<LuaTable>(out var fieldsTable))
        {
            this.Logger.LogWarning("Assistant {ComponentType} callback returned a non-table 'fields' value. The result is ignored.", sourceType);
            return;
        }

        foreach (var pair in fieldsTable)
        {
            if (!pair.Key.TryRead<string>(out var fieldName) || string.IsNullOrWhiteSpace(fieldName))
                continue;

            this.TryApplyFieldUpdate(fieldName, pair.Value, sourceType);
        }
    }

    private void TryApplyFieldUpdate(string fieldName, LuaValue value, AssistantComponentType sourceType)
    {
        if (this.inputFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var textValue))
                this.inputFields[fieldName] = textValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        if (this.dropdownFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var dropdownValue))
                this.dropdownFields[fieldName] = dropdownValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        if (this.multiselectDropdownFields.ContainsKey(fieldName))
        {
            if (value.TryRead<LuaTable>(out var multiselectDropdownValue))
                this.multiselectDropdownFields[fieldName] = ReadStringValues(multiselectDropdownValue);
            else if (value.TryRead<string>(out var singleDropdownValue))
                this.multiselectDropdownFields[fieldName] = string.IsNullOrWhiteSpace(singleDropdownValue) ? [] : [singleDropdownValue];
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string[]", sourceType);
            return;
        }

        if (this.switchFields.ContainsKey(fieldName))
        {
            if (value.TryRead<bool>(out var boolValue))
                this.switchFields[fieldName] = boolValue;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "boolean", sourceType);
            return;
        }

        if (this.colorPickerFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var colorValue))
                this.colorPickerFields[fieldName] = colorValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        if (this.datePickerFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var dateValue))
                this.datePickerFields[fieldName] = dateValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        if (this.dateRangePickerFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var dateRangeValue))
                this.dateRangePickerFields[fieldName] = dateRangeValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        if (this.timePickerFields.ContainsKey(fieldName))
        {
            if (value.TryRead<string>(out var timeValue))
                this.timePickerFields[fieldName] = timeValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        if (this.webContentFields.TryGetValue(fieldName, out var webContentState))
        {
            if (value.TryRead<string>(out var webContentValue))
                webContentState.Content = webContentValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        if (this.fileContentFields.TryGetValue(fieldName, out var fileContentState))
        {
            if (value.TryRead<string>(out var fileContentValue))
                fileContentState.Content = fileContentValue ?? string.Empty;
            else
                this.LogFieldUpdateTypeMismatch(fieldName, "string", sourceType);
            return;
        }

        this.Logger.LogWarning("Assistant {ComponentType} callback tried to update unknown field '{FieldName}'. The value is ignored.", sourceType, fieldName);
    }

    private void LogFieldUpdateTypeMismatch(string fieldName, string expectedType, AssistantComponentType sourceType)
    {
        this.Logger.LogWarning("Assistant {ComponentType} callback tried to write an invalid value to '{FieldName}'. Expected {ExpectedType}.", sourceType, fieldName, expectedType);
    }

    private EventCallback<HashSet<string>> CreateMultiselectDropdownChangedCallback(string fieldName) =>
        EventCallback.Factory.Create<HashSet<string>>(this, values =>
        {
            this.multiselectDropdownFields[fieldName] = values;
        });

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

    private void AddMetaEntries(LuaTable meta, IEnumerable<IAssistantComponent> components)
    {
        foreach (var component in components)
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
                case AssistantDatePicker datePicker:
                    this.AddMetaEntry(meta, datePicker.Name, component.Type, datePicker.Label, datePicker.UserPrompt);
                    break;
                case AssistantDateRangePicker dateRangePicker:
                    this.AddMetaEntry(meta, dateRangePicker.Name, component.Type, dateRangePicker.Label, dateRangePicker.UserPrompt);
                    break;
                case AssistantTimePicker timePicker:
                    this.AddMetaEntry(meta, timePicker.Name, component.Type, timePicker.Label, timePicker.UserPrompt);
                    break;
            }

            if (component.Children.Count > 0)
                this.AddMetaEntries(meta, component.Children);
        }
    }

    private string CollectUserPromptFallback(IEnumerable<IAssistantComponent> components)
    {
        var prompt = string.Empty;

        foreach (var component in components)
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
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                    }
                    break;
                case AssistantComponentType.DROPDOWN:
                    if (component is AssistantDropdown dropdown)
                    {
                        prompt += $"{Environment.NewLine}context:{Environment.NewLine}{dropdown.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (dropdown.IsMultiselect && this.multiselectDropdownFields.TryGetValue(dropdown.Name, out var selections))
                            prompt += $"user prompt:{Environment.NewLine}{string.Join(Environment.NewLine, selections.OrderBy(static value => value, StringComparer.Ordinal))}";
                        else if (this.dropdownFields.TryGetValue(dropdown.Name, out userInput))
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                    }
                    break;
                case AssistantComponentType.SWITCH:
                    if (component is AssistantSwitch switchComponent)
                    {
                        prompt += $"{Environment.NewLine}context:{Environment.NewLine}{switchComponent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.switchFields.TryGetValue(switchComponent.Name, out userDecision))
                            prompt += $"user decision:{Environment.NewLine}{userDecision}";
                    }
                    break;
                case AssistantComponentType.WEB_CONTENT_READER:
                    if (component is AssistantWebContentReader webContent &&
                        this.webContentFields.TryGetValue(webContent.Name, out var webState))
                    {
                        if (!string.IsNullOrWhiteSpace(webContent.UserPrompt))
                            prompt += $"{Environment.NewLine}context:{Environment.NewLine}{webContent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";

                        if (!string.IsNullOrWhiteSpace(webState.Content))
                            prompt += $"user prompt:{Environment.NewLine}{webState.Content}";
                    }
                    break;
                case AssistantComponentType.FILE_CONTENT_READER:
                    if (component is AssistantFileContentReader fileContent &&
                        this.fileContentFields.TryGetValue(fileContent.Name, out var fileState))
                    {
                        if (!string.IsNullOrWhiteSpace(fileContent.UserPrompt))
                            prompt += $"{Environment.NewLine}context:{Environment.NewLine}{fileContent.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";

                        if (!string.IsNullOrWhiteSpace(fileState.Content))
                            prompt += $"user prompt:{Environment.NewLine}{fileState.Content}";
                    }
                    break;
                case AssistantComponentType.COLOR_PICKER:
                    if (component is AssistantColorPicker colorPicker)
                    {
                        prompt += $"context:{Environment.NewLine}{colorPicker.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.colorPickerFields.TryGetValue(colorPicker.Name, out userInput))
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                    }
                    break;
                case AssistantComponentType.DATE_PICKER:
                    if (component is AssistantDatePicker datePicker)
                    {
                        prompt += $"context:{Environment.NewLine}{datePicker.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.datePickerFields.TryGetValue(datePicker.Name, out userInput))
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                    }
                    break;
                case AssistantComponentType.DATE_RANGE_PICKER:
                    if (component is AssistantDateRangePicker dateRangePicker)
                    {
                        prompt += $"context:{Environment.NewLine}{dateRangePicker.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.dateRangePickerFields.TryGetValue(dateRangePicker.Name, out userInput))
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                    }
                    break;
                case AssistantComponentType.TIME_PICKER:
                    if (component is AssistantTimePicker timePicker)
                    {
                        prompt += $"context:{Environment.NewLine}{timePicker.UserPrompt}{Environment.NewLine}---{Environment.NewLine}";
                        if (this.timePickerFields.TryGetValue(timePicker.Name, out userInput))
                            prompt += $"user prompt:{Environment.NewLine}{userInput}";
                    }
                    break;
            }

            if (component.Children.Count > 0)
                prompt += this.CollectUserPromptFallback(component.Children);
        }

        return prompt;
    }

    private static HashSet<string> CreateInitialMultiselectValues(AssistantDropdown dropdown)
    {
        if (string.IsNullOrWhiteSpace(dropdown.Default.Value))
            return [];

        return [dropdown.Default.Value];
    }

    private static LuaTable CreateLuaArray(IEnumerable<string> values)
    {
        var luaArray = new LuaTable();
        var index = 1;

        foreach (var value in values.OrderBy(static value => value, StringComparer.Ordinal))
            luaArray[index++] = value;

        return luaArray;
    }

    private static HashSet<string> ReadStringValues(LuaTable values)
    {
        var parsedValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in values)
        {
            if (entry.Value.TryRead<string>(out var value) && !string.IsNullOrWhiteSpace(value))
                parsedValues.Add(value);
        }

        return parsedValues;
    }

    private DateTime? ParseDatePickerValue(string? value, string? format)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (TryParseDate(value, format, out var parsedDate))
            return parsedDate;

        return null;
    }

    private void SetDatePickerValue(string fieldName, DateTime? value, string? format)
    {
        this.datePickerFields[fieldName] = value.HasValue ? FormatDate(value.Value, format) : string.Empty;
    }

    private DateRange? ParseDateRangePickerValue(string? value, string? format)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split(" - ", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return null;

        if (!TryParseDate(parts[0], format, out var start) || !TryParseDate(parts[1], format, out var end))
            return null;

        return new DateRange(start, end);
    }

    private void SetDateRangePickerValue(string fieldName, DateRange? value, string? format)
    {
        if (value?.Start is null || value.End is null)
        {
            this.dateRangePickerFields[fieldName] = string.Empty;
            return;
        }

        this.dateRangePickerFields[fieldName] = $"{FormatDate(value.Start.Value, format)} - {FormatDate(value.End.Value, format)}";
    }

    private TimeSpan? ParseTimePickerValue(string? value, string? format)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (TryParseTime(value, format, out var parsedTime))
            return parsedTime;

        return null;
    }

    private void SetTimePickerValue(string fieldName, TimeSpan? value, string? format)
    {
        this.timePickerFields[fieldName] = value.HasValue ? FormatTime(value.Value, format) : string.Empty;
    }

    private static bool TryParseDate(string value, string? format, out DateTime parsedDate)
    {
        if (!string.IsNullOrWhiteSpace(format) &&
            DateTime.TryParseExact(value, format, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out parsedDate))
        {
            return true;
        }

        if (DateTime.TryParseExact(value, FALLBACK_DATE_FORMATS, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out parsedDate))
            return true;

        return DateTime.TryParse(value, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out parsedDate);
    }

    private static bool TryParseTime(string value, string? format, out TimeSpan parsedTime)
    {
        if (!string.IsNullOrWhiteSpace(format) &&
            DateTime.TryParseExact(value, format, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out var dateTime))
        {
            parsedTime = dateTime.TimeOfDay;
            return true;
        }

        if (DateTime.TryParseExact(value, FALLBACK_TIME_FORMATS, INVARIANT_CULTURE, DateTimeStyles.AllowWhiteSpaces, out dateTime))
        {
            parsedTime = dateTime.TimeOfDay;
            return true;
        }

        if (TimeSpan.TryParse(value, INVARIANT_CULTURE, out parsedTime))
            return true;

        parsedTime = default;
        return false;
    }

    private static string FormatDate(DateTime value, string? format)
    {
        try
        {
            return value.ToString(string.IsNullOrWhiteSpace(format) ? "yyyy-MM-dd" : format, INVARIANT_CULTURE);
        }
        catch (FormatException)
        {
            return value.ToString("yyyy-MM-dd", INVARIANT_CULTURE);
        }
    }

    private static string FormatTime(TimeSpan value, string? format)
    {
        var dateTime = DateTime.Today.Add(value);

        try
        {
            return dateTime.ToString(string.IsNullOrWhiteSpace(format) ? "HH:mm" : format, INVARIANT_CULTURE);
        }
        catch (FormatException)
        {
            return dateTime.ToString("HH:mm", INVARIANT_CULTURE);
        }
    }
}
