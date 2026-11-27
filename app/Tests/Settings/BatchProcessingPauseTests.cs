using System.Linq.Expressions;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how a configuration plugin sets the bounds of the pause between two files of the Batch
/// Processing Assistant.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, as in the app, because a managed setting
/// writes its value and its lock there. The plugin passes the check of PluginConfiguration.
/// </remarks>
[TestFixture]
public sealed class BatchProcessingPauseTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("5c1e8f27-94ad-4b36-a0f2-7d3b6e91c4a8");

    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
    {
        // Creating the settings data registers every managed setting:
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        // The managed state of a setting outlives this fixture, so we release what the plugin took over:
        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public async Task APluginLocksTheUpperBound()
    {
        var processed = await ProcessAsync(x => x.MaximumDelaySeconds, """SETTINGS["DataBatchProcessing.MaximumDelaySeconds"] = 30""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True);
            Assert.That(this.settingsManager.ConfigurationData.BatchProcessing.MaximumDelaySeconds, Is.EqualTo(30));
            Assert.That(DataBatchProcessing.IsMaximumPauseLocked, Is.True);
            Assert.That(DataBatchProcessing.IsMinimumPauseLocked, Is.False);
        }
    }

    [TestCase(5, TestName = "An upper bound below 6 seconds is rejected")]
    [TestCase(301, TestName = "An upper bound above 300 seconds is rejected")]
    public async Task AnUpperBoundOutsideTheLimitsIsRejected(int configuredSeconds)
    {
        this.settingsManager.ConfigurationData.BatchProcessing.MaximumDelaySeconds = 20;
        var processed = await ProcessAsync(x => x.MaximumDelaySeconds, $"""SETTINGS["DataBatchProcessing.MaximumDelaySeconds"] = {configuredSeconds}""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False);
            Assert.That(this.settingsManager.ConfigurationData.BatchProcessing.MaximumDelaySeconds, Is.EqualTo(20), "A rejected value leaves the choice of the user untouched.");
            Assert.That(DataBatchProcessing.IsMaximumPauseLocked, Is.False, "A rejected value locks nothing.");
        }
    }

    [Test]
    public async Task ABoundWhichUsersMayChangeIsNoLock()
    {
        const string SETTINGS = """
                                SETTINGS["DataBatchProcessing.MinimumDelaySeconds"] = 12
                                SETTINGS["DataBatchProcessing.MinimumDelaySeconds.AllowUserOverride"] = true
                                SETTINGS["DataBatchProcessing.MaximumDelaySeconds"] = 30
                                SETTINGS["DataBatchProcessing.MaximumDelaySeconds.AllowUserOverride"] = true
                                """;

        var minimumProcessed = await ProcessAsync(x => x.MinimumDelaySeconds, SETTINGS);
        var maximumProcessed = await ProcessAsync(x => x.MaximumDelaySeconds, SETTINGS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(minimumProcessed, Is.True);
            Assert.That(maximumProcessed, Is.True);
            Assert.That(DataBatchProcessing.IsMinimumPauseLocked, Is.False, "Users may change a lower bound which the organization only presets.");
            Assert.That(DataBatchProcessing.IsMaximumPauseLocked, Is.False, "Users may change an upper bound which the organization only presets.");
        }
    }

    /// <summary>
    /// Lets a configuration plugin set a bound of the pause.
    /// </summary>
    /// <param name="bound">The bound to process.</param>
    /// <param name="settings">The Lua lines which fill the table SETTINGS of the plugin.</param>
    /// <returns>Whether the plugin's value was taken.</returns>
    private static async Task<bool> ProcessAsync(Expression<Func<DataBatchProcessing, int>> bound, string settings)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();

        await state.DoStringAsync($"""
                                   SETTINGS = {"{}"}
                                   {settings}
                                   """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        return ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, bound, PLUGIN_ID, settingsTable, false, validator: DataBatchProcessing.PAUSE_LIMITS.Allows);
    }
}