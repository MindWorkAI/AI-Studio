using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks how the BeyondFacts Lab is registered as a component: who may see it, which providers it accepts,
/// and what it shares with the rest of the app.
/// </summary>
/// <remarks>
/// The component is written out in full, AIStudio.Tools.Components: from this namespace, the short name finds
/// the test namespace AIStudio.Tests.Components first.
/// </remarks>
[TestFixture]
public sealed class BflComponentTests
{
    private const AIStudio.Tools.Components LAB = AIStudio.Tools.Components.BEYOND_FACTS_LAB_ASSISTANT;

    [Test]
    public void TheLabBelongsToItsPreviewFeature()
    {
        Assert.That(LAB.RequiredPreviewFeature, Is.EqualTo(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026), "The tile, the page, and the menus all ask the component for its preview feature.");
    }

    [Test]
    public void TheLabIsHiddenWithoutItsPreviewFeature()
    {
        var settingsManager = CreateSettingsManager();

        Assert.That(settingsManager.IsAssistantVisible(LAB, requiredPreviewFeature: LAB.RequiredPreviewFeature), Is.False);
    }

    [Test]
    public void TheLabIsVisibleWithItsPreviewFeature()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026);

        Assert.That(settingsManager.IsAssistantVisible(LAB, requiredPreviewFeature: LAB.RequiredPreviewFeature), Is.True);
    }

    [Test]
    public void AnOrganizationCanHideTheLab()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_BEYOND_FACTS_LAB_2026);
        settingsManager.ConfigurationData.App.HiddenAssistants.Add(ConfigurableAssistant.BEYOND_FACTS_LAB_ASSISTANT);

        Assert.That(settingsManager.IsAssistantVisible(LAB, requiredPreviewFeature: LAB.RequiredPreviewFeature), Is.False, "The list of hidden assistants must reach the lab, too.");
    }

    [TestCase(ConfidenceLevel.NONE)]
    [TestCase(ConfidenceLevel.LOW)]
    [TestCase(ConfidenceLevel.MODERATE)]
    [TestCase(ConfidenceLevel.MEDIUM)]
    public void TheMinimumConfidenceNeverDropsBelowMedium(ConfidenceLevel configured)
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.BeyondFactsLab.MinimumProviderConfidence = configured;

        Assert.That(LAB.MinimumConfidence(settingsManager), Is.EqualTo(ConfidenceLevel.MEDIUM));
    }

    [Test]
    public void AHigherMinimumConfidenceWins()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.BeyondFactsLab.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        Assert.That(LAB.MinimumConfidence(settingsManager), Is.EqualTo(ConfidenceLevel.HIGH));
    }

    /// <summary>
    /// Checks that the lab takes the cloud providers of the region one trusts and refuses all others.
    /// </summary>
    /// <remarks>
    /// The scheme which trusts Europe gives MEDIUM to Mistral and LOW to OpenAI. MEDIUM is the floor of the
    /// lab, so only the first one may see unpublished research.
    /// </remarks>
    [Test]
    public void OnlyProvidersOfTheTrustedRegionAreAccepted()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.Confidence.ConfidenceScheme = ConfidenceSchemes.TRUST_EUROPE;
        var trusted = CreateProvider(LLMProviders.MISTRAL);
        var untrusted = CreateProvider(LLMProviders.OPEN_AI);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settingsManager.IsProviderConfident(trusted, LAB), Is.True);
            Assert.That(settingsManager.IsProviderConfident(untrusted, LAB), Is.False);
        }
    }

    [Test]
    public void AProhibitedProviderIsRefusedAlthoughTrusted()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.Confidence.ConfidenceScheme = ConfidenceSchemes.TRUST_EUROPE;
        settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.MISTRAL);

        Assert.That(settingsManager.IsProviderConfident(CreateProvider(LLMProviders.MISTRAL), LAB), Is.False);
    }

    [Test]
    public void TheLabNeitherSendsNorReceivesContent()
    {
        Assert.That(LAB.AllowSendTo, Is.False, "Nothing may enter the lab from other places, and its answers stay in its steps.");
    }

    [Test]
    public void TheLabKeepsOneSessionPerStep()
    {
        Assert.That(LAB.HasSingleSessionSlot, Is.False, "A running step must not block the steps of other preparations.");
    }

    [Test]
    public void TheLabWorksWithoutAProfile()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.App.PreselectedProfile = Guid.NewGuid().ToString();

        Assert.That(LAB.GetProfilePreselection(settingsManager), Is.EqualTo(ProfilePreselection.FromStoredValue(Profile.NO_PROFILE.Id)), "The lab works with what the person enters per preparation, never with the app-wide profile.");
    }

    [Test]
    public void TheLabStartsWithItsPreselectedProvider()
    {
        var settingsManager = CreateSettingsManager();
        var provider = CreateProvider(LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.Providers.Add(provider);
        settingsManager.ConfigurationData.BeyondFactsLab.PreselectedProvider = provider.Id;

        Assert.That(LAB.PreselectedProvider(settingsManager).Id, Is.EqualTo(provider.Id));
    }

    [Test]
    public void NewSettingsAddressFormally()
    {
        Assert.That(new DataBeyondFactsLab().AddressForm, Is.EqualTo(BflAddressForm.FORMAL), "The texts of AI Studio address the person formally as well.");
    }

    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    private static AIStudio.Settings.Provider CreateProvider(LLMProviders llmProvider) => new(1, Guid.NewGuid().ToString(), "Test", llmProvider, new("test-model", null));
}