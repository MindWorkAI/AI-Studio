using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks that the BeyondFacts Lab stays a prototype which nobody sees unless they ask for it.
/// </summary>
/// <remarks>
/// The lab grows over a long series of pull requests. Until it is complete, only people who allow
/// prototypes and switch the lab on may find it; raising its level is a decision of its own.
/// </remarks>
[TestFixture]
public sealed class BflPreviewFeatureTests
{
    [TestCase(PreviewVisibility.PROTOTYPE)]
    [TestCase(PreviewVisibility.EXPERIMENTAL)]
    public void PrototypesOfferTheLab(PreviewVisibility visibility)
    {
        Assert.That(visibility.GetPreviewFeatures(), Does.Contain(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026));
    }

    [TestCase(PreviewVisibility.NONE)]
    [TestCase(PreviewVisibility.RELEASE_CANDIDATE)]
    [TestCase(PreviewVisibility.BETA)]
    [TestCase(PreviewVisibility.ALPHA)]
    public void LowerLevelsDoNotOfferTheLab(PreviewVisibility visibility)
    {
        Assert.That(visibility.GetPreviewFeatures(), Does.Not.Contain(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026));
    }

    [Test]
    public void LoweringTheLevelLeavesTheLabOut()
    {
        var enabled = new HashSet<PreviewFeatures> { PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026 };

        Assert.That(PreviewVisibility.BETA.FilterPreviewFeatures(enabled), Is.Empty, "A beta level must not keep a prototype switched on.");
    }

    [Test]
    public void TheLabIsNotReleased()
    {
        Assert.That(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026.IsReleased, Is.False);
    }

    [Test]
    public void TheLabCountsOnlyWhenSwitchedOn()
    {
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);
        var wasEnabledBefore = PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026.IsEnabled(settingsManager);

        settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(wasEnabledBefore, Is.False, "New settings must not switch the lab on.");
            Assert.That(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026.IsEnabled(settingsManager), Is.True);
        }
    }

    [Test]
    public void TheLabHasADescription()
    {
        Assert.That(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026.PreviewDescription, Does.StartWith("BeyondFacts Lab"), "The settings list the preview features by this text.");
    }
}