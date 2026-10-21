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
    private const string DEFAULT_PROFILE_ID = "77777777-7777-7777-7777-777777777777";
    private const string OTHER_TEMPLATE_ID = "88888888-8888-8888-8888-888888888888";

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
    public void AChatWhoseTemplateProfileIsGoneStartsWithTheDefaultProfile()
    {
        var template = NewTemplate() with { ProfileId = PROFILE_ID };

        var withoutDefault = this.SettingsManager.GetChatTemplateStartProfile(template);

        var defaultProfile = new Profile(2, "77777777-7777-7777-7777-777777777777", "Default", "I am the default.", string.Empty);
        this.SettingsManager.ConfigurationData.Profiles.Add(defaultProfile);
        this.SettingsManager.ConfigurationData.Chat.PreselectOptions = true;
        this.SettingsManager.ConfigurationData.Chat.PreselectedProfile = defaultProfile.Id;
        var withDefault = this.SettingsManager.GetChatTemplateStartProfile(template);

        Assert.Multiple(() =>
        {
            Assert.That(withoutDefault, Is.EqualTo(Profile.NO_PROFILE), "Keeping the profile the chat had would hand out one the previous chat happened to use, a choice nobody made for this one.");
            Assert.That(withDefault, Is.EqualTo(defaultProfile));
        });
    }

    [Test]
    public void ATemplateWhichSaysNothingKeepsTheProfileAChatStartsWith()
    {
        this.AddProfile();

        Assert.Multiple(() =>
        {
            Assert.That(this.SettingsManager.GetChatTemplateStartProfile(NewTemplate()), Is.Null);
            Assert.That(this.SettingsManager.GetChatTemplateStartProfile(NewTemplate() with { AllowProfileUsage = false, ProfileId = PROFILE_ID }), Is.Null);
            Assert.That(this.SettingsManager.GetChatTemplateStartProfile(NewTemplate() with { ProfileId = PROFILE_ID })!.Id, Is.EqualTo(PROFILE_ID));
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

    [Test]
    public void WhatThePreviousTemplatePreselectedIsNotCarriedOver()
    {
        var templateProfile = this.AddProfile();
        var defaultProfile = this.AddDefaultProfile();
        var templateProvider = ToolCapableProvider();
        var defaultProvider = LessTrustedProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(templateProvider);
        this.SettingsManager.ConfigurationData.Providers.Add(defaultProvider);
        this.SettingsManager.ConfigurationData.Chat.PreselectedProvider = defaultProvider.Id;

        var previousTemplate = NewTemplate() with { ProfileId = PROFILE_ID, ProviderId = templateProvider.Id };
        var nextTemplate = NewTemplate() with { Id = OTHER_TEMPLATE_ID };

        Assert.Multiple(() =>
        {
            Assert.That(this.SettingsManager.ChooseChatTemplateProfile(nextTemplate, previousTemplate, templateProfile), Is.EqualTo(defaultProfile), "The profile was the choice of another template, not one made for this chat.");
            Assert.That(this.SettingsManager.ChooseChatTemplateProvider(nextTemplate, previousTemplate, templateProvider), Is.EqualTo(defaultProvider), "The provider was the choice of another template, not one made for this chat.");
        });
    }

    [Test]
    public void AChoiceMadeByHandStaysWhenTheTemplateSaysNothing()
    {
        var handPickedProfile = this.AddProfile();
        var defaultProfile = this.AddDefaultProfile();
        var templateProvider = ToolCapableProvider();
        var handPickedProvider = LessTrustedProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(templateProvider);
        this.SettingsManager.ConfigurationData.Providers.Add(handPickedProvider);

        var previousTemplate = NewTemplate() with { ProfileId = defaultProfile.Id, ProviderId = templateProvider.Id };
        var nextTemplate = NewTemplate() with { Id = OTHER_TEMPLATE_ID };

        Assert.Multiple(() =>
        {
            Assert.That(this.SettingsManager.ChooseChatTemplateProfile(nextTemplate, previousTemplate, handPickedProfile), Is.EqualTo(handPickedProfile));
            Assert.That(this.SettingsManager.ChooseChatTemplateProvider(nextTemplate, previousTemplate, handPickedProvider), Is.EqualTo(handPickedProvider));
            Assert.That(this.SettingsManager.ChooseChatTemplateProfile(NewTemplate(), NewTemplate(), handPickedProfile), Is.EqualTo(handPickedProfile), "Without any preselection, nothing changes.");
        });
    }

    [Test]
    public void TheNewTemplateDecidesWhenItNamesBoth()
    {
        var templateProfile = this.AddProfile();
        var otherProfile = this.AddDefaultProfile();
        var templateProvider = ToolCapableProvider();
        var otherProvider = LessTrustedProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(templateProvider);
        this.SettingsManager.ConfigurationData.Providers.Add(otherProvider);

        var previousTemplate = NewTemplate() with { Id = OTHER_TEMPLATE_ID, ProfileId = otherProfile.Id, ProviderId = otherProvider.Id };
        var nextTemplate = NewTemplate() with { ProfileId = PROFILE_ID, ProviderId = templateProvider.Id };

        Assert.Multiple(() =>
        {
            Assert.That(this.SettingsManager.ChooseChatTemplateProfile(nextTemplate, previousTemplate, otherProfile), Is.EqualTo(templateProfile));
            Assert.That(this.SettingsManager.ChooseChatTemplateProvider(nextTemplate, previousTemplate, otherProvider), Is.EqualTo(templateProvider));
        });
    }

    [Test]
    public void WithoutADefaultProviderTheChatKeepsItsProvider()
    {
        var templateProvider = ToolCapableProvider();
        this.SettingsManager.ConfigurationData.Providers.Add(templateProvider);
        this.SettingsManager.ConfigurationData.Providers.Add(LessTrustedProvider());

        var previousTemplate = NewTemplate() with { ProviderId = templateProvider.Id };
        var nextTemplate = NewTemplate() with { Id = OTHER_TEMPLATE_ID };

        Assert.That(this.SettingsManager.ChooseChatTemplateProvider(nextTemplate, previousTemplate, templateProvider), Is.EqualTo(templateProvider), "A chat without any provider could not answer at all.");
    }

    private Profile AddDefaultProfile()
    {
        var defaultProfile = new Profile(2, DEFAULT_PROFILE_ID, "Default", "I am the default.", string.Empty);
        this.SettingsManager.ConfigurationData.Profiles.Add(defaultProfile);
        this.SettingsManager.ConfigurationData.Chat.PreselectOptions = true;
        this.SettingsManager.ConfigurationData.Chat.PreselectedProfile = defaultProfile.Id;
        return defaultProfile;
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