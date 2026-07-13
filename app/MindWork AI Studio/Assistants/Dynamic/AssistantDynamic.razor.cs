using System.Text;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;
using AIStudio.Tools.AssistantSessions;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Lua;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.Dynamic;

public partial class AssistantDynamic : AssistantBaseCore<NoSettingsPanel>
{
    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    [Parameter] 
    public AssistantForm? RootComponent { get; set; }
    
    protected override string Title => this.title;
    protected override string Description => this.description;
    protected override string SystemPrompt => this.systemPrompt;
    protected override bool AllowProfiles => this.allowProfiles;
    protected override bool ShowProfileSelection => this.showFooterProfileSelection;
    protected override string SubmitText => this.submitText;
    protected override Func<Task> SubmitAction => this.Submit;
    protected override bool SubmitDisabled => this.isSecurityBlocked;
    // Dynamic assistants do not have dedicated settings yet.
    // Reuse chat-level provider filtering/preselection instead of NONE.
    protected override Tools.Components Component => Tools.Components.CHAT;

    /// <summary>
    /// Gets the plugin ID as the assistant session instance ID.
    /// </summary>
    protected override string AssistantSessionInstanceId => this.assistantPlugin is null ? base.AssistantSessionInstanceId : this.assistantPlugin.Id.ToString();

    protected override IReadOnlyList<IButtonData> FooterButtons =>
    [
        new ButtonData
        {
            Text = T("Revise"),
            Icon = Icons.Material.Filled.AutoMode,
            Color = Color.Default,
            AsyncAction = this.OpenRevisionDialogAsync,
            DisabledActionParam = () => !this.CanReviseCurrentAssistant,
        },
    ];
    
    private string title = string.Empty;
    private string description = string.Empty;
    private string systemPrompt = string.Empty;
    private bool allowProfiles = true;
    private string submitText = string.Empty;
    private bool showFooterProfileSelection = true;
    private PluginAssistants? assistantPlugin;
    
    private readonly AssistantState assistantState = new();
    private readonly Dictionary<string, string> imageCache = new();
    private readonly HashSet<string> executingButtonActions = [];
    private readonly HashSet<string> executingSwitchActions = [];
    private string pluginPath = string.Empty;
    private PluginAssistantAudit? audit;
    private string securityMessage = string.Empty;
    private bool isSecurityBlocked;
    private const string ASSISTANT_QUERY_KEY = "assistantId";
    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();
    private static readonly AssistantSessionStateKey<string> TITLE_STATE_KEY = new(nameof(title));
    private static readonly AssistantSessionStateKey<string> DESCRIPTION_STATE_KEY = new(nameof(description));
    private static readonly AssistantSessionStateKey<string> SYSTEM_PROMPT_STATE_KEY = new(nameof(systemPrompt));
    private static readonly AssistantSessionStateKey<bool> ALLOW_PROFILES_STATE_KEY = new(nameof(allowProfiles));
    private static readonly AssistantSessionStateKey<string> SUBMIT_TEXT_STATE_KEY = new(nameof(submitText));
    private static readonly AssistantSessionStateKey<bool> SHOW_FOOTER_PROFILE_SELECTION_STATE_KEY = new(nameof(showFooterProfileSelection));
    private static readonly AssistantSessionStateKey<PluginAssistants?> ASSISTANT_PLUGIN_STATE_KEY = new(nameof(assistantPlugin));
    private static readonly AssistantSessionStateKey<AssistantState> ASSISTANT_STATE_STATE_KEY = new(nameof(assistantState));
    private static readonly AssistantSessionStateKey<Dictionary<string, string>> IMAGE_CACHE_STATE_KEY = new(nameof(imageCache));
    private static readonly AssistantSessionStateKey<HashSet<string>> EXECUTING_BUTTON_ACTIONS_STATE_KEY = new(nameof(executingButtonActions));
    private static readonly AssistantSessionStateKey<HashSet<string>> EXECUTING_SWITCH_ACTIONS_STATE_KEY = new(nameof(executingSwitchActions));
    private static readonly AssistantSessionStateKey<string> PLUGIN_PATH_STATE_KEY = new(nameof(pluginPath));
    private static readonly AssistantSessionStateKey<PluginAssistantAudit?> AUDIT_STATE_KEY = new(nameof(audit));
    private static readonly AssistantSessionStateKey<string> SECURITY_MESSAGE_STATE_KEY = new(nameof(securityMessage));
    private static readonly AssistantSessionStateKey<bool> IS_SECURITY_BLOCKED_STATE_KEY = new(nameof(isSecurityBlocked));

    private bool CanReviseCurrentAssistant => this.assistantPlugin is { IsInternal: false, IsAssistantBuilderGenerated: true } &&
                                              !string.IsNullOrWhiteSpace(this.assistantPlugin.PluginPath);

    /// <inheritdoc />
    protected override void CaptureCustomAssistantSessionState(AssistantSessionStateWriter state)
    {
        state.Set(TITLE_STATE_KEY, this.title);
        state.Set(DESCRIPTION_STATE_KEY, this.description);
        state.Set(SYSTEM_PROMPT_STATE_KEY, this.systemPrompt);
        state.Set(ALLOW_PROFILES_STATE_KEY, this.allowProfiles);
        state.Set(SUBMIT_TEXT_STATE_KEY, this.submitText);
        state.Set(SHOW_FOOTER_PROFILE_SELECTION_STATE_KEY, this.showFooterProfileSelection);
        state.Set(ASSISTANT_PLUGIN_STATE_KEY, this.assistantPlugin);
        state.Set(ASSISTANT_STATE_STATE_KEY, this.assistantState.Clone());
        state.SetDictionary(IMAGE_CACHE_STATE_KEY, this.imageCache);
        state.SetHashSet(EXECUTING_BUTTON_ACTIONS_STATE_KEY, this.executingButtonActions);
        state.SetHashSet(EXECUTING_SWITCH_ACTIONS_STATE_KEY, this.executingSwitchActions);
        state.Set(PLUGIN_PATH_STATE_KEY, this.pluginPath);
        state.Set(AUDIT_STATE_KEY, this.audit);
        state.Set(SECURITY_MESSAGE_STATE_KEY, this.securityMessage);
        state.Set(IS_SECURITY_BLOCKED_STATE_KEY, this.isSecurityBlocked);
    }

    /// <inheritdoc />
    protected override void RestoreCustomAssistantSessionState(AssistantSessionStateReader state)
    {
        state.Restore(TITLE_STATE_KEY, value => this.title = value);
        state.Restore(DESCRIPTION_STATE_KEY, value => this.description = value);
        state.Restore(SYSTEM_PROMPT_STATE_KEY, value => this.systemPrompt = value);
        state.Restore(ALLOW_PROFILES_STATE_KEY, value => this.allowProfiles = value);
        state.Restore(SUBMIT_TEXT_STATE_KEY, value => this.submitText = value);
        state.Restore(SHOW_FOOTER_PROFILE_SELECTION_STATE_KEY, value => this.showFooterProfileSelection = value);
        state.Restore(ASSISTANT_PLUGIN_STATE_KEY, value => this.assistantPlugin = value);
        state.Restore(ASSISTANT_STATE_STATE_KEY, value => this.assistantState.CopyFrom(value));
        state.RestoreDictionary(IMAGE_CACHE_STATE_KEY, this.imageCache);
        state.RestoreHashSet(EXECUTING_BUTTON_ACTIONS_STATE_KEY, this.executingButtonActions);
        state.RestoreHashSet(EXECUTING_SWITCH_ACTIONS_STATE_KEY, this.executingSwitchActions);
        state.Restore(PLUGIN_PATH_STATE_KEY, value => this.pluginPath = value);
        state.Restore(AUDIT_STATE_KEY, value => this.audit = value);
        state.Restore(SECURITY_MESSAGE_STATE_KEY, value => this.securityMessage = value);
        state.Restore(IS_SECURITY_BLOCKED_STATE_KEY, value => this.isSecurityBlocked = value);
    }

    #region Implementation of AssistantBase

    protected override void OnInitialized()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);
        
        var pluginAssistant = this.ResolveAssistantPlugin();
        if (pluginAssistant is null)
        {
            this.Logger.LogWarning("AssistantDynamic could not resolve a registered assistant plugin.");
            base.OnInitialized();
            return;
        }

        this.assistantPlugin = pluginAssistant;
        this.RootComponent = pluginAssistant.RootComponent;
        this.title = pluginAssistant.AssistantTitle;
        this.description = pluginAssistant.AssistantDescription;
        this.systemPrompt = pluginAssistant.SystemPrompt;
        this.submitText = pluginAssistant.SubmitText;
        this.allowProfiles = pluginAssistant.AllowProfiles;
        this.showFooterProfileSelection = !pluginAssistant.HasEmbeddedProfileSelection;
        this.pluginPath = pluginAssistant.PluginPath;
        var pluginHash = pluginAssistant.ComputeAuditHash();
        this.audit = this.SettingsManager.ConfigurationData.AssistantPluginAudits.FirstOrDefault(x => x.PluginId == pluginAssistant.Id && x.PluginHash == pluginHash);

        var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, pluginAssistant);
        if (!securityState.CanStartAssistant)
        {
            this.assistantPlugin = pluginAssistant;
            this.securityMessage = securityState.Description;
            this.isSecurityBlocked = true;
            base.OnInitialized();
            return;
        }
        
        var rootComponent = this.RootComponent;
        if (rootComponent is not null)
        {
            this.InitializeComponentState(rootComponent.Children);
        }

        base.OnInitialized();
    }
    
    protected override void ResetForm()
    {
        this.assistantState.Clear();

        var rootComponent = this.RootComponent;
        if (rootComponent is not null)
            this.InitializeComponentState(rootComponent.Children);
    }

    protected override bool MightPreselectValues()
    {
        // Dynamic assistants have arbitrary fields supplied via plugins, so there
        // isn't a built-in settings section to prefill values. Always return
        // false to keep the plugin-specified defaults.
        return false;
    }

    #endregion

    #region Implementation of dynamic plugin init

    private PluginAssistants? ResolveAssistantPlugin()
    {
        var pluginAssistants = PluginFactory.RunningPlugins.OfType<PluginAssistants>()
            .Where(plugin => this.SettingsManager.IsPluginEnabled(plugin))
            .ToList();
        if (pluginAssistants.Count == 0)
            return null;

        var requestedPluginId = this.TryGetAssistantIdFromQuery();
        if (requestedPluginId is not { } id) return pluginAssistants.First();
        
        var requestedPlugin = pluginAssistants.FirstOrDefault(p => p.Id == id);
        return requestedPlugin ?? pluginAssistants.First();
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

    private async Task OpenRevisionDialogAsync()
    {
        if (this.assistantPlugin is null || !this.CanReviseCurrentAssistant)
            return;

        var testContext = await this.BuildRevisionTestContextAsync();
        var parameters = new DialogParameters<AssistantPluginRevisionDialog>
        {
            { x => x.PluginId, this.assistantPlugin.Id },
            { x => x.PluginLocalPath, this.assistantPlugin.PluginPath },
            { x => x.TestContext, testContext },
        };

        var dialog = await this.DialogService.ShowAsync<AssistantPluginRevisionDialog>(this.T("Revise Assistant"), parameters, DialogOptions.BLOCKING_FULLSCREEN);
        var result = await dialog.Result;
        if (result is null || result.Canceled)
            return;

        if (result.Data is not AssistantPluginRevisionDialogResult revisionResult)
            return;

        this.Logger.LogInformation($"AssistantDynamic of plugin '{revisionResult.PluginName}' ({revisionResult.PluginName}) was successfully revised with audit result {revisionResult.Audit?.Level ?? AssistantAuditLevel.UNKNOWN}.");
        var updatedPlugin = PluginFactory.RunningPlugins.OfType<PluginAssistants>().FirstOrDefault(x => x.Id == revisionResult.PluginId);
        if (updatedPlugin is not null)
            this.ApplyUpdatedAssistantPlugin(updatedPlugin);

        await this.MessageBus.SendSuccess(new(Icons.Material.Filled.AutoFixHigh, string.Format(this.T("The assistant '{0}' has been updated."), revisionResult.PluginName)));
        await this.MessageBus.SendMessage<bool>(this, Event.PLUGINS_RELOADED);
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task<string> BuildRevisionTestContextAsync()
    {
        var builder = new StringBuilder();

        if (this.assistantPlugin is not null)
        {
            var componentSummary = this.assistantPlugin.CreateAuditComponentSummary();
            if (!string.IsNullOrWhiteSpace(componentSummary))
            {
                builder.AppendLine("Current component overview:");
                builder.AppendLine(componentSummary);
                builder.AppendLine();
            }
        }

        var promptPreview = await this.CollectUserPromptAsync();
        if (!string.IsNullOrWhiteSpace(promptPreview))
        {
            builder.AppendLine("Current prompt preview from the assistant form:");
            builder.AppendLine(promptPreview);
            builder.AppendLine();
        }

        if (this.ResultingContentBlock?.Content is ContentText text && !string.IsNullOrWhiteSpace(text.Text))
        {
            builder.AppendLine("Last assistant response visible in this session:");
            builder.AppendLine(text.Text);
        }

        return builder.ToString().Trim();
    }

    private void ApplyUpdatedAssistantPlugin(PluginAssistants updatedPlugin)
    {
        this.assistantPlugin = updatedPlugin;
        this.RootComponent = updatedPlugin.RootComponent;
        this.title = updatedPlugin.AssistantTitle;
        this.description = updatedPlugin.AssistantDescription;
        this.systemPrompt = updatedPlugin.SystemPrompt;
        this.submitText = updatedPlugin.SubmitText;
        this.allowProfiles = updatedPlugin.AllowProfiles;
        this.showFooterProfileSelection = !updatedPlugin.HasEmbeddedProfileSelection;
        this.pluginPath = updatedPlugin.PluginPath;
        var pluginHash = updatedPlugin.ComputeAuditHash();
        this.audit = this.SettingsManager.ConfigurationData.AssistantPluginAudits.FirstOrDefault(x => x.PluginId == updatedPlugin.Id && x.PluginHash == pluginHash);

        var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, updatedPlugin);
        this.securityMessage = securityState.CanStartAssistant ? string.Empty : securityState.Description;
        this.isSecurityBlocked = !securityState.CanStartAssistant;

        this.assistantState.Clear();
        if (this.RootComponent is not null)
            this.InitializeComponentState(this.RootComponent.Children);
    }

    #endregion

    private string ResolveImageSource(AssistantImage image)
    {
        if (string.IsNullOrWhiteSpace(image.Src))
            return string.Empty;

        if (this.imageCache.TryGetValue(image.Src, out var cached) && !string.IsNullOrWhiteSpace(cached))
            return cached;

        var resolved = image.ResolveSource(this.pluginPath);
        this.imageCache[image.Src] = resolved;
        return resolved;
    }

    private async Task<string> CollectUserPromptAsync()
    {
        if (this.assistantPlugin?.HasCustomPromptBuilder != true) return this.CollectUserPromptFallback();
        
        var input = this.BuildPromptInput();
        var prompt = await this.assistantPlugin.TryBuildPromptAsync(input, this.CancellationTokenSource?.Token ?? CancellationToken.None);
        return !string.IsNullOrWhiteSpace(prompt) ? prompt : this.CollectUserPromptFallback();
    }

    private LuaTable BuildPromptInput()
    {
        var rootComponent = this.RootComponent;
        var state = rootComponent is not null
            ? this.assistantState.ToLuaTable(rootComponent.Children)
            : new LuaTable();

        var profile = new LuaTable
        {
            ["Name"] = this.CurrentProfile.Name,
            ["NeedToKnow"] = this.CurrentProfile.NeedToKnow,
            ["Actions"] = this.CurrentProfile.Actions,
            ["Num"] = this.CurrentProfile.Num,
        };
        
        state["profile"] = profile;
        return state;
    }

    private string CollectUserPromptFallback()
    {
        var prompt = string.Empty;
        var rootComponent = this.RootComponent;
        return rootComponent is null ? prompt : this.CollectUserPromptFallback(rootComponent.Children);
    }

    private void InitializeComponentState(IEnumerable<IAssistantComponent> components)
    {
        foreach (var component in components)
        {
            if (component is IStatefulAssistantComponent statefulComponent)
                statefulComponent.InitializeState(this.assistantState);

            if (component.Children.Count > 0)
                this.InitializeComponentState(component.Children);
        }
    }

    private static string MergeClass(string customClass, string fallback)
    {
        var trimmedCustom = customClass.Trim();
        var trimmedFallback = fallback.Trim();
        if (string.IsNullOrEmpty(trimmedCustom))
            return trimmedFallback;

        return string.IsNullOrEmpty(trimmedFallback) ? trimmedCustom : $"{trimmedCustom} {trimmedFallback}";
    }

    private static string GetOptionalStyle(string? style) => string.IsNullOrWhiteSpace(style) ? string.Empty : style;

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
            var cancellationToken = this.CancellationTokenSource?.Token ?? CancellationToken.None;
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

        this.assistantState.Booleans[switchComponent.Name] = value;

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
            var cancellationToken = this.CancellationTokenSource?.Token ?? CancellationToken.None;
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
        if (!result.TryGetValue("state", out var statesValue))
            return;

        if (!statesValue.TryRead<LuaTable>(out var stateTable))
        {
            this.Logger.LogWarning($"Assistant {sourceType} callback returned a non-table 'state' value. The result is ignored.");
            return;
        }

        foreach (var component in stateTable)
        {
            if (!component.Key.TryRead<string>(out var componentName) || string.IsNullOrWhiteSpace(componentName))
                continue;

            if (!component.Value.TryRead<LuaTable>(out var componentUpdate))
            {
                this.Logger.LogWarning($"Assistant {sourceType} callback returned a non-table update for '{componentName}'. The result is ignored.");
                continue;
            }

            this.TryApplyComponentUpdate(componentName, componentUpdate, sourceType);
        }
    }

    private void TryApplyComponentUpdate(string componentName, LuaTable componentUpdate, AssistantComponentType sourceType)
    {
        if (componentUpdate.TryGetValue("Value", out var value))
            this.TryApplyFieldUpdate(componentName, value, sourceType);

        if (!componentUpdate.TryGetValue("Props", out var propsValue))
            return;

        if (!propsValue.TryRead<LuaTable>(out var propsTable))
        {
            this.Logger.LogWarning($"Assistant {sourceType} callback returned a non-table 'Props' value for '{componentName}'. The props update is ignored.");
            return;
        }

        var rootComponent = this.RootComponent;
        if (rootComponent is null || !TryFindNamedComponent(rootComponent.Children, componentName, out var component))
        {
            this.Logger.LogWarning($"Assistant {sourceType} callback tried to update props of unknown component '{componentName}'. The props update is ignored.");
            return;
        }

        this.ApplyPropUpdates(component, propsTable, sourceType);
    }

    private void TryApplyFieldUpdate(string fieldName, LuaValue value, AssistantComponentType sourceType)
    {
        if (this.assistantState.TryApplyValue(fieldName, value, out var expectedType))
            return;

        if (!string.IsNullOrWhiteSpace(expectedType))
        {
            this.Logger.LogWarning($"Assistant {sourceType} callback tried to write an invalid value to '{fieldName}'. Expected {expectedType}.");
            return;
        }

        this.Logger.LogWarning($"Assistant {sourceType} callback tried to update unknown field '{fieldName}'. The value is ignored.");
    }

    private void ApplyPropUpdates(IAssistantComponent component, LuaTable propsTable, AssistantComponentType sourceType)
    {
        var propSpec = ComponentPropSpecs.SPECS.GetValueOrDefault(component.Type);

        foreach (var prop in propsTable)
        {
            if (!prop.Key.TryRead<string>(out var propName) || string.IsNullOrWhiteSpace(propName))
                continue;

            if (propSpec is not null && propSpec.NonWriteable.Contains(propName, StringComparer.Ordinal))
            {
                this.Logger.LogWarning($"Assistant {sourceType} callback tried to update non-writeable prop '{propName}' on component '{GetComponentName(component)}'. The value is ignored.");
                continue;
            }

            if (!AssistantLuaConversion.TryReadScalarOrStructuredValue(prop.Value, out var convertedValue))
            {
                this.Logger.LogWarning($"Assistant {sourceType} callback returned an unsupported value for prop '{propName}' on component '{GetComponentName(component)}'. The props update is ignored.");
                continue;
            }

            component.Props[propName] = convertedValue;
        }
    }

    private static bool TryFindNamedComponent(IEnumerable<IAssistantComponent> components, string componentName, out IAssistantComponent component)
    {
        foreach (var candidate in components)
        {
            if (candidate is INamedAssistantComponent named && string.Equals(named.Name, componentName, StringComparison.Ordinal))
            {
                component = candidate;
                return true;
            }

            if (candidate.Children.Count > 0 && TryFindNamedComponent(candidate.Children, componentName, out component))
                return true;
        }

        component = null!;
        return false;
    }

    private static string GetComponentName(IAssistantComponent component) => component is INamedAssistantComponent named ? named.Name : component.Type.ToString();

    private EventCallback<HashSet<string>> CreateMultiselectDropdownChangedCallback(string fieldName) =>
        EventCallback.Factory.Create<HashSet<string>>(this, values =>
        {
            this.assistantState.MultiSelect[fieldName] = values;
        });

    private string? ValidateProfileSelection(AssistantProfileSelection profileSelection, Profile? profile)
    {
        if (profile != null && profile != Profile.NO_PROFILE) return null;
        return !string.IsNullOrWhiteSpace(profileSelection.ValidationMessage) ? profileSelection.ValidationMessage : this.T("Please select one of your profiles.");
    }
    
    private async Task Submit()
    {
        if (this.assistantPlugin is not null)
        {
            var securityState = PluginAssistantSecurityResolver.Resolve(this.SettingsManager, this.assistantPlugin);
            if (!securityState.CanStartAssistant)
                return;
        }

        this.CreateChatThread();
        var time = this.AddUserRequest(await this.CollectUserPromptAsync());
        await this.AddAIResponseAsync(time);
    }

    private string CollectUserPromptFallback(IEnumerable<IAssistantComponent> components)
    {
        var prompt = new StringBuilder();

        foreach (var component in components)
        {
            if (component is IStatefulAssistantComponent statefulComponent)
                prompt.Append(statefulComponent.UserPromptFallback(this.assistantState));

            if (component.Children.Count > 0)
            {
                prompt.Append(this.CollectUserPromptFallback(component.Children));
            }
        }
        
        return prompt.Append(Environment.NewLine).ToString();
    }
}
