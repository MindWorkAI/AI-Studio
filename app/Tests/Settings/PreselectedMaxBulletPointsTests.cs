using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what an organization preselects through a configuration plugin for the maximum number of bullet points per slide.
/// </summary>
/// <remarks>
/// The settings are reached through Program.SERVICE_PROVIDER, as in the app, because a managed setting
/// writes its value and its lock there. The plugin passes the range check of DataSlideBuilder, as
/// PluginConfiguration does.
/// </remarks>
[TestFixture]
public sealed class PreselectedMaxBulletPointsTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("4c81e2a6-0f3b-4d97-a52e-7b19c6d3f084");

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
    public async Task APluginPreselectsTheMaximum()
    {
        var processed = await ProcessAsync("5");

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(this.PreselectedMaximum, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task NoMaximumIsAValidPreselection()
    {
        this.PreselectedMaximum = 3;
        var processed = await ProcessAsync("0");

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True, "An organization may decide on no maximum at all.");
            Assert.That(this.PreselectedMaximum, Is.Zero);
        });
    }

    [TestCase("9")]
    [TestCase("-1")]
    public async Task AMaximumOutsideTheRangeIsRejected(string configuredMaximum)
    {
        this.PreselectedMaximum = 3;
        var processed = await ProcessAsync(configuredMaximum);

        ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.PreselectedMaxBulletPointsPerSlide, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(this.PreselectedMaximum, Is.EqualTo(3), "A rejected value leaves the choice of the user untouched.");
            Assert.That(configMeta.IsLocked, Is.False, "A rejected value locks nothing.");
        });
    }

    [Test]
    public async Task ThePreselectionIsLockedWithoutUserOverride()
    {
        await ProcessAsync("5");

        var isManaged = ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.PreselectedMaxBulletPointsPerSlide, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(isManaged, Is.True);
            Assert.That(configMeta.IsLocked, Is.True);
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(PLUGIN_ID));
        });
    }

    private int PreselectedMaximum
    {
        get => this.settingsManager.ConfigurationData.SlideBuilder.PreselectedMaxBulletPointsPerSlide;
        set => this.settingsManager.ConfigurationData.SlideBuilder.PreselectedMaxBulletPointsPerSlide = value;
    }

    /// <summary>
    /// Lets a configuration plugin preselect the maximum number of bullet points per slide.
    /// </summary>
    /// <param name="luaValue">The maximum as the plugin writes it in Lua.</param>
    /// <returns>Whether the plugin's value was taken.</returns>
    private static async Task<bool> ProcessAsync(string luaValue)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();

        await state.DoStringAsync($"""
                                   SETTINGS = {"{}"}
                                   SETTINGS["DataSlideBuilder.PreselectedMaxBulletPointsPerSlide"] = {luaValue}
                                   """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        return ManagedConfiguration.TryProcessConfiguration(x => x.SlideBuilder, x => x.PreselectedMaxBulletPointsPerSlide, PLUGIN_ID, settingsTable, false, validator: DataSlideBuilder.IsValidMaxBulletPointsPerSlide);
    }
}