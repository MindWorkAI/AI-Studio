using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks who may open the page of the BeyondFacts Lab and what its settings offer.
/// </summary>
/// <remarks>
/// The page asks by itself whether it may be opened, because its address can be reached without the tile.
/// </remarks>
[TestFixture]
public sealed class BflAccessTests
{
    [Test]
    public void ThePageStaysClosedWithoutThePreviewFeature()
    {
        Assert.That(BflAccess.CanOpen(CreateSettingsManager()), Is.False);
    }

    [Test]
    public void ThePageOpensWithThePreviewFeature()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026);

        Assert.That(BflAccess.CanOpen(settingsManager), Is.True);
    }

    [Test]
    public void ThePageStaysClosedWhenAnOrganizationHidesTheLab()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026);
        settingsManager.ConfigurationData.App.HiddenAssistants.Add(ConfigurableAssistant.BEYOND_FACTS_LAB_ASSISTANT);

        Assert.That(BflAccess.CanOpen(settingsManager), Is.False);
    }

    [Test]
    public void TheSettingsOfferNoConfidenceBelowMedium()
    {
        var levels = ConfigurationSelectDataFactory.GetBeyondFactsLabConfidenceLevelsData(CreateSettingsManager()).Select(data => data.Value);

        Assert.That(levels, Is.EqualTo(new[] { ConfidenceLevel.MEDIUM, ConfidenceLevel.HIGH }));
    }

    [Test]
    public void AnEnforcedGlobalMinimumLiftsTheChoice()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var levels = ConfigurationSelectDataFactory.GetBeyondFactsLabConfidenceLevelsData(settingsManager).Select(data => data.Value);

        Assert.That(levels, Is.EqualTo(new[] { ConfidenceLevel.HIGH }));
    }

    [Test]
    public void TheSettingsOfferEveryFormOfAddress()
    {
        var forms = ConfigurationSelectDataFactory.GetBeyondFactsLabAddressFormData().Select(data => data.Value);

        Assert.That(forms, Is.EquivalentTo(Enum.GetValues<AddressForm>()));
    }

    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);
}