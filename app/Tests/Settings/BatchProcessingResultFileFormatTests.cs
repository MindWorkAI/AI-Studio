using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which format of the result files an organization can preselect for the Batch Processing
/// assistant through a configuration plugin.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, as in the app, because a managed setting
/// writes its value and its lock there. The plugin passes the check of DataBatchProcessing, as
/// PluginConfiguration does.
/// </remarks>
[TestFixture]
public sealed class BatchProcessingResultFileFormatTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("9d2f6c41-3b8e-4a07-b5d1-e6a8c2f04b73");

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
    public async Task APluginPreselectsAFormatForEntireAnswers()
    {
        var processed = await ProcessAsync("MICROSOFT_WORD");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True);
            Assert.That(this.ResultFileFormat, Is.EqualTo(FileExportFormat.MICROSOFT_WORD));
        }
    }

    [TestCase("CSV", TestName = "A table, which holds one table out of an answer")]
    [TestCase("SOURCE_CODE", TestName = "Code, which takes its extension from a language")]
    [TestCase("NONE", TestName = "No format at all")]
    public async Task AFormatWhichHoldsNoEntireAnswerIsRejected(string configuredFormat)
    {
        this.ResultFileFormat = FileExportFormat.HTML;
        var processed = await ProcessAsync(configuredFormat);

        ManagedConfiguration.TryGet(x => x.BatchProcessing, x => x.ResultFileFormat, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False, "The result files would end up without an extension or as something other than an answer.");
            Assert.That(this.ResultFileFormat, Is.EqualTo(FileExportFormat.HTML), "A rejected value leaves the choice of the user untouched.");
            Assert.That(configMeta.IsLocked, Is.False, "A rejected value locks nothing.");
        }
    }

    private FileExportFormat ResultFileFormat
    {
        get => this.settingsManager.ConfigurationData.BatchProcessing.ResultFileFormat;
        set => this.settingsManager.ConfigurationData.BatchProcessing.ResultFileFormat = value;
    }

    /// <summary>
    /// Lets a configuration plugin preselect the format of the result files.
    /// </summary>
    /// <param name="format">The name of the format as the plugin writes it.</param>
    /// <returns>Whether the plugin's value was taken.</returns>
    private static async Task<bool> ProcessAsync(string format)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();

        await state.DoStringAsync($"""
                                   SETTINGS = {"{}"}
                                   SETTINGS["DataBatchProcessing.ResultFileFormat"] = "{format}"
                                   """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        return ManagedConfiguration.TryProcessConfiguration(x => x.BatchProcessing, x => x.ResultFileFormat, PLUGIN_ID, settingsTable, false, validator: DataBatchProcessing.IsValidResultFileFormat);
    }
}