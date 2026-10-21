using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.ToolCalling;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which profile and provider of a chat template a chat actually starts with.
/// </summary>
/// <remarks>
/// A preselection must never get past what the user would be allowed to pick by hand: a profile the
/// template forbids, or a provider below the confidence level of the chat. And it must never stop a
/// chat either: whatever the template names and cannot be used leaves the chat to start as if the
/// template said nothing.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ChatTemplateStartSelectionTests : ToolRegistryTestBase
{
    private const string PROFILE_ID = "55555555-5555-5555-5555-555555555555";

    [Test]
    public void ATemplateWhichSaysNothingLeavesBothInPlace()
    {
        this.AddProfile();
        this.SettingsManager.ConfigurationData.Providers.Add(ToolCapableProvider());

        Assert.Multiple(() =>
        {
            Assert.That(this.SettingsManager.GetChatTemplateProfile(NewTemplate()), Is.Null);
            Assert.That(this.SettingsManager.GetChatTemplateProvider(NewTemplate()), Is.EqualTo(AIStudio.Settings.Provider.NONE));
        });
    }

    [Test]
    public void ATemplateHandsOutWhatItNames()
    {
        var profile = this.AddProfile();
        var provider = ToolCapableProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(provider);
        var template = NewTemplate() with { ProfileId = PROFILE_ID, ProviderId = provider.Id };

        Assert.Multiple(() =>
        {
            Assert.That(this.SettingsManager.GetChatTemplateProfile(template), Is.EqualTo(profile));
            Assert.That(this.SettingsManager.GetChatTemplateProvider(template), Is.EqualTo(provider));
        });
    }

    [Test]
    public void StartingWithoutAProfileIsAnAnswerOfItsOwn()
    {
        var template = NewTemplate() with { ProfileId = Profile.NO_PROFILE.Id };

        Assert.That(this.SettingsManager.GetChatTemplateProfile(template), Is.EqualTo(Profile.NO_PROFILE), "Null would keep the default profile, which is exactly what this template rules out.");
    }

    [Test]
    public void WhatIsGoneLeavesTheChatAsIfTheTemplateSaidNothing()
    {
        var template = NewTemplate() with { ProfileId = PROFILE_ID, ProviderId = "66666666-6666-6666-6666-666666666666" };

        Assert.Multiple(() =>
        {
            Assert.That(this.SettingsManager.GetChatTemplateProfile(template), Is.Null);
            Assert.That(this.SettingsManager.GetChatTemplateProvider(template), Is.EqualTo(AIStudio.Settings.Provider.NONE));
        });
    }

    [Test]
    public void ATemplateWhichForbidsProfilesHandsOutNone()
    {
        this.AddProfile();
        var template = NewTemplate() with { AllowProfileUsage = false, ProfileId = PROFILE_ID };

        Assert.That(this.SettingsManager.GetChatTemplateProfile(template), Is.Null, "The chat locks the profile selection for such a template, so a profile handed out here could neither be seen nor changed.");
    }

    [Test]
    public void AProviderBelowTheConfidenceOfTheChatIsNotHandedOut()
    {
        var provider = LessTrustedProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(provider);
        this.SettingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        this.SettingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var template = NewTemplate() with { ProviderId = provider.Id };

        Assert.That(this.SettingsManager.GetChatTemplateProvider(template), Is.EqualTo(AIStudio.Settings.Provider.NONE), "A template must not open the way to a provider the user could not pick by hand.");
    }

    [Test]
    public void ATemplateNamesItsWorkspaceOnlyWhileWorkspacesAreUsed()
    {
        var template = NewTemplate() with { WorkspaceName = " Mails " };

        this.SettingsManager.ConfigurationData.Workspace.StorageBehavior = WorkspaceStorageBehavior.STORE_CHATS_AUTOMATICALLY;
        var whileUsed = this.SettingsManager.GetChatTemplateWorkspaceName(template);

        this.SettingsManager.ConfigurationData.Workspace.StorageBehavior = WorkspaceStorageBehavior.DISABLE_WORKSPACES;
        var whileSwitchedOff = this.SettingsManager.GetChatTemplateWorkspaceName(template);

        Assert.Multiple(() =>
        {
            Assert.That(whileUsed, Is.EqualTo("Mails"));
            Assert.That(whileSwitchedOff, Is.Null, "Without workspaces, a template must not bring one back for its chats.");
            Assert.That(this.SettingsManager.GetChatTemplateWorkspaceName(NewTemplate()), Is.Null);
        });
    }

    private Profile AddProfile()
    {
        var profile = new Profile(1, PROFILE_ID, "Mail", "I read a lot of mails.", "Answer briefly.");
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