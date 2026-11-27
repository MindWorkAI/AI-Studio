using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks how the categories users collapse or hide on the assistants page affect their assistants.
/// </summary>
/// <remarks>
/// Collapsing a category is about a tidy page, so its assistants stay in the "Send to ..." menu by default.
/// Hiding one says that its assistants do not matter, so they disappear there as well by default. Two
/// options change both, and a hidden category follows its option even when it is collapsed, too.
/// </remarks>
[TestFixture]
public sealed class AssistantCategoryPageSettingsTests
{
    private static readonly AssistantCategoryKey BUSINESS = new(PluginCategory.BUSINESS);
    private static readonly AssistantCategoryKey SOFTWARE_DEVELOPMENT = new(PluginCategory.SOFTWARE_DEVELOPMENT);

    [Test]
    public void TheDefaultsKeepCollapsedCategoriesAndDropHiddenOnes()
    {
        var page = new DataAssistantsPage();
        page.SetCategoryCollapsed(BUSINESS, true);
        page.SetCategoryHidden(SOFTWARE_DEVELOPMENT, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.IsCategoryOffering(BUSINESS), Is.True);
            Assert.That(page.IsCategoryOffering(SOFTWARE_DEVELOPMENT), Is.False);
            Assert.That(page.IsCategoryOffering(new AssistantCategoryKey(PluginCategory.EDUCATION)), Is.True, "A category the user left alone offers its assistants.");
        }
    }

    [Test]
    public void TheOptionsTurnBothAround()
    {
        var page = new DataAssistantsPage
        {
            CollapsedCategoryAssistants = AssistantOfferBehavior.TREAT_AS_ABSENT,
            HiddenCategoryAssistants = AssistantOfferBehavior.KEEP_OFFERING,
        };

        page.SetCategoryCollapsed(BUSINESS, true);
        page.SetCategoryHidden(SOFTWARE_DEVELOPMENT, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.IsCategoryOffering(BUSINESS), Is.False);
            Assert.That(page.IsCategoryOffering(SOFTWARE_DEVELOPMENT), Is.True);
        }
    }

    [Test]
    public void AHiddenCategoryFollowsTheOptionForHiddenOnesAlsoWhenItIsCollapsed()
    {
        var page = new DataAssistantsPage();
        page.SetCategoryCollapsed(BUSINESS, true);
        page.SetCategoryHidden(BUSINESS, true);

        Assert.That(page.IsCategoryOffering(BUSINESS), Is.False);
    }

    [Test]
    public void TheDefaultsOfTheSettingsAreTheOnesOfThePlan()
    {
        var page = new Data().AssistantsPage;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.CollapsedCategoryAssistants, Is.EqualTo(AssistantOfferBehavior.KEEP_OFFERING));
            Assert.That(page.HiddenCategoryAssistants, Is.EqualTo(AssistantOfferBehavior.TREAT_AS_ABSENT));
            Assert.That(page.InputsWhenLeaving, Is.EqualTo(AssistantInputsWhenLeaving.KEEP_INPUTS));
        }
    }

    [Test]
    public void CoreIsStoredAsCommon()
    {
        var page = new DataAssistantsPage();
        page.SetCategoryHidden(new AssistantCategoryKey(PluginCategory.CORE), true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.HiddenCategories, Is.EquivalentTo(new[] { PluginCategory.COMMON }));
            Assert.That(page.IsCategoryHidden(new AssistantCategoryKey(PluginCategory.COMMON)), Is.True);
        }
    }

    [Test]
    public void CustomCategoriesAreComparedWithoutRegardToCase()
    {
        var page = new DataAssistantsPage();
        page.SetCategoryCollapsed(new AssistantCategoryKey(PluginCategory.CUSTOM, "Space Travel"), true);
        page.SetCategoryCollapsed(new AssistantCategoryKey(PluginCategory.CUSTOM, "space travel"), true);
        var isCollapsedInAnotherCase = page.IsCategoryCollapsed(new AssistantCategoryKey(PluginCategory.CUSTOM, "SPACE TRAVEL"));

        page.SetCategoryCollapsed(new AssistantCategoryKey(PluginCategory.CUSTOM, "SPACE TRAVEL"), false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isCollapsedInAnotherCase, Is.True);
            Assert.That(page.CollapsedCustomCategories, Is.Empty, "Expanding a category removes every spelling of it.");
        }
    }
}