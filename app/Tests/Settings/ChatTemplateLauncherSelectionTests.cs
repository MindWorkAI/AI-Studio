using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tests.Tools.ToolCalling;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks who decides the provider and the profile when an assistant chat launcher and the chat
/// template it opens its chat with both name one.
/// </summary>
/// <remarks>
/// The rule is the one for the tools and the data sources: the chat template wins. Whatever the
/// template names and cannot be used must not stop the launch, though, because the author of the
/// launcher could not fix it in the launcher. The launcher then decides as if the template named none.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ChatTemplateLauncherSelectionTests : ToolRegistryTestBase
{
    private const string PLUGIN_NAME = "Mail assistant";
    private const string TEMPLATE_PROFILE_ID = "55555555-5555-5555-5555-555555555555";
    private static readonly Guid LAUNCHER_PROVIDER_ID = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LAUNCHER_PROFILE_ID = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public void TheTemplateWinsOverTheLauncher()
    {
        var templateProvider = ToolCapableProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(templateProvider);
        this.SettingsManager.ConfigurationData.Providers.Add(LauncherProvider());
        var templateProfile = this.AddProfile(TEMPLATE_PROFILE_ID);
        this.AddProfile(LAUNCHER_PROFILE_ID.ToString());

        var template = NewTemplate() with { ProviderId = templateProvider.Id, ProfileId = TEMPLATE_PROFILE_ID };
        var service = this.CreateService();
        var providerResult = service.ResolveProvider(PLUGIN_NAME, template, LAUNCHER_PROVIDER_ID);
        var profileResult = service.ResolveProfile(PLUGIN_NAME, template, LAUNCHER_PROFILE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(providerResult.Provider, Is.EqualTo(templateProvider));
            Assert.That(providerResult.IsExplicit, Is.True);
            Assert.That(profileResult.Profile, Is.EqualTo(templateProfile));
        });
    }

    [Test]
    public void WhatTheTemplateNamesAndIsGoneLeavesTheChoiceToTheLauncher()
    {
        var launcherProvider = LauncherProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(launcherProvider);
        var launcherProfile = this.AddProfile(LAUNCHER_PROFILE_ID.ToString());

        var template = NewTemplate() with { ProviderId = "66666666-6666-6666-6666-666666666666", ProfileId = TEMPLATE_PROFILE_ID };
        var service = this.CreateService();
        var providerResult = service.ResolveProvider(PLUGIN_NAME, template, LAUNCHER_PROVIDER_ID);
        var profileResult = service.ResolveProfile(PLUGIN_NAME, template, LAUNCHER_PROFILE_ID);

        Assert.Multiple(() =>
        {
            Assert.That(providerResult.Provider, Is.EqualTo(launcherProvider));
            Assert.That(providerResult.ErrorMessage, Is.Empty);
            Assert.That(profileResult.Profile, Is.EqualTo(launcherProfile));
            Assert.That(profileResult.ErrorMessage, Is.Empty);
        });
    }

    [Test]
    public void ATemplateProviderBelowTheConfidenceOfTheChatDoesNotStopTheLaunch()
    {
        var templateProvider = LessTrustedProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(templateProvider);
        this.SettingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        this.SettingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var template = NewTemplate() with { ProviderId = templateProvider.Id };
        var providerResult = this.CreateService().ResolveProvider(PLUGIN_NAME, template, null);

        Assert.Multiple(() =>
        {
            Assert.That(providerResult.Provider, Is.Not.EqualTo(templateProvider), "A template must not open the way to a provider the user could not pick by hand.");
            Assert.That(providerResult.IsExplicit, Is.False, "The launcher names no provider, so the chat starts with its defaults instead of failing.");
            Assert.That(providerResult.ErrorMessage, Is.Empty);
        });
    }

    [Test]
    public void ATemplateWhichForbidsProfilesLeavesTheProfileToTheLauncher()
    {
        this.AddProfile(TEMPLATE_PROFILE_ID);
        var launcherProfile = this.AddProfile(LAUNCHER_PROFILE_ID.ToString());

        var template = NewTemplate() with { AllowProfileUsage = false, ProfileId = TEMPLATE_PROFILE_ID };
        var profileResult = this.CreateService().ResolveProfile(PLUGIN_NAME, template, LAUNCHER_PROFILE_ID);

        Assert.That(profileResult.Profile, Is.EqualTo(launcherProfile), "The launch drops the profile afterward for such a template; picking the one of the template here would only hide that.");
    }

    // Data sources are not resolved by these tests, so the service needs none:
    private DirectChatService CreateService() => new(this.SettingsManager, dataSourceService: null!, toolRegistry: this.CreateRegistry(), NullLogger<DirectChatService>.Instance);

    private static AIStudio.Settings.Provider LauncherProvider() => LessTrustedProvider() with { Id = LAUNCHER_PROVIDER_ID.ToString() };

    private Profile AddProfile(string profileId)
    {
        var profile = new Profile((uint)this.SettingsManager.ConfigurationData.Profiles.Count + 1, profileId, $"Profile {profileId[..4]}", "I read a lot of mails.", "Answer briefly.");
        this.SettingsManager.ConfigurationData.Profiles.Add(profile);
        return profile;
    }

    private static ChatTemplate NewTemplate() => new()
    {
        Num = 1,
        Id = "33333333-3333-3333-3333-333333333333",
        Name = "Mail",
        SystemPrompt = "You help with mails.",
        PredefinedUserPrompt = string.Empty,
        ExampleConversation = [],
        FileAttachments = [],
        AllowProfileUsage = true,
    };
}