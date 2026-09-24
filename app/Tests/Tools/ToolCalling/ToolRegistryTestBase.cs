using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// What every test of the tool registry needs: settings of its own, a registry around test tools,
/// and providers which can or cannot use them.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, see below, which is why every fixture
/// deriving from this has to be marked as not parallelizable.
/// </remarks>
public abstract class ToolRegistryTestBase
{
    protected const string TOOL_ID = "test_tool";
    protected const string REQUIRED_SETTING = "endpoint";

    private RustService rustService = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    protected SettingsManager SettingsManager { get; private set; } = null!;

    [SetUp]
    public void CreateSettings()
    {
        // Only builds its HTTP clients. Nothing connects, as long as no tool reads a secret:
        this.rustService = new RustService("1", "unused");
        this.SettingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);

        // Self-hosted providers are trusted highly, all others moderately:
        this.SettingsManager.ConfigurationData.Confidence.ConfidenceScheme = ConfidenceSchemes.TRUST_ALL;

        //
        // The managed configuration asks for the settings through the application's service
        // provider rather than taking them as an argument, see ConfigMetaBase.SettingsManagerAccess.
        // Reading the tool's required confidence goes through it, so the settings of this test have
        // to be the ones found there, and only while this test runs:
        //
        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.SettingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    protected ToolRegistry CreateRegistry(params TestTool[] tools)
    {
        var toolSettingsService = new ToolSettingsService(this.SettingsManager, this.rustService, NullLogger<ToolSettingsService>.Instance);
        return new ToolRegistry(tools, [new CodeToolDefinitionSource(tools)], this.SettingsManager, toolSettingsService, NullLogger<ToolRegistry>.Instance);
    }

    protected ToolResolutionContext ContextFor(AIStudio.Settings.Provider provider) => new()
    {
        Provider = provider,
        Component = AIStudio.Tools.Components.CHAT,
        ProviderConfidence = provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level,
        ChatThread = new ChatThread(),
    };

    protected static ToolDefinition Definition(string toolId = TOOL_ID, ConfidenceLevel minimumConfidence = ConfidenceLevel.NONE, bool requiresSetting = false, bool visibleInChat = true, ToolActivation activation = ToolActivation.SELECTION) => new()
    {
        Id = toolId,
        ImplementationKey = toolId,
        MinimumProviderConfidence = minimumConfidence,
        VisibleIn = new() { Chat = visibleInChat },
        Activation = activation,
        SettingsSchema = requiresSetting
            ? ToolSettingsSchemaBuilder.Create().Required(REQUIRED_SETTING).Build()
            : ToolSettingsSchemaBuilder.Create().Build(),
        Function = new()
        {
            Name = toolId,
            DescriptionForLLM = "A tool for tests.",
            Parameters = ToolParameterSchemaBuilder.Create().Build(),
        },
    };

    // Self-hosted, so highly trusted, and able to call functions no matter what the rules say:
    protected static AIStudio.Settings.Provider ToolCapableProvider() => new(0, "self-hosted", "Self-hosted", LLMProviders.SELF_HOSTED, new Model("llama3.3:70b", null))
    {
        CapabilityOverrides = new() { FunctionCalling = true },
    };

    protected static AIStudio.Settings.Provider LessTrustedProvider() => new(1, "cloud", "Cloud", LLMProviders.OPEN_AI, new Model("gpt-5", null))
    {
        CapabilityOverrides = new() { FunctionCalling = true },
    };

    /// <summary>
    /// A tool which does nothing, and offers what it is told to.
    /// </summary>
    /// <param name="definition">What the tool is.</param>
    /// <param name="resolve">What it offers per request; when left out, its function as defined.</param>
    protected sealed class TestTool(ToolDefinition definition, Func<ToolDefinition, ToolFunctionDefinition?>? resolve = null) : IToolImplementation
    {
        public int ResolveCount { get; private set; }

        public string ImplementationKey => definition.ImplementationKey;

        public ToolDefinition GetDefinition() => definition;

        public ValueTask<ToolFunctionDefinition?> ResolveFunctionAsync(ToolDefinition registeredDefinition, ToolResolutionContext context, CancellationToken token = default)
        {
            this.ResolveCount++;
            return ValueTask.FromResult(resolve is null ? registeredDefinition.Function : resolve(registeredDefinition));
        }

        public IReadOnlySet<string> SensitiveTraceArgumentNames { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default) => Task.FromResult(new ToolExecutionResult());
    }
}