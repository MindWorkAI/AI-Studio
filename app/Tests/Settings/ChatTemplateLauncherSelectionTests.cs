using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
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
/// The last tests cover the data sources, which only stop a launch while the chat can use them.
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(providerResult.Provider, Is.EqualTo(templateProvider));
            Assert.That(providerResult.IsExplicit, Is.True);
            Assert.That(profileResult.Profile, Is.EqualTo(templateProfile));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(providerResult.Provider, Is.EqualTo(launcherProvider));
            Assert.That(providerResult.ErrorMessage, Is.Empty);
            Assert.That(profileResult.Profile, Is.EqualTo(launcherProfile));
            Assert.That(profileResult.ErrorMessage, Is.Empty);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(providerResult.Provider, Is.Not.EqualTo(templateProvider), "A template must not open the way to a provider the user could not pick by hand.");
            Assert.That(providerResult.IsExplicit, Is.False, "The launcher names no provider, so the chat starts with its defaults instead of failing.");
            Assert.That(providerResult.ErrorMessage, Is.Empty);
        }
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

    [Test]
    public async Task WithoutRagAMissingDataSourceDoesNotStopTheLaunch()
    {
        var options = new DataSourceOptions { DisableDataSources = false, PreselectedDataSourceIds = ["99999999-9999-9999-9999-999999999999"] };
        var result = await this.CreateService().CheckChosenDataSourcesAsync(ToolCapableProvider(), options, originChatTemplate: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Options, Is.SameAs(options), "The chat uses no data sources without RAG, so the options travel along unchecked and count once RAG is back.");
            Assert.That(result.ErrorMessage, Is.Empty);
        }
    }

    [Test]
    public async Task WithRagAMissingDataSourceStillStopsTheLaunch()
    {
        this.SettingsManager.ConfigurationData.App.EnabledPreviewFeatures.Add(PreviewFeatures.PRE_RAG_2024);
        var options = new DataSourceOptions { DisableDataSources = false, PreselectedDataSourceIds = ["99999999-9999-9999-9999-999999999999"] };
        var result = await this.CreateService().CheckChosenDataSourcesAsync(ToolCapableProvider(), options, originChatTemplate: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Options, Is.Null, "Opening a launcher is one click, so a missing source has to be said out loud.");
            Assert.That(result.ErrorMessage, Is.Not.Empty);
        }
    }

    // Data sources are not resolved by these tests beyond their lookup, so the service needs no data source service:
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