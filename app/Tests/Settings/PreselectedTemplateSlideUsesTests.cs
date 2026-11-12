using AIStudio.Settings;
using AIStudio.Tools.PowerPoint;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what an organization preselects through a configuration plugin for the slides of its template.
/// </summary>
/// <remarks>
/// The setting maps the numbers of the slides to the members of an enum, a shape which ManagedConfiguration
/// reads with an overload of its own. The settings are reached through Program.SERVICE_PROVIDER, as in the
/// app, because a managed setting writes its value and its lock there.
/// </remarks>
[TestFixture]
public sealed class PreselectedTemplateSlideUsesTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("9e4b2d17-6a3c-4f58-b1e0-2c7d8a5f3e91");

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
    public async Task APluginPreselectsTheSlidesItNumbers()
    {
        await ProcessAsync("""{ [1] = "KEEP", [4] = "edit", [7] = "OMIT" }""");

        Assert.That(this.PreselectedUses, Is.EquivalentTo(new Dictionary<int, TemplateSlideUse>
        {
            [1] = TemplateSlideUse.KEEP,
            [4] = TemplateSlideUse.EDIT,
            [7] = TemplateSlideUse.OMIT,
        }), "Options count whatever their case, as everywhere in a configuration.");
    }

    /// <summary>
    /// Checks that an entry which names no slide or no option preselects nothing and leaves the rest of the table intact.
    /// </summary>
    /// <remarks>
    /// Slides count from 1, so 0 and negative numbers name no slide, and a number in quotes is a text in
    /// Lua. "KEEPS" is the typo an administrator would make. None may take the valid entries down with it.
    /// </remarks>
    [Test]
    public async Task AnEntryWhichNamesNoSlideOrNoOptionIsPassedOver()
    {
        await ProcessAsync("""{ [0] = "KEEP", [-2] = "KEEP", ["3"] = "KEEP", [5] = "KEEPS", ["TITLE"] = "EDIT", [2] = "EDIT" }""");

        Assert.That(this.PreselectedUses, Is.EquivalentTo(new Dictionary<int, TemplateSlideUse> { [2] = TemplateSlideUse.EDIT }), "Only the valid entry may count.");
    }

    [Test]
    public async Task ThePreselectionIsLockedWithoutUserOverride()
    {
        await ProcessAsync("""{ [1] = "KEEP" }""");

        var isManaged = ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.PreselectedTemplateSlideUses, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(isManaged, Is.True);
            Assert.That(configMeta.IsLocked, Is.True);
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(PLUGIN_ID));
        }
    }

    [Test]
    public async Task AnEditableDefaultKeepsTheChoicesOfTheUser()
    {
        await ProcessAsync("""{ [1] = "KEEP" }""", allowUserOverride: true);

        var isManaged = ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.PreselectedTemplateSlideUses, out var configMeta);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(isManaged, Is.True);
            Assert.That(configMeta.IsLocked, Is.False, "With AllowUserOverride, users may change the choices.");
            Assert.That(configMeta.EditableDefaultByConfigPluginId, Is.EqualTo(PLUGIN_ID));
            Assert.That(this.PreselectedUses, Is.EquivalentTo(new Dictionary<int, TemplateSlideUse> { [1] = TemplateSlideUse.KEEP }), "The first time, the default of the organization applies.");
        }

        this.PreselectedUses[1] = TemplateSlideUse.EDIT;
        await ProcessAsync("""{ [1] = "KEEP" }""", allowUserOverride: true);

        Assert.That(this.PreselectedUses[1], Is.EqualTo(TemplateSlideUse.EDIT), "Once the user changed the choices, the next start of the plugin must not overwrite them.");
    }

    [Test]
    public async Task RemovingThePluginHandsBackTheChoicesOfTheUser()
    {
        this.PreselectedUses[3] = TemplateSlideUse.EDIT;
        await ProcessAsync("""{ [1] = "KEEP" }""");

        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Assert.That(this.PreselectedUses, Is.EquivalentTo(new Dictionary<int, TemplateSlideUse> { [3] = TemplateSlideUse.EDIT }), "Without its plugin, the choices the user made before apply again.");
    }

    private Dictionary<int, TemplateSlideUse> PreselectedUses => this.settingsManager.ConfigurationData.SlideBuilder.PreselectedTemplateSlideUses;

    /// <summary>
    /// Lets a configuration plugin preselect what the presentation does with the slides of the template.
    /// </summary>
    /// <param name="luaTable">The Lua table of options by number of slide.</param>
    /// <param name="allowUserOverride">Whether users may change the choices.</param>
    private static async Task ProcessAsync(string luaTable, bool allowUserOverride = false)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    SETTINGS = {}
                                    SETTINGS["DataSlideBuilder.PreselectedTemplateSlideUses"] = {{luaTable}}
                                    SETTINGS["DataSlideBuilder.PreselectedTemplateSlideUses.AllowUserOverride"] = {{(allowUserOverride ? "true" : "false")}}
                                    """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        var processed = ManagedConfiguration.TryProcessConfiguration(x => x.SlideBuilder, x => x.PreselectedTemplateSlideUses, PLUGIN_ID, settingsTable, false);
        Assert.That(processed, Is.True, "A table of options by number of slide is a valid configuration of this setting.");
    }
}