using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.AssistantLaunch;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

using Microsoft.Extensions.Logging.Abstractions;

using ComponentKind = AIStudio.Tools.Components;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks how the categories your organization disabled keep assistants away.
/// </summary>
/// <remarks>
/// Organizations disable categories such as entertainment on their devices. A built-in assistant has one
/// category, a plugin may have several and stays available while one of them is free. Plugins the
/// organization deployed itself are exempt, but only with a proof: a plugin from the internet may declare
/// whatever it likes about itself.
/// </remarks>
[TestFixture]
public sealed class AssistantCategoryRulesTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("4c8e2a6f-9d1b-4f3a-8e5c-7b2d4f6a8c0e");

    private string pluginDirectory = string.Empty;

    [SetUp]
    public void CreatePluginDirectory()
    {
        this.pluginDirectory = Path.Combine(Path.GetTempPath(), $"assistant-category-rules-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.pluginDirectory);
    }

    [TearDown]
    public void DeletePluginDirectory()
    {
        if (Directory.Exists(this.pluginDirectory))
            Directory.Delete(this.pluginDirectory, recursive: true);
    }

    [Test]
    public void APluginOfADisabledCategoryIsDisabled()
    {
        var app = new DataApp { DisabledAssistantCategories = [PluginCategory.ENTERTAINMENT] };

        Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Quiz", [PluginCategory.ENTERTAINMENT]), isDeployedByOrganization: false), Is.True);
    }

    [Test]
    public void APluginStaysAvailableWhileOneOfItsCategoriesIsFree()
    {
        var app = new DataApp { DisabledAssistantCategories = [PluginCategory.ENTERTAINMENT] };

        Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Team quiz", [PluginCategory.ENTERTAINMENT, PluginCategory.BUSINESS]), isDeployedByOrganization: false), Is.False);
    }

    [Test]
    public void APluginYourOrganizationDeployedIsNeverDisabled()
    {
        var app = new DataApp { DisabledAssistantCategories = [PluginCategory.ENTERTAINMENT] };

        Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Quiz", [PluginCategory.ENTERTAINMENT]), isDeployedByOrganization: true), Is.False);
    }

    [TestCase(PluginCategory.CORE, PluginCategory.COMMON)]
    [TestCase(PluginCategory.COMMON, PluginCategory.CORE)]
    public void CoreAndCommonAreOneCategory(PluginCategory disabledCategory, PluginCategory categoryOfThePlugin)
    {
        var app = new DataApp { DisabledAssistantCategories = [disabledCategory] };

        Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Letter writer", [categoryOfThePlugin]), isDeployedByOrganization: false), Is.True, "Both share the heading \"General\", so disabling one disables the other.");
    }

    [Test]
    public void CustomCategoriesAreComparedWithoutRegardToCase()
    {
        var app = new DataApp { DisabledCustomAssistantCategories = ["fun & games "] };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Riddles", [PluginCategory.CUSTOM], ["Fun & Games"]), isDeployedByOrganization: false), Is.True);
            Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Orbit planner", [PluginCategory.CUSTOM], ["Space Travel"]), isDeployedByOrganization: false), Is.False);
        }
    }

    [Test]
    public void DisablingCustomDisablesEveryCustomCategory()
    {
        var app = new DataApp { DisabledAssistantCategories = [PluginCategory.CUSTOM] };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Riddles", [PluginCategory.CUSTOM], ["Fun & Games", "Space Travel"]), isDeployedByOrganization: false), Is.True);
            Assert.That(app.DisablesAssistantPlugin(new FakeAssistantPlugin("Orbit planner", [PluginCategory.CUSTOM, PluginCategory.SCIENCE], ["Space Travel"]), isDeployedByOrganization: false), Is.False);
        }
    }

    [Test]
    public void ABuiltInAssistantOfADisabledCategoryIsGoneEverywhere()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settingsManager.ConfigurationData.App.DisabledAssistantCategories.Add(PluginCategory.SOFTWARE_DEVELOPMENT);

        var destinations = AssistantLaunchService.SelectDestinations(ComponentKind.CHAT, component => settingsManager.IsAssistantVisible(component, withLogging: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.IsAssistantVisible(ComponentKind.CODING_ASSISTANT, withLogging: false), Is.False);
            Assert.That(settingsManager.IsAssistantVisible(ComponentKind.ERI_ASSISTANT, withLogging: false), Is.False);
            Assert.That(settingsManager.IsAssistantVisible(ComponentKind.EMAIL_ASSISTANT, withLogging: false), Is.True);
            Assert.That(destinations, Does.Not.Contain(ComponentKind.CODING_ASSISTANT), "An assistant of a disabled category must not be offered in \"Send to …\" either.");
            Assert.That(destinations, Does.Contain(ComponentKind.EMAIL_ASSISTANT));
        }
    }

    [TestCase(true, true, ExpectedResult = true, TestName = "TheDeclarationWithAnApprovalOfItsCodeIsAProof")]
    [TestCase(true, false, ExpectedResult = false, TestName = "TheDeclarationAloneIsNoProof")]
    [TestCase(false, true, ExpectedResult = false, TestName = "AnApprovalWithoutTheDeclarationIsNoProof")]
    public async Task<bool> APluginOutsideTheDirectoryOfYourOrganizationNeedsAProof(bool declaresDeployment, bool isApproved)
    {
        using var plugin = await this.LoadLauncherAsync([PluginCategory.ENTERTAINMENT], declaresDeployment);
        DataAssistantPluginEnterpriseApproval[] approvals = isApproved ? [ApprovalOf(plugin)] : [];

        return PluginFactory.IsAssistantDeployedByOrganization(plugin, approvals);
    }

    [Test]
    public async Task APluginYourOrganizationRolledOutStaysAvailableInADisabledCategory()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        settingsManager.ConfigurationData.App.DisabledAssistantCategories.Add(PluginCategory.ENTERTAINMENT);
        using var plugin = await this.LoadLauncherAsync([PluginCategory.ENTERTAINMENT], declaresDeployment: true);

        var isDisabledWithoutApproval = settingsManager.IsDisabledByItsCategories(plugin);
        settingsManager.ConfigurationData.AssistantPluginAudit.EnterpriseApprovedPlugins.Add(ApprovalOf(plugin));
        var isDisabledWithApproval = settingsManager.IsDisabledByItsCategories(plugin);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isDisabledWithoutApproval, Is.True);
            Assert.That(isDisabledWithApproval, Is.False);
        }
    }

    /// <summary>
    /// Approves exactly the code of a plugin, spelled in lowercase, as an organization may write the hash.
    /// </summary>
    private static DataAssistantPluginEnterpriseApproval ApprovalOf(PluginAssistants plugin) => new() { PluginHash = plugin.ComputeAuditHash().ToLowerInvariant() };

    /// <summary>
    /// Writes a direct chat launcher into the plugin directory of the test and loads it from there, so that
    /// its code has a hash.
    /// </summary>
    private async Task<PluginAssistants> LoadLauncherAsync(PluginCategory[] categories, bool declaresDeployment)
    {
        var metadata = new DirectChatLauncherPluginMetadata(PLUGIN_ID, "1.0.0", null, ["MindWork AI"], "MindWork AI", "https://github.com/MindWorkAI/AI-Studio",
            categories, [], [PluginTargetGroup.EVERYONE], IsMaintained: true, DeprecationMessage: string.Empty, IsAssistantBuilderGenerated: false);

        var definition = new DirectChatLauncherDefinition("Launcher for the tests", "Launcher for the tests", "Opens a disappearing chat.", new AssistantChatLaunchConfiguration(string.Empty, null, null, null, null, null));
        var lua = DirectChatLauncherLuaWriter.Write(metadata, definition);
        if (declaresDeployment)
            lua = lua.Replace("DEPLOYED_USING_CONFIG_SERVER = false", "DEPLOYED_USING_CONFIG_SERVER = true", StringComparison.Ordinal);

        await File.WriteAllTextAsync(Path.Combine(this.pluginDirectory, "plugin.lua"), lua);
        var plugin = await PluginFactory.Load(this.pluginDirectory, lua);
        Assert.That(plugin, Is.InstanceOf<PluginAssistants>(), string.Join(" ", plugin.Issues));

        var assistantPlugin = (PluginAssistants)plugin;
        assistantPlugin.PluginPath = this.pluginDirectory;
        Assert.That(assistantPlugin.IsManagedByConfigServer, Is.EqualTo(declaresDeployment), "The test needs the declaration it asked for.");
        return assistantPlugin;
    }
}