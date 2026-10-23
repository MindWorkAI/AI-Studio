using AIStudio.Settings;
using AIStudio.Tools.PowerPoint;

using Lua;
using Lua.Standard;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks the layouts for kinds of slides which an organization preselects through a configuration plugin.
/// </summary>
/// <remarks>
/// The setting maps the members of an enum to texts, a shape which ManagedConfiguration reads with an
/// overload of its own. The settings are reached through Program.SERVICE_PROVIDER, as in the app,
/// because a managed setting writes its value and its lock there.
/// </remarks>
[TestFixture]
public sealed class PreselectedLayoutsTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("5c2e8a71-3f4d-4b6e-9d0a-8e1f7b3c6a52");

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
    public async Task APluginPreselectsTheLayoutsItNames()
    {
        await ProcessAsync("""{ ["CHAPTER"] = "Section Header", ["content"] = "Title and Content" }""");

        Assert.That(this.PreselectedLayouts, Is.EquivalentTo(new Dictionary<LayoutPurpose, string>
        {
            [LayoutPurpose.CHAPTER] = "Section Header",
            [LayoutPurpose.CONTENT] = "Title and Content",
        }), "Kinds of slides count whatever their case, as everywhere in a configuration, and the names of the layouts stay as written.");
    }

    /// <summary>
    /// Checks that a kind of slide which does not exist preselects nothing and leaves the rest of the table intact.
    /// </summary>
    /// <remarks>
    /// "CHAPTERS" is the typo an administrator would make, and a newer plugin might name a kind of slide
    /// which this version does not know yet. Neither may take the valid entries down with it.
    /// </remarks>
    [Test]
    public async Task AnUnknownKindOfSlideIsPassedOver()
    {
        await ProcessAsync("""{ ["CHAPTERS"] = "Section Header", ["TITLE"] = "Title Slide", [3] = "Two Content" }""");

        Assert.That(this.PreselectedLayouts, Is.EquivalentTo(new Dictionary<LayoutPurpose, string> { [LayoutPurpose.TITLE] = "Title Slide" }), "Only the valid kind of slide may count, and a number is no kind of slide.");
    }

    [Test]
    public async Task ThePreselectionIsLockedWithoutUserOverride()
    {
        await ProcessAsync("""{ ["CHAPTER"] = "Section Header" }""");

        var isManaged = ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.PreselectedLayouts, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(isManaged, Is.True);
            Assert.That(configMeta.IsLocked, Is.True);
            Assert.That(configMeta.LockedByConfigPluginId, Is.EqualTo(PLUGIN_ID));
        });
    }

    [Test]
    public async Task AnEditableDefaultKeepsTheChoicesOfTheUser()
    {
        await ProcessAsync("""{ ["CHAPTER"] = "Section Header" }""", allowUserOverride: true);

        var isManaged = ManagedConfiguration.TryGet(x => x.SlideBuilder, x => x.PreselectedLayouts, out var configMeta);
        Assert.Multiple(() =>
        {
            Assert.That(isManaged, Is.True);
            Assert.That(configMeta.IsLocked, Is.False, "With AllowUserOverride, users may change the layouts.");
            Assert.That(configMeta.EditableDefaultByConfigPluginId, Is.EqualTo(PLUGIN_ID));
            Assert.That(this.PreselectedLayouts, Is.EquivalentTo(new Dictionary<LayoutPurpose, string> { [LayoutPurpose.CHAPTER] = "Section Header" }), "The first time, the default of the organization applies.");
        });

        this.PreselectedLayouts[LayoutPurpose.CHAPTER] = "Chapter";
        await ProcessAsync("""{ ["CHAPTER"] = "Section Header" }""", allowUserOverride: true);

        Assert.That(this.PreselectedLayouts[LayoutPurpose.CHAPTER], Is.EqualTo("Chapter"), "Once the user changed the layouts, the next start of the plugin must not overwrite them.");
    }

    [Test]
    public async Task RemovingThePluginHandsBackTheLayoutsOfTheUser()
    {
        this.PreselectedLayouts[LayoutPurpose.CONTENT] = "Content";
        await ProcessAsync("""{ ["CHAPTER"] = "Section Header" }""");

        ManagedConfiguration.CleanupLeftOverManagedConfigurations([], new HashSet<Guid>());

        Assert.That(this.PreselectedLayouts, Is.EquivalentTo(new Dictionary<LayoutPurpose, string> { [LayoutPurpose.CONTENT] = "Content" }), "Without its plugin, the layouts the user chose before apply again.");
    }

    private Dictionary<LayoutPurpose, string> PreselectedLayouts => this.settingsManager.ConfigurationData.SlideBuilder.PreselectedLayouts;

    /// <summary>
    /// Lets a configuration plugin set the preselected layouts.
    /// </summary>
    /// <param name="luaTable">The Lua table of layout names by kind of slide.</param>
    /// <param name="allowUserOverride">Whether users may change the layouts.</param>
    private static async Task ProcessAsync(string luaTable, bool allowUserOverride = false)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    SETTINGS = {}
                                    SETTINGS["DataSlideBuilder.PreselectedLayouts"] = {{luaTable}}
                                    SETTINGS["DataSlideBuilder.PreselectedLayouts.AllowUserOverride"] = {{(allowUserOverride ? "true" : "false")}}
                                    """);

        if (!state.Environment["SETTINGS"].TryRead<LuaTable>(out var settingsTable))
            throw new InvalidOperationException("The settings of this test are not a Lua table.");

        var processed = ManagedConfiguration.TryProcessConfiguration(x => x.SlideBuilder, x => x.PreselectedLayouts, PLUGIN_ID, settingsTable, false);
        Assert.That(processed, Is.True, "A table of layout names by kind of slide is a valid configuration of this setting.");
    }
}