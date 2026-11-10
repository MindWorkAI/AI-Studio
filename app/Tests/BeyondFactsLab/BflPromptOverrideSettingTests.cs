using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Settings;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks that an organization can keep the prompts of the BeyondFacts Lab as they come with AI Studio.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, as in the app, because a managed setting writes its
/// value and its lock there.
/// </remarks>
[TestFixture]
public sealed class BflPromptOverrideSettingTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("4f0c2b7e-8d31-4a96-b5e2-7c1d9a3e6f08");

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
    public async Task AnOrganizationCanSwitchOffThePromptOverridesForGood()
    {
        this.settingsManager.ConfigurationData.BeyondFactsLab.AllowPromptOverrides = true;

        var state = LuaState.Create();
        state.OpenBasicLibrary();
        await state.DoStringAsync("""
                                  SETTINGS = {}
                                  SETTINGS["DataBeyondFactsLab.AllowPromptOverrides"] = false
                                  """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        var processed = ManagedConfiguration.TryProcessConfiguration(x => x.BeyondFactsLab, x => x.AllowPromptOverrides, PLUGIN_ID, settingsTable, false);
        ManagedConfiguration.TryGet(x => x.BeyondFactsLab, x => x.AllowPromptOverrides, out var configMeta);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(processed, Is.True);
            Assert.That(configMeta.IsLocked, Is.True);
            Assert.That(BflPromptOverrides.ActiveDirectory(this.settingsManager.ConfigurationData.BeyondFactsLab, "prompts"), Is.Null);
        }
    }
}