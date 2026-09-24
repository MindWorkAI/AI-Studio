using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools;
using AIStudio.Tools.Services;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.ToolCalling;

/// <summary>
/// Checks that asking whether a tool can be offered gets the same answer as preparing a request.
/// </summary>
/// <remarks>
/// The RAG process leaves the searching of the data sources to Semantic Search only when the
/// registry says the tool can be offered. If the question and the preparation of the request ever
/// disagreed, a chat would end up searching nothing at all: the RAG process would stand back, and
/// the request would offer no tool either. Each reason is therefore checked against both.<br/><br/>
/// Not parallelizable, because the settings are reached through Program.SERVICE_PROVIDER, see below.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ToolRegistryOfferTests
{
    private const string TOOL_ID = "test_tool";
    private const string REQUIRED_SETTING = "endpoint";

    private RustService rustService = null!;
    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
    {
        // Only builds its HTTP clients. Nothing connects, as long as no tool reads a secret:
        this.rustService = new RustService("1", "unused");
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);

        // Self-hosted providers are trusted highly, all others moderately:
        this.settingsManager.ConfigurationData.Confidence.ConfidenceScheme = ConfidenceSchemes.TRUST_ALL;

        //
        // The managed configuration asks for the settings through the application's service
        // provider rather than taking them as an argument, see ConfigMetaBase.SettingsManagerAccess.
        // Reading the tool's required confidence goes through it, so the settings of this test have
        // to be the ones found there, and only while this test runs:
        //
        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    [Test]
    public async Task NothingInTheWay()
    {
        await this.AssertBothAgree(this.CreateRegistry(Definition()), ToolCapableProvider(), ToolOfferBlockReason.NONE, "A tool-capable, highly trusted provider and a tool which needs nothing.");
    }

    [Test]
    public async Task ToolsSwitchedOffAltogether()
    {
        this.settingsManager.ConfigurationData.Tools.EnableTools = false;

        await this.AssertBothAgree(this.CreateRegistry(Definition()), ToolCapableProvider(), ToolOfferBlockReason.TOOLS_SWITCHED_OFF, "The organization turned all tools off.");
    }

    [Test]
    public async Task AModelWithoutTools()
    {
        var provider = ToolCapableProvider() with { CapabilityOverrides = new() { FunctionCalling = false } };

        await this.AssertBothAgree(this.CreateRegistry(Definition()), provider, ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS, "The person said their model cannot call functions.");
    }

    [Test]
    public async Task NoProviderSelected()
    {
        await this.AssertBothAgree(this.CreateRegistry(Definition()), AIStudio.Settings.Provider.NONE, ToolOfferBlockReason.MODEL_CANNOT_USE_TOOLS, "Without a provider there is no model that could call a tool.");
    }

    [Test]
    public async Task AToolNotMeantForTheChat()
    {
        await this.AssertBothAgree(this.CreateRegistry(Definition(visibleInChat: false)), ToolCapableProvider(), ToolOfferBlockReason.NOT_AVAILABLE_HERE, "The tool belongs to the assistants only.");
    }

    [Test]
    public async Task AToolNobodyKnows()
    {
        var registry = this.CreateRegistry(Definition());

        Assert.That(await registry.GetOfferBlockReasonAsync("unknown_tool", ToolCapableProvider(), AIStudio.Tools.Components.CHAT), Is.EqualTo(ToolOfferBlockReason.NOT_AVAILABLE_HERE));
    }

    [Test]
    public async Task AToolSwitchedOffByTheOrganization()
    {
        this.settingsManager.ConfigurationData.Tools.DisabledToolIds.Add(TOOL_ID);

        await this.AssertBothAgree(this.CreateRegistry(Definition()), ToolCapableProvider(), ToolOfferBlockReason.TOOL_SWITCHED_OFF, "The organization turned this one tool off.");
    }

    [Test]
    public async Task AToolMissingASetting()
    {
        await this.AssertBothAgree(this.CreateRegistry(Definition(requiresSetting: true)), ToolCapableProvider(), ToolOfferBlockReason.NOT_CONFIGURED, "The tool cannot work without a setting nobody filled in.");
    }

    [Test]
    public async Task AProviderTrustedTooLittle()
    {
        await this.AssertBothAgree(this.CreateRegistry(Definition(minimumConfidence: ConfidenceLevel.HIGH)), LessTrustedProvider(), ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW, "The tool asks for high confidence, the provider has a moderate one.");
    }

    [Test]
    public async Task ARaisedRequirementCountsAsWell()
    {
        this.settingsManager.SetMinimumProviderConfidenceForTool(TOOL_ID, ConfidenceLevel.HIGH, ConfidenceLevel.NONE);

        await this.AssertBothAgree(this.CreateRegistry(Definition()), LessTrustedProvider(), ToolOfferBlockReason.PROVIDER_CONFIDENCE_TOO_LOW, "The tool asks for nothing itself, but its requirement was raised in the settings.");
    }

    private async Task AssertBothAgree(ToolRegistry registry, AIStudio.Settings.Provider provider, ToolOfferBlockReason expected, string situation)
    {
        var reason = await registry.GetOfferBlockReasonAsync(TOOL_ID, provider, AIStudio.Tools.Components.CHAT);
        var providerConfidence = provider.UsedLLMProvider.GetConfidence(this.settingsManager).Level;
        var runnableTools = await registry.GetRunnableToolsAsync(provider, AIStudio.Tools.Components.CHAT, [TOOL_ID], providerConfidence, mayRunTools: true);

        Assert.Multiple(() =>
        {
            Assert.That(reason, Is.EqualTo(expected), situation);
            Assert.That(runnableTools.Any(x => x.Definition.Id == TOOL_ID), Is.EqualTo(expected is ToolOfferBlockReason.NONE), "Preparing the request has to come to the same answer as asking beforehand.");
        });
    }

    private ToolRegistry CreateRegistry(ToolDefinition definition)
    {
        var tool = new TestTool(definition);
        var toolSettingsService = new ToolSettingsService(this.settingsManager, this.rustService, NullLogger<ToolSettingsService>.Instance);
        return new ToolRegistry([tool], [new CodeToolDefinitionSource([tool])], this.settingsManager, toolSettingsService, NullLogger<ToolRegistry>.Instance);
    }

    private static ToolDefinition Definition(ConfidenceLevel minimumConfidence = ConfidenceLevel.NONE, bool requiresSetting = false, bool visibleInChat = true) => new()
    {
        Id = TOOL_ID,
        ImplementationKey = TOOL_ID,
        MinimumProviderConfidence = minimumConfidence,
        VisibleIn = new() { Chat = visibleInChat },
        SettingsSchema = requiresSetting
            ? ToolSettingsSchemaBuilder.Create().Required(REQUIRED_SETTING).Build()
            : ToolSettingsSchemaBuilder.Create().Build(),
        Function = new()
        {
            Name = TOOL_ID,
            DescriptionForLLM = "A tool for tests.",
            Parameters = ToolParameterSchemaBuilder.Create().Build(),
        },
    };

    // Self-hosted, so highly trusted, and able to call functions no matter what the rules say:
    private static AIStudio.Settings.Provider ToolCapableProvider() => new(0, "self-hosted", "Self-hosted", LLMProviders.SELF_HOSTED, new Model("llama3.3:70b", null))
    {
        CapabilityOverrides = new() { FunctionCalling = true },
    };

    private static AIStudio.Settings.Provider LessTrustedProvider() => new(1, "cloud", "Cloud", LLMProviders.OPEN_AI, new Model("gpt-5", null))
    {
        CapabilityOverrides = new() { FunctionCalling = true },
    };

    private sealed class TestTool(ToolDefinition definition) : IToolImplementation
    {
        public string ImplementationKey => definition.ImplementationKey;

        public ToolDefinition GetDefinition() => definition;

        public IReadOnlySet<string> SensitiveTraceArgumentNames { get; } = new HashSet<string>(StringComparer.Ordinal);

        public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, ToolExecutionContext context, CancellationToken token = default) => Task.FromResult(new ToolExecutionResult());
    }
}