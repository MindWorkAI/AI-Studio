using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Hosting;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolRegistry
{
    private readonly ILogger<ToolRegistry> logger;
    private readonly SettingsManager settingsManager;
    private readonly ToolSettingsService toolSettingsService;
    private readonly Dictionary<string, ToolDefinition> definitionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IToolImplementation> implementationsByKey = new(StringComparer.Ordinal);

    public ToolRegistry(
        IWebHostEnvironment webHostEnvironment,
        IEnumerable<IToolImplementation> implementations,
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

        var definitionsDirectory = webHostEnvironment.WebRootFileProvider.GetDirectoryContents("tool_definitions");
        if (!definitionsDirectory.Exists)
        {
            this.logger.LogWarning("The tool definitions directory was not found.");
            return;
        }

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        var functionNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in definitionsDirectory.Where(x => !x.IsDirectory && x.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var stream = file.CreateReadStream();
                var definition = JsonSerializer.Deserialize<ToolDefinition>(stream, serializerOptions);
                if (definition is null)
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolFile}' because it could not be deserialized.", file.Name);
                    continue;
                }

                if (!TryValidateDefinition(definition, out var validationIssue))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolFile}': {ValidationIssue}", file.Name, validationIssue);
                    continue;
                }

                if (!this.implementationsByKey.ContainsKey(definition.ImplementationKey))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolId}' because implementation key '{ImplementationKey}' is not registered.", definition.Id, definition.ImplementationKey);
                    continue;
                }

                if (this.definitionsById.ContainsKey(definition.Id))
                {
                    this.logger.LogWarning("Skipping duplicate tool definition ID '{ToolId}' from '{ToolFile}'.", definition.Id, file.Name);
                    continue;
                }

                if (!functionNames.Add(definition.Function.Name))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolId}' because function name '{FunctionName}' is already registered.", definition.Id, definition.Function.Name);
                    continue;
                }

                this.definitionsById.Add(definition.Id, definition);
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(exception, "Skipping invalid tool definition file '{ToolFile}'.", file.Name);
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
        var isChat = component is AIStudio.Tools.Components.CHAT;
        return this.definitionsById.Values
            .Where(x => isChat ? x.VisibleIn.Chat : x.VisibleIn.Assistants)
            .OrderBy(x => this.implementationsByKey.GetValueOrDefault(x.ImplementationKey)?.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ToolDefinition> GetAllDefinitions() => this.definitionsById.Values
        .OrderBy(x => this.implementationsByKey.GetValueOrDefault(x.ImplementationKey)?.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
        .ToList();

    public ToolDefinition? GetDefinition(string toolId) => this.definitionsById.GetValueOrDefault(toolId);

    public IToolImplementation? GetImplementation(string implementationKey) => this.implementationsByKey.GetValueOrDefault(implementationKey);

    public async Task<IReadOnlyList<ToolCatalogItem>> GetCatalogAsync(AIStudio.Tools.Components component)
    {
        var definitions = this.GetDefinitionsForComponent(component);
        return await this.GetCatalogAsync(definitions);
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
                MinimumProviderConfidence = this.settingsManager.GetMinimumProviderConfidenceForTool(definition.Id),
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)>> GetRunnableToolsAsync(
        AIStudio.Settings.Provider provider,
        AIStudio.Tools.Components component,
        IEnumerable<string> selectedToolIds,
        IReadOnlyCollection<Capability> modelCapabilities,
        ConfidenceLevel providerConfidence,
        bool isToolSelectionVisible)
    {
        if (!this.settingsManager.AreToolsEnabled())
        {
            this.logger.LogInformation("Tool calling is skipped because tools are disabled by managed configuration.");
            return [];
        }

        if (!isToolSelectionVisible)
        {
            this.logger.LogInformation("Tool calling is skipped for component '{Component}' because tool selection is not visible.", component);
            return [];
        }

        var toolCallingAvailability = provider.GetToolCallingAvailability();
        if (!toolCallingAvailability.IsAvailable)
        {
            this.logger.LogInformation("Tool calling is unavailable for provider '{Provider}' with model '{ModelId}': {Reason}", provider.InstanceName, provider.Model.Id, toolCallingAvailability.Message);
            return [];
        }

        if (!modelCapabilities.Contains(Capability.FUNCTION_CALLING) ||
            (!modelCapabilities.Contains(Capability.CHAT_COMPLETION_API) && !modelCapabilities.Contains(Capability.RESPONSES_API)))
        {
            this.logger.LogInformation("Tool calling is unavailable for provider '{Provider}' with model '{ModelId}' because the model lacks the required API or function-calling capability.", provider.InstanceName, provider.Model.Id);
            return [];
        }

        var selectedToolIdSet = ToolSelectionRules.NormalizeSelection(selectedToolIds);
        this.logger.LogInformation("Resolving runnable tools for provider '{Provider}' with model '{ModelId}'. Selected tool IDs: [{ToolIds}].", provider.InstanceName, provider.Model.Id, string.Join(", ", selectedToolIdSet.OrderBy(x => x, StringComparer.Ordinal)));

        var definitions = this.GetDefinitionsForComponent(component).Where(x => selectedToolIdSet.Contains(x.Id)).ToList();
        var result = new List<(ToolDefinition, IToolImplementation)>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (!this.settingsManager.IsToolActive(definition.Id))
            {
                this.logger.LogInformation("Skipping tool '{ToolId}' because it is disabled by managed configuration.", definition.Id);
                continue;
            }

            if (!this.implementationsByKey.TryGetValue(definition.ImplementationKey, out var implementation))
            {
                this.logger.LogInformation("Skipping tool '{ToolId}' because no implementation is registered.", definition.Id);
                continue;
            }

            var configurationState = await this.toolSettingsService.GetConfigurationStateAsync(definition, implementation);
            if (!configurationState.IsConfigured)
            {
                this.logger.LogInformation("Skipping tool '{ToolId}' because it is not configured.", definition.Id);
                continue;
            }

            var resolution = this.settingsManager.GetMinimumProviderConfidenceResolutionForTool(definition.Id);
            var minimumToolConfidence = resolution.ConfidenceLevel;
            this.logger.LogInformation("Tool '{ToolId}' uses minimum provider confidence '{ConfidenceLevel}' from {Source}.", definition.Id, minimumToolConfidence, resolution.Source);

            if (!ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, minimumToolConfidence))
            {
                this.logger.LogInformation("Skipping tool '{ToolId}' because provider confidence '{ProviderConfidence}' is below the required minimum '{MinimumConfidence}'.", definition.Id, providerConfidence, minimumToolConfidence);
                continue;
            }

            result.Add((definition, implementation));
        }

        foreach (var selectedToolId in selectedToolIdSet.Where(selectedToolId => definitions.All(definition => !definition.Id.Equals(selectedToolId, StringComparison.Ordinal))))
            this.logger.LogInformation("Skipping tool '{ToolId}' because it is not selected in this component or not available in this context.", selectedToolId);

        return result;
    }
}
