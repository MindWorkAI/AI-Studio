using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;


/// <summary>
/// Holds the tools AI Studio knows and decides which of them a request may use.
/// </summary>
/// <remarks>
/// Definitions arrive through tool definition sources — the app's own tools from code, later the
/// ones plugin authors write. Every definition passes the same validation regardless of where it
/// came from, which matters most for the ones AI Studio does not control.<br/><br/>
/// Every tool belongs to exactly one collection, see ToolCollectionDefinition: a declared one, or
/// one of its own under its own ID. Whether a tool is switched off and which confidence it needs
/// are questions about its collection, so they are answered here, where the collections are known.
/// </remarks>
public sealed class ToolRegistry
{
    private readonly ILogger<ToolRegistry> logger;
    private readonly SettingsManager settingsManager;
    private readonly ToolSettingsService toolSettingsService;
    private readonly Dictionary<string, ToolDefinition> definitionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IToolImplementation> implementationsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RegisteredCollection> collectionsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> collectionIdsByToolId = new(StringComparer.Ordinal);

    /// <summary>
    /// A declared collection as registered, holding only the tools which are registered themselves.
    /// </summary>
    /// <param name="Definition">The definition, reduced to the registered tools.</param>
    /// <param name="Collection">The collection, which presents the definition.</param>
    private sealed record RegisteredCollection(ToolCollectionDefinition Definition, IToolCollection Collection);

    /// <summary>
    /// What the checks of a single tool found.
    /// </summary>
    /// <param name="BlockReason">What keeps the tool from being offered, or none.</param>
    /// <param name="Implementation">The tool's implementation, once it was found.</param>
    /// <param name="MinimumConfidence">The confidence the tool requires and where that requirement came from, once it was read.</param>
    private readonly record struct ToolCheck(ToolOfferBlockReason BlockReason, IToolImplementation? Implementation, SettingsManager.ToolMinimumProviderConfidenceResolution? MinimumConfidence);

    public ToolRegistry(
        IEnumerable<IToolImplementation> implementations,
        IEnumerable<IToolDefinitionSource> definitionSources,
        IEnumerable<IToolCollection> collections,
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

        // After the tools, since a collection can only gather tools which are registered:
        foreach (var collection in collections)
            this.RegisterCollection(collection);
    }

    /// <summary>
    /// Registers a declared tool collection, with those of its tools which are registered.
    /// </summary>
    /// <remarks>
    /// A tool belongs to one collection at most, or switching one collection off would take a tool
    /// of another one along. A collection may not take the ID of a tool either, since a tool which
    /// belongs to no collection forms one under its own ID. A tool which offers itself from the
    /// context of a chat stays out, because nobody selects it, see ToolActivation.CONTEXT.<br/><br/>
    /// A tool the collection names but which is not registered is left out with a warning, so the
    /// others still work as one. A collection left without any tool is skipped.
    /// </remarks>
    private void RegisterCollection(IToolCollection collection)
    {
        var definition = collection.GetDefinition();
        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            this.logger.LogWarning("Skipping a tool collection with an empty ID.");
            return;
        }

        if (this.definitionsById.ContainsKey(definition.Id))
        {
            this.logger.LogWarning("Skipping tool collection '{CollectionId}' because a tool has the same ID.", definition.Id);
            return;
        }

        if (this.collectionsById.ContainsKey(definition.Id))
        {
            this.logger.LogWarning("Skipping duplicate tool collection ID '{CollectionId}'.", definition.Id);
            return;
        }

        var toolIds = new List<string>(definition.ToolIds.Count);
        foreach (var toolId in definition.ToolIds.Distinct(StringComparer.Ordinal))
        {
            if (this.definitionsById.GetValueOrDefault(toolId) is not { } toolDefinition)
                this.logger.LogWarning("Leaving tool '{ToolId}' out of tool collection '{CollectionId}' because the tool is not registered.", toolId, definition.Id);
            else if (toolDefinition.Activation is not ToolActivation.SELECTION)
                this.logger.LogWarning("Leaving tool '{ToolId}' out of tool collection '{CollectionId}' because nobody selects the tool.", toolId, definition.Id);
            else if (this.collectionIdsByToolId.TryGetValue(toolId, out var otherCollectionId))
                this.logger.LogWarning("Leaving tool '{ToolId}' out of tool collection '{CollectionId}' because it belongs to tool collection '{OtherCollectionId}' already.", toolId, definition.Id, otherCollectionId);
            else
                toolIds.Add(toolId);
        }

        if (toolIds.Count == 0)
        {
            this.logger.LogWarning("Skipping tool collection '{CollectionId}' because none of its tools is registered.", definition.Id);
            return;
        }

        this.collectionsById[definition.Id] = new(definition with { ToolIds = toolIds }, collection);
        foreach (var toolId in toolIds)
            this.collectionIdsByToolId[toolId] = definition.Id;
    }

    /// <summary>
    /// Whether a tool definition is complete enough to register.
    /// </summary>
    /// <remarks>
    /// What a definition cannot be is null in its parts: definitions are C# objects whose members
    /// are non-nullable and initialized, so only their content is checked here. Should definitions
    /// one day arrive from outside as data — a tool plugin, say — that assumption ends at the point
    /// where the data becomes a definition, and it is there that null has to be caught.
    /// </remarks>
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

        if (!IsValidFunctionName(definition.Function.Name))
        {
            issue = "the function name must contain 1-64 ASCII letters, digits, underscores, or hyphens";
            return false;
        }

        if (definition.Function.Parameters.ValueKind is not JsonValueKind.Object)
        {
            issue = "the function parameters schema must be a JSON object";
            return false;
        }

        if (definition.VisibleIn.AllowedComponents.Any(component => !Enum.IsDefined(component)) ||
            definition.VisibleIn.DeniedComponents.Any(component => !Enum.IsDefined(component)))
        {
            issue = "the visibility definition must contain valid component lists";
            return false;
        }

        if (!string.Equals(definition.SettingsSchema.Type, "object", StringComparison.OrdinalIgnoreCase))
        {
            issue = "the settings schema must have type 'object'";
            return false;
        }

        if (definition.SettingsSchema.Properties.Any(x =>
                string.IsNullOrWhiteSpace(x.Key) ||
                !string.Equals(x.Value.Type, "string", StringComparison.OrdinalIgnoreCase)))
        {
            issue = "settings properties must be named string fields";
            return false;
        }

        //
        // An empty group is how a field says it belongs to no group. Whitespace looks the
        // same in the settings file but is a different string, so it would open a second,
        // nameless group next to the ungrouped fields:
        //
        var fieldsWithBlankGroup = definition.SettingsSchema.Properties
            .Where(x => x.Value.Group.Length > 0 && string.IsNullOrWhiteSpace(x.Value.Group))
            .Select(x => x.Key)
            .ToList();
        if (fieldsWithBlankGroup.Count > 0)
        {
            issue = $"these settings declare a blank group name: {string.Join(", ", fieldsWithBlankGroup)}";
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

    public IReadOnlyList<ToolDefinition> GetDefinitionsForComponent(Components component)
    {
        return this.definitionsById.Values
            .Where(x => x.VisibleIn.IsVisibleIn(component) && !this.IsUnavailable(x))
            .OrderBy(x => this.implementationsByKey.GetValueOrDefault(x.ImplementationKey)?.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ToolDefinition> GetAllDefinitions() => this.definitionsById.Values
        .Where(x => !this.IsUnavailable(x))
        .OrderBy(x => this.implementationsByKey.GetValueOrDefault(x.ImplementationKey)?.GetDisplayName(), StringComparer.OrdinalIgnoreCase)
        .ToList();

    /// <summary>
    /// Whether the implementation of a tool says it does not exist right now, e.g., while its preview is switched off.
    /// </summary>
    /// <remarks>
    /// A definition without an implementation is not unavailable in this sense: the lists keep it
    /// as before, and every check leaves it out as a tool nobody knows.
    /// </remarks>
    private bool IsUnavailable(ToolDefinition definition) => this.implementationsByKey.GetValueOrDefault(definition.ImplementationKey) is { IsAvailable: false };

    public ToolDefinition? GetDefinition(string toolId) => this.definitionsById.GetValueOrDefault(toolId);

    public IToolImplementation? GetImplementation(string implementationKey) => this.implementationsByKey.GetValueOrDefault(implementationKey);

    /// <summary>
    /// The ID of the collection a tool belongs to.
    /// </summary>
    /// <remarks>
    /// A tool which belongs to no declared collection forms one of its own, so this is its own ID
    /// then. The ID of a collection stays as it is, and so does an ID the registry does not know,
    /// such as one of a tool from another installation.
    /// </remarks>
    /// <param name="toolOrCollectionId">The ID of a tool, or of a collection.</param>
    /// <returns>The ID of the collection.</returns>
    public string GetCollectionId(string toolOrCollectionId) => this.collectionIdsByToolId.GetValueOrDefault(toolOrCollectionId, toolOrCollectionId);

    /// <summary>
    /// Whether a tool may be used at all: tools are switched on, and the organization did not switch its collection off.
    /// </summary>
    /// <remarks>
    /// An organization switches a collection off by its ID or by the ID of any of its tools. A
    /// single tool of a collection cannot be switched off: the others would stop making sense, and
    /// an administrator who names one tool would rather lose the collection than keep the tool.
    /// </remarks>
    /// <param name="toolOrCollectionId">The ID of a tool, or of a collection.</param>
    /// <returns>True when the tool may be used.</returns>
    public bool IsToolActive(string toolOrCollectionId)
    {
        if (!this.settingsManager.AreToolsEnabled())
            return false;

        var disabledIds = this.settingsManager.ConfigurationData.Tools.DisabledToolIds;
        return !this.GetSettingsIds(this.GetCollectionId(toolOrCollectionId)).Any(disabledIds.Contains);
    }

    /// <summary>
    /// The provider confidence a tool needs: the minimum of its collection, unless the user or an
    /// administrator raised or lowered it.
    /// </summary>
    /// <remarks>
    /// This is the place that knows both halves — the collection's own minimum and the stored
    /// overrides — so callers holding only a tool ID come here instead of to the settings.
    /// </remarks>
    /// <param name="toolOrCollectionId">The ID of a tool, or of a collection.</param>
    public ConfidenceLevel GetMinimumProviderConfidence(string toolOrCollectionId) => this.GetMinimumProviderConfidenceResolution(toolOrCollectionId).ConfidenceLevel;

    public ConfidenceLevel GetMinimumProviderConfidence(ToolDefinition definition) => this.GetMinimumProviderConfidence(definition.Id);

    /// <summary>
    /// Stores which provider confidence the collection of a tool needs, as the user chose it.
    /// </summary>
    /// <remarks>
    /// Whatever tool of a collection the level is chosen for, it is stored for the collection, so
    /// all of its tools need the same level afterward.
    /// </remarks>
    /// <param name="toolOrCollectionId">The ID of a tool, or of a collection.</param>
    /// <param name="confidenceLevel">The level the user chose. Choosing the collection's own minimum removes the override.</param>
    public void SetMinimumProviderConfidence(string toolOrCollectionId, ConfidenceLevel confidenceLevel)
    {
        var collectionId = this.GetCollectionId(toolOrCollectionId);
        this.settingsManager.SetMinimumProviderConfidence(this.GetSettingsIds(collectionId), confidenceLevel, this.GetDefaultMinimumProviderConfidence(collectionId));
    }

    private SettingsManager.ToolMinimumProviderConfidenceResolution GetMinimumProviderConfidenceResolution(string toolOrCollectionId)
    {
        var collectionId = this.GetCollectionId(toolOrCollectionId);
        return this.settingsManager.GetMinimumProviderConfidenceResolution(this.GetSettingsIds(collectionId), this.GetDefaultMinimumProviderConfidence(collectionId));
    }

    /// <summary>
    /// The minimum a collection asks for itself: the one it declares, or that of the tool which forms it.
    /// </summary>
    private ConfidenceLevel GetDefaultMinimumProviderConfidence(string collectionId)
    {
        if (this.collectionsById.TryGetValue(collectionId, out var collection))
            return collection.Definition.MinimumProviderConfidence;

        return this.GetDefinition(collectionId)?.MinimumProviderConfidence ?? ConfidenceLevel.NONE;
    }

    /// <summary>
    /// The IDs which stand for a collection in the settings: its own first, then those of its tools.
    /// </summary>
    /// <remarks>
    /// The ID of a tool stands for its collection, so an entry made for the tool before it joined
    /// the collection, or by an administrator who named the tool, still counts.
    /// </remarks>
    private IReadOnlyList<string> GetSettingsIds(string collectionId) => this.collectionsById.TryGetValue(collectionId, out var collection)
        ? [collectionId, ..collection.Definition.ToolIds]
        : [collectionId];

    /// <summary>
    /// Narrows a selection of tool IDs to those the given provider may actually use in this chat.
    /// </summary>
    /// <remarks>
    /// Used before a request is sent, so the chat records what will really be available rather
    /// than what the user once ticked, and by the token count below the message field, so a tool
    /// the request leaves out does not count. Lives here because judging a tool needs its
    /// definition: the settings know the overrides, the definition knows the tool's own minimum.
    /// Where the chat may still send data is judged with the rule the preparation of a request
    /// uses, see CheckToolAsync.
    /// </remarks>
    /// <param name="provider">The provider the request goes to.</param>
    /// <param name="selectedToolIds">The tools the user selected.</param>
    /// <param name="outboundDataRestriction">Where the chat may still send data, see ChatThread.RequiredOutboundDataRestriction.</param>
    /// <returns>The subset that is enabled, active, available, allowed by the provider's confidence, and allowed by the outbound data restriction.</returns>
    public HashSet<string> FilterToolIdsForProvider(AIStudio.Settings.Provider provider, IEnumerable<string> selectedToolIds, OutboundDataRestriction outboundDataRestriction)
    {
        if (!this.settingsManager.AreToolsEnabled())
            return [];

        if (!provider.GetToolCallingAvailability().IsAvailable)
            return [];

        var providerConfidence = provider.UsedLLMProvider.GetConfidence(this.settingsManager).Level;
        var filtered = ToolSelectionRules.NormalizeSelection(selectedToolIds);
        foreach (var toolId in filtered.ToList())
        {
            if (!this.IsToolActive(toolId))
            {
                filtered.Remove(toolId);
                continue;
            }

            if (!ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, this.GetMinimumProviderConfidence(toolId)))
            {
                filtered.Remove(toolId);
                continue;
            }

            if (this.GetDefinition(toolId) is not { } definition || !this.implementationsByKey.TryGetValue(definition.ImplementationKey, out var implementation))
                continue;

            if (!implementation.IsAvailable || !ToolSelectionRules.IsOutboundDataAllowed(outboundDataRestriction, implementation))
                filtered.Remove(toolId);
        }

        return filtered;
    }

    /// <summary>
    /// The tools somebody can select in this component.
    /// </summary>
    /// <remarks>
    /// Every selection in the app is built from this list: the one below the message field, the
    /// defaults, the templates, and the tools the AI picks for a new assistant. A tool which offers
    /// itself from the context of a chat is left out, because selecting it would change nothing.
    /// The tool list of the app settings asks for all definitions instead, so an organization can
    /// still switch such a tool off or set the trust it requires.
    /// </remarks>
    public async Task<IReadOnlyList<ToolCatalogItem>> GetCatalogAsync(Components component)
    {
        var definitions = this.GetDefinitionsForComponent(component).Where(x => x.Activation is ToolActivation.SELECTION);
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
    public async Task<HashSet<string>> FilterSelectableToolIdsAsync(Components component, IEnumerable<string> toolIds)
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
            if (!this.implementationsByKey.TryGetValue(definition.ImplementationKey, out var implementation) || !implementation.IsAvailable)
                continue;

            items.Add(new ToolCatalogItem
            {
                Definition = definition,
                Implementation = implementation,
                ConfigurationState = await this.toolSettingsService.GetConfigurationStateAsync(definition, implementation),
                IsActive = this.IsToolActive(definition.Id),
                MinimumProviderConfidence = this.GetMinimumProviderConfidence(definition),
            });
        }

        return items;
    }

    /// <summary>
    /// The tools a request offers the model, each with the function it offers in this request.
    /// </summary>
    /// <remarks>
    /// Model capabilities are not a parameter on purpose: they are read from the given provider,
    /// which carries the user's expert capability overrides. Passing them in separately allowed a
    /// caller to gate tools on capabilities that differed from the ones the availability check saw.<br/><br/>
    /// The candidates are the selected tools and every tool which offers itself from the context of
    /// the chat, see ToolActivation. Each one passes the same checks, and only then is it asked what
    /// it offers in this request, see IToolImplementation.ResolveFunctionAsync and
    /// IToolImplementation.ResolveSystemPromptInstructionsAsync.
    /// </remarks>
    /// <param name="context">The request being prepared.</param>
    /// <param name="selectedToolIds">The tools selected for the request.</param>
    /// <param name="mayRunTools">Whether the request may run tools at all, as its caller decides.</param>
    /// <param name="token">The cancellation token of the request.</param>
    /// <returns>The runnable tools, with their definitions as offered in this request.</returns>
    public async Task<IReadOnlyList<(ToolDefinition Definition, IToolImplementation Implementation)>> GetRunnableToolsAsync(ToolResolutionContext context, IEnumerable<string> selectedToolIds, bool mayRunTools, CancellationToken token = default)
    {
        var provider = context.Provider;
        var component = context.Component;
        var providerConfidence = context.ProviderConfidence;
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

        var definitions = this.GetDefinitionsForComponent(component)
            .Where(x => x.Activation is ToolActivation.CONTEXT || selectedToolIdSet.Contains(x.Id))
            .ToList();

        var outboundDataRestriction = context.ChatThread.RequiredOutboundDataRestriction.Restriction;
        var result = new List<(ToolDefinition, IToolImplementation)>(definitions.Count);
        foreach (var definition in definitions)
        {
            var check = await this.CheckToolAsync(definition, providerConfidence, outboundDataRestriction);
            if (check.MinimumConfidence is { } minimumConfidence)
                this.logger.LogDebug("Tool '{ToolId}' uses minimum provider confidence '{ConfidenceLevel}' from {Source}.", definition.Id, minimumConfidence.ConfidenceLevel, minimumConfidence.Source);

            switch (check)
            {
                case { BlockReason: ToolOfferBlockReason.NONE, Implementation: { } implementation }:
                    if (await this.ResolveAsync(definition, implementation, context, token) is { } offeredDefinition)
                        result.Add((offeredDefinition, implementation));

                    break;

                case { BlockReason: ToolOfferBlockReason.TOOL_SWITCHED_OFF }:
                    this.logger.LogDebug("Skipping tool '{ToolId}' because it is disabled by managed configuration.", definition.Id);
                    break;

                case { BlockReason: ToolOfferBlockReason.NOT_CONFIGURED }:
                    this.logger.LogDebug("Skipping tool '{ToolId}' because it is not configured.", definition.Id);
                    break;

                case { BlockReason: ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW }:
                    this.logger.LogInformation("Skipping tool '{ToolId}' because provider confidence '{ProviderConfidence}' is below the required minimum '{MinimumConfidence}'.", definition.Id, providerConfidence, check.MinimumConfidence?.ConfidenceLevel);
                    break;

                case { BlockReason: ToolOfferBlockReason.OUTBOUND_DATA_RESTRICTED }:
                    this.logger.LogInformation("Skipping tool '{ToolId}' because the chat read from a mailbox which restricts outbound data to '{OutboundDataRestriction}'.", definition.Id, outboundDataRestriction);
                    break;

                case { BlockReason: ToolOfferBlockReason.NOT_AVAILABLE_HERE }:
                    this.logger.LogWarning("Skipping tool '{ToolId}' because no implementation is registered.", definition.Id);
                    break;
            }
        }

        foreach (var selectedToolId in selectedToolIdSet.Where(selectedToolId => definitions.All(definition => !definition.Id.Equals(selectedToolId, StringComparison.Ordinal))))
            this.logger.LogDebug("Skipping tool '{ToolId}' because it is not selected in this component or not available in this context.", selectedToolId);

        return result;
    }

    /// <summary>
    /// Whether a tool can be offered to a provider in this component, and if not, what is in the way.
    /// </summary>
    /// <remarks>
    /// Asks the same questions, in the same order, as the preparation of a request does, because
    /// whoever decides something on the tool's behalf must not come to another answer than the
    /// request will. The RAG process, for instance, leaves the searching of the data sources to
    /// Semantic Search only when this says it can be offered; checks of its own which forgot one
    /// of these would leave a chat without its data sources.<br/><br/>
    /// Three questions stay out. Whether the tool is selected is the caller's business, and whether
    /// the tool has anything to offer right now depends on the chat, so only the preparation of a
    /// request can answer it. Where the chat may still send data belongs to the chat as well, see
    /// ChatThread.RequiredOutboundDataRestriction, so the tool is judged as for a chat which read
    /// no mailbox. Semantic Search, which the RAG process asks about, only reaches services
    /// configured in AI Studio, and no restriction ever keeps it back.
    /// </remarks>
    /// <param name="toolId">The tool to check.</param>
    /// <param name="provider">The provider the request would go to.</param>
    /// <param name="component">Where the request would come from.</param>
    /// <returns>ToolOfferBlockReason.NONE when nothing is in the way, otherwise the first obstacle found.</returns>
    public async Task<ToolOfferBlockReason> GetOfferBlockReasonAsync(string toolId, AIStudio.Settings.Provider provider, Components component)
    {
        if (!this.settingsManager.AreToolsEnabled())
            return ToolOfferBlockReason.TOOLS_SWITCHED_OFF;

        if (!provider.GetToolCallingAvailability().IsAvailable)
            return ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS;

        if (this.GetDefinition(toolId) is not { } definition || !definition.VisibleIn.IsVisibleIn(component))
            return ToolOfferBlockReason.NOT_AVAILABLE_HERE;

        var providerConfidence = provider.UsedLLMProvider.GetConfidence(this.settingsManager).Level;
        return (await this.CheckToolAsync(definition, providerConfidence, OutboundDataRestriction.UNRESTRICTED)).BlockReason;
    }

    /// <summary>
    /// How the data sources of a chat are actually searched: the way the user wants, or with every
    /// message when Semantic Search cannot be offered.
    /// </summary>
    /// <remarks>
    /// The one place which decides between the two. The RAG process, the data source selection,
    /// and the check of what a launched chat may search all ask here, so that none of them counts
    /// the agents of the RAG process in or out while another does the opposite. The chat searches
    /// itself when the user prefers it and GetOfferBlockReasonAsync has nothing against it. Its
    /// answer comes along whatever the user prefers, so that the user interface can leave out a
    /// choice which is none and say why the chat searches with every message.<br/><br/>
    /// Whether Semantic Search has data sources to offer right now is no question here. Without
    /// them, the RAG process would find nothing to search either: it asks the same checks, and more
    /// providers have to pass them.
    /// </remarks>
    /// <param name="options">The data source options of the chat.</param>
    /// <param name="provider">The provider the chat runs with.</param>
    /// <param name="component">Where the chat runs.</param>
    /// <returns>How the data sources are searched, and why Semantic Search cannot be used, if so.</returns>
    public async Task<EffectiveRetrievalMode> GetEffectiveRetrievalModeAsync(DataSourceOptions options, AIStudio.Settings.Provider provider, Components component)
    {
        var blockReason = await this.GetOfferBlockReasonAsync(ToolSelectionRules.SEMANTIC_SEARCH_TOOL_ID, provider, component);
        var searchesItself = options.RetrievalMode is DataSourceRetrievalMode.SEMANTIC_SEARCH && blockReason is ToolOfferBlockReason.NONE;
        return new(searchesItself ? DataSourceRetrievalMode.SEMANTIC_SEARCH : DataSourceRetrievalMode.EVERY_MESSAGE, blockReason);
    }

    /// <summary>
    /// Checks one tool on its own, apart from what applies to all tools of a request.
    /// </summary>
    /// <remarks>
    /// Shared by the preparation of a request and by GetOfferBlockReasonAsync, so the two cannot
    /// drift apart. It reports rather than logs: the preparation of a request writes down why a
    /// tool was left out, while a question asked by the user interface on every render must not.
    /// </remarks>
    private async Task<ToolCheck> CheckToolAsync(ToolDefinition definition, ConfidenceLevel providerConfidence, OutboundDataRestriction outboundDataRestriction)
    {
        if (!this.IsToolActive(definition.Id))
            return new(ToolOfferBlockReason.TOOL_SWITCHED_OFF, null, null);

        if (!this.implementationsByKey.TryGetValue(definition.ImplementationKey, out var implementation) || !implementation.IsAvailable)
            return new(ToolOfferBlockReason.NOT_AVAILABLE_HERE, null, null);

        var configurationState = await this.toolSettingsService.GetConfigurationStateAsync(definition, implementation);
        if (!configurationState.IsConfigured)
            return new(ToolOfferBlockReason.NOT_CONFIGURED, implementation, null);

        var minimumConfidence = this.GetMinimumProviderConfidenceResolution(definition.Id);
        if (!ToolSelectionRules.IsProviderConfidenceAllowed(providerConfidence, minimumConfidence.ConfidenceLevel))
            return new(ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW, implementation, minimumConfidence);

        if (!ToolSelectionRules.IsOutboundDataAllowed(outboundDataRestriction, implementation))
            return new(ToolOfferBlockReason.OUTBOUND_DATA_RESTRICTED, implementation, minimumConfidence);

        return new(ToolOfferBlockReason.NONE, implementation, minimumConfidence);
    }

    /// <summary>
    /// Asks a tool which passed every check what it offers in this request.
    /// </summary>
    /// <remarks>
    /// Two answers are taken: the function, of which only the description and the parameters count,
    /// and the instructions for the system prompt. The name and the strict mode stay as registered,
    /// because the model's calls find their tool by that name, and the rest of the definition was
    /// checked a moment ago and must not change after that. The instructions are asked for only once
    /// the tool has a function to offer, since they would otherwise describe a tool the model never
    /// gets to see.
    /// </remarks>
    /// <returns>The definition as offered in this request, or null when the tool has nothing to offer or could not say what.</returns>
    private async Task<ToolDefinition?> ResolveAsync(ToolDefinition definition, IToolImplementation implementation, ToolResolutionContext context, CancellationToken token)
    {
        ToolFunctionDefinition? function;
        string instructions;
        try
        {
            function = await implementation.ResolveFunctionAsync(definition, context, token);
            if (function is null)
            {
                this.logger.LogDebug("Skipping tool '{ToolId}' because it has nothing to offer in this request.", definition.Id);
                return null;
            }

            instructions = await implementation.ResolveSystemPromptInstructionsAsync(definition, context, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this.logger.LogError(exception, "Skipping tool '{ToolId}' because it could not say what it offers in this request.", definition.Id);
            return null;
        }

        var offeredFunction = this.GetOfferedFunction(definition, function);
        if (ReferenceEquals(offeredFunction, definition.Function) && string.Equals(instructions, definition.SystemPromptInstructions, StringComparison.Ordinal))
            return definition;

        return definition with
        {
            Function = offeredFunction,
            SystemPromptInstructions = instructions,
        };
    }

    /// <summary>
    /// The function a tool offers in this request, made of what it answered and what it registered.
    /// </summary>
    /// <returns>The registered function when the tool offers it unchanged or offers something unusable, otherwise the tailored one with the registered name and strict mode.</returns>
    private ToolFunctionDefinition GetOfferedFunction(ToolDefinition definition, ToolFunctionDefinition function)
    {
        if (ReferenceEquals(function, definition.Function))
            return definition.Function;

        if (function.Parameters.ValueKind is not JsonValueKind.Object)
        {
            this.logger.LogWarning("Tool '{ToolId}' offered parameters which are not a JSON object schema. Its function is offered as registered instead.", definition.Id);
            return definition.Function;
        }

        if (!string.Equals(function.Name, definition.Function.Name, StringComparison.Ordinal) || function.Strict != definition.Function.Strict)
            this.logger.LogWarning("Tool '{ToolId}' changed the name or the strict mode of its function for a request. Both stay as registered.", definition.Id);

        return function with
        {
            Name = definition.Function.Name,
            Strict = definition.Function.Strict,
        };
    }
}