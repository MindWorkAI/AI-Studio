using System.Globalization;

using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what an organization sets through a configuration plugin for compressing a chat.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, as in the app, because a managed setting
/// writes its value and its lock there. The two numbers pass the range checks of DataChatCompression, as
/// PluginConfiguration does.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionSettingsTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("8d2f6a91-3c47-4b05-9e18-5a7c0b4e2f63");

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
    public void TheDefaultsKeepTheBehaviorOfEarlierVersions()
    {
        var settings = new DataChatCompression();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.PreselectedProvider, Is.Empty, "Without a provider of its own, the model of the chat writes the summary.");
            Assert.That(settings.ContextWindowThresholdPercent, Is.EqualTo(95));
            Assert.That(settings.MaxSummaryTokens, Is.EqualTo(8_192));
            Assert.That(settings.ContextWindowFullBehavior, Is.EqualTo(ContextWindowFullBehavior.ASK));
        }
    }

    [TestCase(50)]
    [TestCase(80)]
    [TestCase(95)]
    public async Task APluginSetsTheShareOfTheContextWindow(int percent)
    {
        var processed = await ProcessThresholdAsync(percent.ToString(CultureInfo.InvariantCulture));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True);
            Assert.That(this.settingsManager.ConfigurationData.ChatCompression.ContextWindowThresholdPercent, Is.EqualTo(percent));
        }
    }

    [TestCase("49")]
    [TestCase("96")]
    [TestCase("0")]
    public async Task AShareOutsideTheRangeIsRejected(string percent)
    {
        this.settingsManager.ConfigurationData.ChatCompression.ContextWindowThresholdPercent = 90;
        var processed = await ProcessThresholdAsync(percent);

        ManagedConfiguration.TryGet(x => x.ChatCompression, x => x.ContextWindowThresholdPercent, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False);
            Assert.That(this.settingsManager.ConfigurationData.ChatCompression.ContextWindowThresholdPercent, Is.EqualTo(90), "A rejected value leaves the choice of the user untouched.");
            Assert.That(configMeta.IsLocked, Is.False, "A rejected value locks nothing.");
        }
    }

    [TestCase(1_024)]
    [TestCase(32_768)]
    public async Task APluginLimitsTheSummary(int tokens)
    {
        var processed = await ProcessMaxSummaryTokensAsync(tokens.ToString(CultureInfo.InvariantCulture));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True);
            Assert.That(this.settingsManager.ConfigurationData.ChatCompression.MaxSummaryTokens, Is.EqualTo(tokens));
        }
    }

    [TestCase("1023")]
    [TestCase("32769")]
    public async Task ALimitOfTheSummaryOutsideTheRangeIsRejected(string tokens)
    {
        this.settingsManager.ConfigurationData.ChatCompression.MaxSummaryTokens = 4_096;
        var processed = await ProcessMaxSummaryTokensAsync(tokens);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.False);
            Assert.That(this.settingsManager.ConfigurationData.ChatCompression.MaxSummaryTokens, Is.EqualTo(4_096), "A rejected value leaves the choice of the user untouched.");
        }
    }

    [Test]
    public async Task APluginSetsWhatHappensWhenTheContextWindowFills()
    {
        var table = await CreateSettingsTableAsync("""SETTINGS["DataChatCompression.ContextWindowFullBehavior"] = "COMPRESS_INTO_NEW_CHAT" """);
        var processed = ManagedConfiguration.TryProcessConfiguration(x => x.ChatCompression, x => x.ContextWindowFullBehavior, PLUGIN_ID, table, false);

        var isManaged = ManagedConfiguration.TryGet(x => x.ChatCompression, x => x.ContextWindowFullBehavior, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True);
            Assert.That(this.settingsManager.ConfigurationData.ChatCompression.ContextWindowFullBehavior, Is.EqualTo(ContextWindowFullBehavior.COMPRESS_INTO_NEW_CHAT));
            Assert.That(isManaged, Is.True);
            Assert.That(configMeta.IsLocked, Is.True, "Without AllowUserOverride, the organization locks the choice.");
        }
    }

    [Test]
    public async Task APluginMayLeaveTheBehaviorToTheUser()
    {
        var table = await CreateSettingsTableAsync("""
                                                   SETTINGS["DataChatCompression.ContextWindowFullBehavior"] = "SEND_WITHOUT_ASKING"
                                                   SETTINGS["DataChatCompression.ContextWindowFullBehavior.AllowUserOverride"] = true
                                                   """);

        ManagedConfiguration.TryProcessConfiguration(x => x.ChatCompression, x => x.ContextWindowFullBehavior, PLUGIN_ID, table, false);

        ManagedConfiguration.TryGet(x => x.ChatCompression, x => x.ContextWindowFullBehavior, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(this.settingsManager.ConfigurationData.ChatCompression.ContextWindowFullBehavior, Is.EqualTo(ContextWindowFullBehavior.SEND_WITHOUT_ASKING));
            Assert.That(configMeta.IsLocked, Is.False, "With AllowUserOverride, the value is a default of the organization which the user may change.");
        }
    }

    /// <summary>
    /// Lets a configuration plugin set the share of the context window from which AI Studio acts.
    /// </summary>
    private static async Task<bool> ProcessThresholdAsync(string luaValue)
    {
        var table = await CreateSettingsTableAsync($"""SETTINGS["DataChatCompression.ContextWindowThresholdPercent"] = {luaValue}""");
        return ManagedConfiguration.TryProcessConfiguration(x => x.ChatCompression, x => x.ContextWindowThresholdPercent, PLUGIN_ID, table, false, validator: DataChatCompression.IsValidContextWindowThresholdPercent);
    }

    /// <summary>
    /// Lets a configuration plugin set the most tokens of a summary.
    /// </summary>
    private static async Task<bool> ProcessMaxSummaryTokensAsync(string luaValue)
    {
        var table = await CreateSettingsTableAsync($"""SETTINGS["DataChatCompression.MaxSummaryTokens"] = {luaValue}""");
        return ManagedConfiguration.TryProcessConfiguration(x => x.ChatCompression, x => x.MaxSummaryTokens, PLUGIN_ID, table, false, validator: DataChatCompression.IsValidMaxSummaryTokens);
    }

    /// <summary>
    /// Runs the Lua lines of a plugin which fill its table of settings.
    /// </summary>
    /// <param name="settingsLines">The lines which set values in the table SETTINGS.</param>
    /// <returns>The table of settings.</returns>
    private static async Task<LuaTable> CreateSettingsTableAsync(string settingsLines)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();

        await state.DoStringAsync($"""
                                   SETTINGS = {"{}"}
                                   {settingsLines}
                                   """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        return settingsTable;
    }
}