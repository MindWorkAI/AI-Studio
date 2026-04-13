using System.Text;
using AIStudio.Dialogs.Settings;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Lua;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;

namespace AIStudio.Assistants.Dynamic;

public partial class AssistantDynamic : AssistantBaseCore<NoSettingsPanel>
{
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

    #region Implementation of AssistantBase

    protected override void OnInitialized()
    {
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
        var prompt = await this.assistantPlugin.TryBuildPromptAsync(input, this.cancellationTokenSource?.Token ?? CancellationToken.None);
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
            ["Name"] = this.currentProfile.Name,
            ["NeedToKnow"] = this.currentProfile.NeedToKnow,
            ["Actions"] = this.currentProfile.Actions,
            ["Num"] = this.currentProfile.Num,
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
