using System.Text.Json;

using AIStudio.Provider;

using Microsoft.AspNetCore.Hosting;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolRegistry
{
    private readonly ILogger<ToolRegistry> logger;
    private readonly ToolSettingsService toolSettingsService;
    private readonly Dictionary<string, ToolDefinition> definitionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IToolImplementation> implementationsByKey = new(StringComparer.Ordinal);

    public ToolRegistry(
        IWebHostEnvironment webHostEnvironment,
        IEnumerable<IToolImplementation> implementations,
        ToolSettingsService toolSettingsService,
        ILogger<ToolRegistry> logger)
    {
        this.logger = logger;
        this.toolSettingsService = toolSettingsService;

        foreach (var implementation in implementations)
            this.implementationsByKey[implementation.ImplementationKey] = implementation;

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

        foreach (var file in definitionsDirectory.Where(x => !x.IsDirectory && x.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var stream = file.CreateReadStream();
                var definition = JsonSerializer.Deserialize<ToolDefinition>(stream, serializerOptions);
                if (definition is null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolFile}' because it could not be deserialized.", file.Name);
                    continue;
                }

                if (!this.implementationsByKey.ContainsKey(definition.ImplementationKey))
                {
                    this.logger.LogWarning("Skipping tool definition '{ToolId}' because implementation key '{ImplementationKey}' is not registered.", definition.Id, definition.ImplementationKey);
                    continue;
                }

                this.definitionsById[definition.Id] = definition;
            }
            catch (Exception exception)
            {
                this.logger.LogWarning(exception, "Skipping invalid tool definition file '{ToolFile}'.", file.Name);
            }
        }
    }

    public IReadOnlyList<ToolDefinition> GetDefinitionsForComponent(AIStudio.Tools.Components component)
    {
        var isChat = component is AIStudio.Tools.Components.CHAT;
        return this.definitionsById.Values
            .Where(x => isChat ? x.VisibleIn.Chat : x.VisibleIn.Assistants)
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ToolDefinition> GetAllDefinitions() => this.definitionsById.Values
        .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
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
            items.Add(new ToolCatalogItem
            {
                Definition = definition,
                ConfigurationState = await this.toolSettingsService.GetConfigurationStateAsync(definition),
            });
        }

        return items;
    }

    public async Task<IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)>> GetRunnableToolsAsync(
        AIStudio.Tools.Components component,
        IEnumerable<string> selectedToolIds,
        IReadOnlyCollection<Capability> modelCapabilities,
        bool isToolSelectionVisible)
    {
        if (!isToolSelectionVisible)
            return [];

        if (!modelCapabilities.Contains(Capability.CHAT_COMPLETION_API) || !modelCapabilities.Contains(Capability.FUNCTION_CALLING))
            return [];

        var selectedToolIdSet = selectedToolIds.ToHashSet(StringComparer.Ordinal);
        var definitions = this.GetDefinitionsForComponent(component).Where(x => selectedToolIdSet.Contains(x.Id)).ToList();
        var result = new List<(ToolDefinition, IToolImplementation)>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (!this.implementationsByKey.TryGetValue(definition.ImplementationKey, out var implementation))
                continue;

            var configurationState = await this.toolSettingsService.GetConfigurationStateAsync(definition);
            if (!configurationState.IsConfigured)
                continue;

            result.Add((definition, implementation));
        }

        return result;
    }
}
