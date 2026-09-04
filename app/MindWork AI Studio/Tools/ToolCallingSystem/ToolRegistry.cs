using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools.ToolCallingSystem;


/// <summary>
/// Holds the tools AI Studio knows and decides which of them a request may use.
/// </summary>
/// <remarks>
/// Definitions arrive through tool definition sources — the app's own tools from code, later the
/// ones plugin authors write. Every definition passes the same validation regardless of where it
/// came from, which matters most for the ones AI Studio does not control.
/// </remarks>
public sealed class ToolRegistry
{
    private readonly ILogger<ToolRegistry> logger;
    private readonly SettingsManager settingsManager;
    private readonly ToolSettingsService toolSettingsService;
    private readonly Dictionary<string, ToolDefinition> definitionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IToolImplementation> implementationsByKey = new(StringComparer.Ordinal);

    public ToolRegistry(
        IEnumerable<IToolImplementation> implementations,
        IEnumerable<IToolDefinitionSource> definitionSources,
        SettingsManager settingsManager,
        ToolSettingsService toolSettingsService,
        ILogger<ToolRegistry> logger)
    {
        this.logger = logger;
        this.settingsManager = settingsManager;
        this.toolSettingsService = toolSettingsService;

        foreach (var implementation in implementations)
        {
            if (string.IsNullOrWhiteSpace(implementation.ImplementationKey))
            {
                this.logger.LogWarning("Skipping a tool implementation with an empty implementation key.");
                continue;
            }

            if (!this.implementationsByKey.TryAdd(implementation.ImplementationKey, implementation))
                this.logger.LogWarning("Skipping duplicate tool implementation key '{ImplementationKey}'.", implementation.ImplementationKey);
        }

        //
        // Function names are checked across all sources together: two tools offering the same
        // name would be indistinguishable to a model, no matter who defined them.
        //
        var functionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in definitionSources)
        {
            foreach (var definition in source.GetDefinitions())
            {
                if (!TryValidateDefinition(definition, out var validationIssue))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolId}' from source '{SourceName}': {ValidationIssue}", definition.Id, source.SourceName, validationIssue);
                    continue;
                }

                if (!this.implementationsByKey.ContainsKey(definition.ImplementationKey))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolId}' because implementation key '{ImplementationKey}' is not registered.", definition.Id, definition.ImplementationKey);
                    continue;
                }

                if (!this.definitionsById.TryAdd(definition.Id, definition))
                {
                    this.logger.LogWarning("Skipping duplicate tool definition ID '{ToolId}' from source '{SourceName}'.", definition.Id, source.SourceName);
                    continue;
                }

                if (!functionNames.Add(definition.Function.Name))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolId}' because function name '{FunctionName}' is already registered.", definition.Id, definition.Function.Name);
                    this.definitionsById.Remove(definition.Id);
                }
            }
        }
    }

    private static bool TryValidateDefinition(ToolDefinition definition, out string issue)
    {
        issue = string.Empty;
        if (definition.SchemaVersion != 1)
        {
            issue = $"unsupported schema version '{definition.SchemaVersion}'";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            issue = "the definition ID is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.ImplementationKey))
        {
            issue = "the implementation key is empty";
            return false;
        }

        if (definition.Function is null || !IsValidFunctionName(definition.Function.Name))
        {
            issue = "the function name must contain 1-64 ASCII letters, digits, underscores, or hyphens";
            return false;
        }

        if (definition.Function.Parameters.ValueKind is not JsonValueKind.Object)
        {
            issue = "the function parameters schema must be a JSON object";
            return false;
        }

        if (definition.VisibleIn is null ||
            definition.VisibleIn.AllowedComponents is null ||
            definition.VisibleIn.DeniedComponents is null ||
            definition.VisibleIn.AllowedComponents.Any(component => !Enum.IsDefined(component)) ||
            definition.VisibleIn.DeniedComponents.Any(component => !Enum.IsDefined(component)))
        {
            issue = "the visibility definition must contain valid component lists";
            return false;
        }

        if (definition.SettingsSchema is null ||
            !string.Equals(definition.SettingsSchema.Type, "object", StringComparison.OrdinalIgnoreCase) ||
            definition.SettingsSchema.Properties is null ||
            definition.SettingsSchema.Required is null)
        {
            issue = "the settings schema must have type 'object'";
            return false;
        }

        if (definition.SettingsSchema.Properties.Any(x =>
                string.IsNullOrWhiteSpace(x.Key) ||
                x.Value is null ||
                !string.Equals(x.Value.Type, "string", StringComparison.OrdinalIgnoreCase) ||
                x.Value.EnumValues is null))
        {
            issue = "settings properties must be named string fields with valid enum lists";
            return false;
        }

        var fieldsWithBothOptionKinds = definition.SettingsSchema.Properties
            .Where(x => !string.IsNullOrWhiteSpace(x.Value.OptionSource) && x.Value.EnumValues.Count > 0)
            .Select(x => x.Key)
            .ToList();
        if (fieldsWithBothOptionKinds.Count > 0)
        {
            issue = $"these settings declare both an option source and an enum list: {string.Join(", ", fieldsWithBothOptionKinds)}";
            return false;
        }

        var fieldsWithUnknownOptionSource = definition.SettingsSchema.Properties
            .Where(x => !string.IsNullOrWhiteSpace(x.Value.OptionSource) && !ToolSettingsOptionSources.IsKnown(x.Value.OptionSource))
            .Select(x => $"{x.Key} ('{x.Value.OptionSource}')")
            .ToList();
        if (fieldsWithUnknownOptionSource.Count > 0)
        {
            issue = $"these settings reference an unknown option source: {string.Join(", ", fieldsWithUnknownOptionSource)}";
            return false;
        }

        if (definition.SettingsSchema.Required.Any(string.IsNullOrWhiteSpace))
        {
            issue = "required setting names cannot be empty";
            return false;
        }

        var missingRequiredProperties = definition.SettingsSchema.Required
            .Where(x => !definition.SettingsSchema.Properties.ContainsKey(x))
            .ToList();
        if (missingRequiredProperties.Count > 0)
        {
            issue = $"required settings are missing definitions: {string.Join(", ", missingRequiredProperties)}";
            return false;
        }

        return true;
    }

    private static bool IsValidFunctionName(string? functionName) =>
        !string.IsNullOrWhiteSpace(functionName) &&
        functionName.Length <= 64 &&
        functionName.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    public IReadOnlyList<ToolDefinition> GetDefinitionsForComponent(AIStudio.Tools.Components component)
    {
        return this.definitionsById.Values
            .Where(x => x.VisibleIn.IsVisibleIn(component))
            .OrderBy(x => this.implementationsByKey.GetValueOrDefault(x.ImplementationKey)?.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ToolDefinition> GetAllDefinitions() => this.definitionsById.Values
        .OrderBy(x => this.implementationsByKey.GetValueOrDefault(x.ImplementationKey)?.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
        .ToList();

    public ToolDefinition? GetDefinition(string toolId) => this.definitionsById.GetValueOrDefault(toolId);

    public IToolImplementation? GetImplementation(string implementationKey) => this.implementationsByKey.GetValueOrDefault(implementationKey);

    /// <summary>
    /// The provider confidence a tool needs: its own minimum, unless the user or an administrator
    /// raised or lowered it.
    /// </summary>
    /// <remarks>
    /// This is the place that knows both halves — the definition's own minimum and the stored
    /// overrides — so callers holding only a tool ID come here instead of to the settings.
    /// </remarks>
    public ConfidenceLevel GetMinimumProviderConfidence(string toolId) => this.GetDefinition(toolId) is { } definition
        ? this.GetMinimumProviderConfidence(definition)
        : ConfidenceLevel.NONE;

    public ConfidenceLevel GetMinimumProviderConfidence(ToolDefinition definition) =>
        this.settingsManager.GetMinimumProviderConfidenceForTool(definition.Id, definition.MinimumProviderConfidence);

    /// <summary>
    /// Narrows a selection of tool IDs to those the given provider may actually use.
    /// </summary>
    /// <remarks>
    /// Used before a request is sent, so the chat records what will really be available rather
    /// than what the user once ticked. Lives here because judging a tool needs its definition:
    /// the settings know the overrides, the definition knows the tool's own minimum.
    /// </remarks>
    /// <param name="provider">The provider the request goes to.</param>
    /// <param name="selectedToolIds">The tools the user selected.</param>
    /// <returns>The subset that is enabled, active, and allowed by the provider's confidence.</returns>
    public HashSet<string> FilterToolIdsForProvider(AIStudio.Settings.Provider provider, IEnumerable<string> selectedToolIds)
    {
        if (!this.settingsManager.AreToolsEnabled())
            return [];

        if (!provider.GetToolCallingAvailability().IsAvailable)
            return [];

        var providerConfidence = provider.UsedLLMProvider.GetConfidence(this.settingsManager).Level;
        var filtered = ToolSelectionRules.NormalizeSelection(selectedToolIds);
        foreach (var toolId in filtered.ToList())
        {
            if (!this.settingsManager.IsToolActive(toolId))
            {
                filtered.Remove(toolId);
                continue;
            }

            if (!ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, this.GetMinimumProviderConfidence(toolId)))
                filtered.Remove(toolId);
        }

        return filtered;
    }

    public async Task<IReadOnlyList<ToolCatalogItem>> GetCatalogAsync(AIStudio.Tools.Components component)
    {
        var definitions = this.GetDefinitionsForComponent(component);
        return await this.GetCatalogAsync(definitions);
    }

    /// <summary>
    /// Reduces a set of tool IDs to the tools a user could switch on themselves in this component.
    /// </summary>
    /// <remarks>
    /// For preselecting tools on someone's behalf, such as when a launcher opens a chat. A tool
    /// this installation does not know, one an organization switched off, or one whose settings are
    /// incomplete cannot be enabled by hand either, so handing it over as enabled would show the
    /// user a state they could not have produced and could not fix from where they are. The
    /// provider confidence stays out of this: it belongs to the moment a message is sent, not to
    /// the selection, and it may well be a different provider by then.
    /// </remarks>
    public async Task<HashSet<string>> FilterSelectableToolIdsAsync(AIStudio.Tools.Components component, IEnumerable<string> toolIds)
    {
        var wantedToolIds = ToolSelectionRules.NormalizeSelection(toolIds);
        if (wantedToolIds.Count is 0 || !this.settingsManager.AreToolsEnabled())
            return [];

        var catalog = await this.GetCatalogAsync(component);
        return catalog
            .Where(x => wantedToolIds.Contains(x.Definition.Id) && x is { IsActive: true, ConfigurationState.IsConfigured: true })
            .Select(x => x.Definition.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<ToolCatalogItem>> GetCatalogAsync(IEnumerable<ToolDefinition> definitions)
    {
        var definitionList = definitions.ToList();
        var items = new List<ToolCatalogItem>(definitionList.Count);
        foreach (var definition in definitionList)
        {
            if (!this.implementationsByKey.TryGetValue(definition.ImplementationKey, out var implementation))
                continue;

            items.Add(new ToolCatalogItem
            {
                Definition = definition,
                Implementation = implementation,
                ConfigurationState = await this.toolSettingsService.GetConfigurationStateAsync(definition, implementation),
                IsActive = this.settingsManager.IsToolActive(definition.Id),
                MinimumProviderConfidence = this.GetMinimumProviderConfidence(definition),
            });
        }

        return items;
    }

    /// <remarks>
    /// Model capabilities are not a parameter on purpose: they are read from the given provider,
    /// which carries the user's expert capability overrides. Passing them in separately allowed a
    /// caller to gate tools on capabilities that differed from the ones the availability check saw.
    /// </remarks>
    public async Task<IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)>> GetRunnableToolsAsync(AIStudio.Settings.Provider provider,
        Components component, IEnumerable<string> selectedToolIds, ConfidenceLevel providerConfidence, bool mayRunTools)
    {
        if (!this.settingsManager.AreToolsEnabled())
        {
            this.logger.LogDebug("Tool calling is skipped because tools are disabled by managed configuration.");
            return [];
        }

        //
        // Where the user selects the tools, they must be able to see that selection; where the
        // assistant's own rules name them, there is nothing to see. Which of the two applies is
        // decided by the caller, because only it knows where its tools came from:
        //
        if (!mayRunTools)
        {
            this.logger.LogDebug("Tool calling is skipped for component '{Component}' because its tool selection is hidden and no assistant rule names the tools.", component);
            return [];
        }

        var toolCallingAvailability = provider.GetToolCallingAvailability();
        if (!toolCallingAvailability.IsAvailable)
        {
            this.logger.LogDebug("Tool calling is unavailable for provider '{Provider}' with model '{ModelId}': {Reason}", provider.InstanceName, provider.Model.Id, toolCallingAvailability.Message);
            return [];
        }

        var selectedToolIdSet = ToolSelectionRules.NormalizeSelection(selectedToolIds);
        this.logger.LogDebug("Resolving runnable tools for provider '{Provider}' with model '{ModelId}'. Selected tool IDs: [{ToolIds}].", provider.InstanceName, provider.Model.Id, string.Join(", ", selectedToolIdSet.OrderBy(x => x, StringComparer.Ordinal)));

        var definitions = this.GetDefinitionsForComponent(component).Where(x => selectedToolIdSet.Contains(x.Id)).ToList();
        var result = new List<(ToolDefinition, IToolImplementation)>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (!this.settingsManager.IsToolActive(definition.Id))
            {
                this.logger.LogDebug("Skipping tool '{ToolId}' because it is disabled by managed configuration.", definition.Id);
                continue;
            }

            if (!this.implementationsByKey.TryGetValue(definition.ImplementationKey, out var implementation))
            {
                this.logger.LogWarning("Skipping tool '{ToolId}' because no implementation is registered.", definition.Id);
                continue;
            }

            var configurationState = await this.toolSettingsService.GetConfigurationStateAsync(definition, implementation);
            if (!configurationState.IsConfigured)
            {
                this.logger.LogDebug("Skipping tool '{ToolId}' because it is not configured.", definition.Id);
                continue;
            }

            var resolution = this.settingsManager.GetMinimumProviderConfidenceResolutionForTool(definition.Id, definition.MinimumProviderConfidence);
            var minimumToolConfidence = resolution.ConfidenceLevel;
            this.logger.LogDebug("Tool '{ToolId}' uses minimum provider confidence '{ConfidenceLevel}' from {Source}.", definition.Id, minimumToolConfidence, resolution.Source);

            if (!ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, minimumToolConfidence))
            {
                this.logger.LogInformation("Skipping tool '{ToolId}' because provider confidence '{ProviderConfidence}' is below the required minimum '{MinimumConfidence}'.", definition.Id, providerConfidence, minimumToolConfidence);
                continue;
            }

            result.Add((definition, implementation));
        }

        foreach (var selectedToolId in selectedToolIdSet.Where(selectedToolId => definitions.All(definition => !definition.Id.Equals(selectedToolId, StringComparison.Ordinal))))
            this.logger.LogDebug("Skipping tool '{ToolId}' because it is not selected in this component or not available in this context.", selectedToolId);

        return result;
    }
}