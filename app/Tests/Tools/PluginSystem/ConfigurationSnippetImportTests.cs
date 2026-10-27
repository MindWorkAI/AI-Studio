using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tests.Tools.ToolCalling;
using AIStudio.Tools;
using AIStudio.Tools.Glossaries;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Services;

using Microsoft.Extensions.Logging.Abstractions;

using SharedTools;

using AuthMethod = AIStudio.Tools.ERIClient.DataModel.AuthMethod;
using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that an imported configuration snippet becomes a configuration object of the user's own,
/// which keeps what the export wrote and leaves out what does not exist on this computer.
/// </summary>
/// <remarks>
/// The exports encrypt their secrets with an encryption secret of the test. PluginFactory holds it
/// for the whole process, as the app does, so this fixture must not run in parallel with others.
/// Nothing here asserts on the wording of a hint or an issue, see ConfigurationSnippetParserTests.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class ConfigurationSnippetImportTests : ToolRegistryTestBase
{
    private const string API_KEY = "sk-imported-key";

    private const string TOKEN = "eri-imported-token";

    private static readonly PluginConfigurationObjectType[] IMPORTABLE_TYPES =
    [
        PluginConfigurationObjectType.LLM_PROVIDER,
        PluginConfigurationObjectType.EMBEDDING_PROVIDER,
        PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER,
        PluginConfigurationObjectType.PROFILE,
        PluginConfigurationObjectType.CHAT_TEMPLATE,
        PluginConfigurationObjectType.DATA_SOURCE,
        PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY,
        PluginConfigurationObjectType.GLOSSARY,
    ];

    private string temporaryDirectory = string.Empty;
    private string dataDirectory = string.Empty;
    private string? previousDataDirectory;

    [SetUp]
    public void PrepareEncryptionAndFiles()
    {
        PluginFactory.InitializeEnterpriseEncryption(EnterpriseEncryption.GenerateSecret());
        this.temporaryDirectory = Directory.CreateTempSubdirectory("snippet-import-").FullName;

        // The data directory is static state of the whole application, too. An exported tokenizer is found below it:
        this.previousDataDirectory = SettingsManager.DataDirectory;
        this.dataDirectory = Path.Combine(this.temporaryDirectory, "data");
        SettingsManager.DataDirectory = this.dataDirectory;
    }

    [TearDown]
    public void RemoveEncryptionAndFiles()
    {
        RemoveEncryptionSecret();
        SettingsManager.DataDirectory = this.previousDataDirectory;
        Directory.Delete(this.temporaryDirectory, recursive: true);
    }

    [TestCaseSource(nameof(IMPORTABLE_TYPES))]
    public void EveryExportBecomesAnObjectOfTheUsersOwn(PluginConfigurationObjectType type)
    {
        var (original, snippet) = Export(type);
        var imported = Import(snippet, type).ConfigurationObject;

        Assert.Multiple(() =>
        {
            Assert.That(Guid.TryParse(imported.Id, out _), Is.True, "Every configuration object needs a GUID as its ID.");
            Assert.That(imported.Id, Is.Not.EqualTo(original.Id).And.Not.EqualTo(ExportedId(snippet, type)), "Importing the same snippet twice must not create two objects with one ID.");
            Assert.That(imported.Num, Is.Zero, "The number is given when the object is stored.");
            Assert.That(imported.IsEnterpriseConfiguration, Is.False);
            Assert.That(imported.EnterpriseConfigurationPluginId, Is.EqualTo(Guid.Empty));
            Assert.That(imported.Name, Is.EqualTo(original.Name));
        });
    }

    [Test]
    public void EveryTypeOfConfigurationObjectCanBeImported() => Assert.That(IMPORTABLE_TYPES, Is.EquivalentTo(PluginConfigurationObjectDescriptors.ALL.Select(descriptor => descriptor.Type)), "A new type of configuration object needs a decision on how an import makes it local, and its export here.");

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.PROFILE)]
    [TestCase(PluginConfigurationObjectType.DATA_SOURCE)]
    public void AnImportKeepsEveryValueButTheIdentity(PluginConfigurationObjectType type)
    {
        var (original, snippet) = Export(type);
        var imported = Import(snippet, type).ConfigurationObject;

        Assert.That(WithIdentityOf(imported, original), Is.EqualTo(original));
    }

    [Test]
    public void AChatTemplateKeepsItsPromptsConversationAndPreselections()
    {
        var (original, snippet) = Export(PluginConfigurationObjectType.CHAT_TEMPLATE);
        var template = (ChatTemplate)original;
        var imported = (ChatTemplate)Import(snippet, PluginConfigurationObjectType.CHAT_TEMPLATE).ConfigurationObject;

        Assert.Multiple(() =>
        {
            Assert.That(imported.SystemPrompt, Is.EqualTo(template.SystemPrompt));
            Assert.That(imported.PredefinedUserPrompt, Is.EqualTo(template.PredefinedUserPrompt));
            Assert.That(imported.AllowProfileUsage, Is.EqualTo(template.AllowProfileUsage));
            Assert.That(imported.ExampleConversation.Select(Message), Is.EqualTo(template.ExampleConversation.Select(Message)));
            Assert.That(imported.ToolIds, Is.EquivalentTo(template.ToolIds!));
            Assert.That(imported.DataSourceOptions!.AutomaticDataSourceSelection, Is.EqualTo(template.DataSourceOptions!.AutomaticDataSourceSelection));
            Assert.That(imported.DataSourceOptions.AutomaticValidation, Is.EqualTo(template.DataSourceOptions.AutomaticValidation));
            Assert.That(imported.DataSourceOptions.PreselectedDataSourceIds, Is.EqualTo(template.DataSourceOptions.PreselectedDataSourceIds));
            Assert.That(imported.ProviderId, Is.EqualTo(template.ProviderId));
            Assert.That(imported.ProfileId, Is.EqualTo(template.ProfileId));
            Assert.That(imported.WorkspaceName, Is.EqualTo(template.WorkspaceName));
        });
    }

    [Test]
    public void APolicyKeepsItsRulesButNotItsProtection()
    {
        var (original, snippet) = Export(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY);
        var policy = (DataDocumentAnalysisPolicy)original;
        var imported = (DataDocumentAnalysisPolicy)Import(snippet, PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY).ConfigurationObject;

        Assert.Multiple(() =>
        {
            Assert.That(imported.IsProtected, Is.False, "A configuration plugin protects its policies; the user's own policy is the user's to edit.");
            Assert.That(imported.PolicyDescription, Is.EqualTo(policy.PolicyDescription));
            Assert.That(imported.AnalysisRules, Is.EqualTo(policy.AnalysisRules));
            Assert.That(imported.OutputRules, Is.EqualTo(policy.OutputRules));
            Assert.That(imported.MinimumProviderConfidence, Is.EqualTo(policy.MinimumProviderConfidence));
            Assert.That(imported.AllowedToolIds, Is.EquivalentTo(policy.AllowedToolIds));
            Assert.That(imported.PreselectedProvider, Is.EqualTo(policy.PreselectedProvider));
            Assert.That(imported.PreselectedProfile, Is.EqualTo(policy.PreselectedProfile));
            Assert.That(imported.HidePolicyDefinition, Is.EqualTo(policy.HidePolicyDefinition));
        });
    }

    [Test]
    public void ASnippetWhichIsNoValidConfigurationIsRefused()
    {
        const string PROFILE_WITHOUT_ACTIONS = """CONFIG["PROFILES"][#CONFIG["PROFILES"]+1] = { ["Id"] = "6b8e1a2c-3d4f-4a5b-9c6d-7e8f9a0b1c2d", ["Name"] = "Writer", ["NeedToKnow"] = "I write." }""";

        Assert.Multiple(() =>
        {
            Assert.That(ConfigurationSnippetImport.TryImport(PROFILE_WITHOUT_ACTIONS, PluginConfigurationObjectType.PROFILE, out var result, out var issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.Not.Empty);
        });
    }

    [Test]
    public void ASnippetWhichCannotBeReadIsRefused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConfigurationSnippetImport.TryImport("CONFIG[\"PROFILES\"] = os.getenv(\"HOME\")", PluginConfigurationObjectType.PROFILE, out _, out var issue), Is.False);
            Assert.That(issue, Is.Not.Empty);
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public void TheAPIKeyOfAProviderIsDecrypted(PluginConfigurationObjectType type)
    {
        var result = Import(ExportProvider(type, Encrypt(API_KEY)).Snippet, type);

        Assert.Multiple(() =>
        {
            Assert.That(result.Secret, Is.EqualTo(API_KEY));
            Assert.That(result.Hints, Is.Empty);
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public void AnAPIKeyOfAnotherOrganizationLeavesAHint(PluginConfigurationObjectType type)
    {
        var otherOrganization = new EnterpriseEncryption(NullLogger<EnterpriseEncryption>.Instance, EnterpriseEncryption.GenerateSecret());
        Assert.That(otherOrganization.TryEncrypt(API_KEY, EnterpriseEncryptionFormat.V2, out var foreignApiKey), Is.True);

        AssertAPIKeyLeftOut(Import(ExportProvider(type, foreignApiKey).Snippet, type));
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public void AnAPIKeyWithoutAnEncryptionSecretLeavesAHint(PluginConfigurationObjectType type)
    {
        var snippet = ExportProvider(type, Encrypt(API_KEY)).Snippet;
        RemoveEncryptionSecret();

        AssertAPIKeyLeftOut(Import(snippet, type));
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public void AnUnencryptedAPIKeyIsLeftOut(PluginConfigurationObjectType type) => AssertAPIKeyLeftOut(Import(ExportProvider(type, API_KEY).Snippet, type));

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public void AnImportedProviderNeverAsksForTheAPIKeyOfTheUser(PluginConfigurationObjectType type)
    {
        //
        // An export never writes the option, but a snippet may come from a configuration plugin of an
        // organization. The option implies the user-provided key, so both have to go:
        //
        var snippet = ExportProvider(type, null).Snippet;
        var tableStart = snippet.IndexOf("= {", StringComparison.Ordinal) + "= {".Length;
        var snippetWithPrompt = snippet.Insert(tableStart, """

                                                              ["PromptForUserAPIKey"] = true,
                                                          """);

        var imported = (IUserProvidedAPIKey)Import(snippetWithPrompt, type).ConfigurationObject;
        Assert.Multiple(() =>
        {
            Assert.That(imported.PromptForUserAPIKey, Is.False);
            Assert.That(imported.AllowUserProvidedAPIKey, Is.False);
        });
    }

    [Test]
    public void TheTokenOfADataSourceIsDecrypted() => Assert.That(Import(ExportDataSource(AuthMethod.TOKEN).Snippet, PluginConfigurationObjectType.DATA_SOURCE).Secret, Is.EqualTo(TOKEN));

    [Test]
    public void ADataSourceWithATokenNeedsTheEncryptionSecret()
    {
        var snippet = ExportDataSource(AuthMethod.TOKEN).Snippet;
        RemoveEncryptionSecret();

        AssertRefused(snippet, PluginConfigurationObjectType.DATA_SOURCE);
    }

    [Test]
    public void ADataSourceWithATokenOfAnotherOrganizationIsRefused()
    {
        var snippet = ExportDataSource(AuthMethod.TOKEN).Snippet;
        PluginFactory.InitializeEnterpriseEncryption(EnterpriseEncryption.GenerateSecret());

        AssertRefused(snippet, PluginConfigurationObjectType.DATA_SOURCE);
    }

    [Test]
    public void ADataSourceWithoutSignInNeedsNoEncryptionSecret()
    {
        var snippet = ExportDataSource(AuthMethod.NONE).Snippet;
        RemoveEncryptionSecret();

        Assert.That(Import(snippet, PluginConfigurationObjectType.DATA_SOURCE).Secret, Is.Empty);
    }

    [Test]
    public void AKerberosDataSourceIsRefused() => AssertRefused(ExportDataSource(AuthMethod.KERBEROS).Snippet, PluginConfigurationObjectType.DATA_SOURCE);

    [TestCase("jane.doe", "jane.doe")]
    [TestCase("", "")]
    public void ASharedUsernameIsKeptButNotItsPlaceholder(string username, string expectedUsername)
    {
        var (_, snippet) = ExportDataSource(AuthMethod.USERNAME_PASSWORD, username, DataSourceERIUsernamePasswordMode.SHARED_USERNAME_AND_PASSWORD);
        var result = Import(snippet, PluginConfigurationObjectType.DATA_SOURCE);
        var dataSource = (DataSourceERI_V1)result.ConfigurationObject;

        Assert.Multiple(() =>
        {
            Assert.That(dataSource.Username, Is.EqualTo(expectedUsername));
            Assert.That(dataSource.UsernamePasswordMode, Is.EqualTo(DataSourceERIUsernamePasswordMode.USER_MANAGED), "Only an organization manages the sign-in for its users.");
            Assert.That(result.Secret, Is.EqualTo(TOKEN));
        });
    }

    [TestCase(null, OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, TestName = "A data source without a level gets the one of a new data source")]
    [TestCase("\"ONLY_CONFIGURED_SERVICES\"", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, TestName = "A data source keeps the level it names")]
    [TestCase("\"only_links_from_chat\"", OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, TestName = "A level is read regardless of case")]
    [TestCase("\"NOTHING_AT_ALL\"", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, TestName = "A level this version does not know counts as the strictest")]
    [TestCase("\"2\"", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, TestName = "A number is no level, even as text")]
    [TestCase("2", OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, TestName = "A number is no level")]
    public void ADataSourceReadsWhereAChatMaySendData(string? luaValue, OutboundDataRestriction expected)
    {
        var (_, exported) = ExportDataSource(AuthMethod.NONE);
        const string EXPORTED_LINE = "[\"OutboundDataRestriction\"] = \"UNRESTRICTED\",";
        Assert.That(exported, Does.Contain(EXPORTED_LINE));

        var snippet = exported.Replace(EXPORTED_LINE, luaValue is null ? string.Empty : $"[\"OutboundDataRestriction\"] = {luaValue},", StringComparison.Ordinal);
        var dataSource = (DataSourceERI_V1)Import(snippet, PluginConfigurationObjectType.DATA_SOURCE).ConfigurationObject;

        Assert.That(dataSource.OutboundDataRestriction, Is.EqualTo(expected));
    }

    [Test]
    public void AttachmentsWhichAreNotOnThisComputerAreToBeRelinked()
    {
        var existing = this.CreateFile("report.pdf");
        var missing = Path.Combine(this.temporaryDirectory, "missing.pdf");
        const string RELATIVE = "attachments/notes.txt";
        const string WINDOWS_SHARE = @"\\fileserver\share\minutes.docx";
        const string UNIX_SHARE = "//fileserver/share/minutes.docx";

        var result = Import(ExportChatTemplate([existing, missing, RELATIVE, WINDOWS_SHARE, UNIX_SHARE]), PluginConfigurationObjectType.CHAT_TEMPLATE);
        var template = (ChatTemplate)result.ConfigurationObject;

        Assert.Multiple(() =>
        {
            Assert.That(template.FileAttachments.Select(attachment => attachment.FilePath), Is.EqualTo(new[] { existing }));
            Assert.That(result.AttachmentsToRelink, Is.EqualTo(new[] { missing, RELATIVE, WINDOWS_SHARE, UNIX_SHARE }));
        });
    }

    [Test]
    public void AttachmentsOnThisComputerAreNamedInAHint()
    {
        var existing = this.CreateFile("id_rsa");
        var result = Import(ExportChatTemplate([existing]), PluginConfigurationObjectType.CHAT_TEMPLATE);

        Assert.That(result.Hints, Has.Exactly(1).Contains(existing), "A snippet can name any file of this computer, so the user has to see which ones every chat would carry.");
    }

    [Test]
    public void AChatTemplateWithoutAttachmentsLeavesNoHint()
    {
        var result = Import(ExportChatTemplate([]), PluginConfigurationObjectType.CHAT_TEMPLATE);

        Assert.Multiple(() =>
        {
            Assert.That(result.Hints, Is.Empty);
            Assert.That(result.AttachmentsToRelink, Is.Empty);
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public void ATokenizerOnThisComputerIsKept(PluginConfigurationObjectType type)
    {
        // The export keeps only the file name of the tokenizer and names the folder of the provider's copy itself:
        var (original, snippet) = ExportProvider(type, null, Path.Combine(this.temporaryDirectory, "tokenizer.json"));
        var copy = this.CreateTokenizerCopy(type, original.Id);
        var result = Import(snippet, type);

        Assert.Multiple(() =>
        {
            Assert.That(TokenizerPathOf(result.ConfigurationObject), Is.EqualTo(copy), "Imported on the computer it came from, the export leads back to the copy of the provider.");
            Assert.That(result.Hints, Is.Empty);
        });
    }

    [Test]
    public async Task AnImportedEmbeddingProviderIsStoredWithTheFingerprintOfItsTokenizer()
    {
        const PluginConfigurationObjectType TYPE = PluginConfigurationObjectType.EMBEDDING_PROVIDER;
        var (original, snippet) = ExportProvider(TYPE, null, Path.Combine(this.temporaryDirectory, "tokenizer.json"));
        var copy = this.CreateTokenizerCopy(TYPE, original.Id);
        var imported = (EmbeddingProvider)Import(snippet, TYPE).ConfigurationObject;

        // What the embedding provider dialog does while storing the imported provider:
        var storedFingerprint = await TokenizerFingerprint.KeepOrReadAsync(imported.TokenizerPath, imported.TokenizerFingerprint);
        var fingerprintOfTheCopiedFile = await TokenizerFingerprint.ForFileAsync(copy);

        Assert.Multiple(() =>
        {
            Assert.That(imported.TokenizerFingerprint, Is.Empty, "The import reads no file before the user agreed to it.");
            Assert.That(storedFingerprint, Is.Not.Empty.And.EqualTo(fingerprintOfTheCopiedFile), "Without the fingerprint, the embedding signature cannot tell the tokenizer apart from no tokenizer at all.");
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public void ATokenizerWhichIsNotOnThisComputerIsLeftOut(PluginConfigurationObjectType type)
    {
        var result = Import(ExportProvider(type, null, Path.Combine(this.temporaryDirectory, "missing-tokenizer.json")).Snippet, type);

        Assert.Multiple(() =>
        {
            Assert.That(TokenizerPathOf(result.ConfigurationObject), Is.Empty, "Saving the provider would fail on a tokenizer which cannot be copied.");
            Assert.That(result.Hints, Has.Exactly(1).Contains("missing-tokenizer.json"));
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public void TheAbsoluteTokenizerPathOfAnOlderExportIsKept(PluginConfigurationObjectType type)
    {
        var tokenizer = this.CreateFile("tokenizer.json");
        var result = Import(ExportProviderWithTokenizerPath(type, tokenizer), type);

        Assert.Multiple(() =>
        {
            Assert.That(TokenizerPathOf(result.ConfigurationObject), Is.EqualTo(tokenizer), "Versions before v26.10.1 exported the absolute path of the tokenizer.");
            Assert.That(result.Hints, Is.Empty);
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    public void AnExportCannotNameAnotherFileOfTheDataDirectoryAsItsTokenizer(PluginConfigurationObjectType type)
    {
        Directory.CreateDirectory(this.dataDirectory);
        File.WriteAllText(Path.Combine(this.dataDirectory, "settings.json"), "{}");
        var result = Import(ExportProviderWithTokenizerPath(type, "tokenizers/../settings.json"), type);

        Assert.Multiple(() =>
        {
            Assert.That(TokenizerPathOf(result.ConfigurationObject), Is.Empty, "Only the folder of the tokenizers holds what an export may name.");
            Assert.That(result.Hints, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task MissingReferencesOfAPolicyAreNamed()
    {
        var missingProfileId = Guid.NewGuid().ToString();
        var snippet = ExportPolicy(preselectedProvider: "missing-provider", preselectedProfile: missingProfileId, allowedToolIds: [TOOL_ID, "missing_tool"]);
        var result = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(snippet, PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY), this.SettingsManager, this.CreateRegistry(new TestTool(Definition())));

        Assert.Multiple(() =>
        {
            Assert.That(result.Hints, Has.Count.EqualTo(3), "One sentence for each kind of reference.");
            Assert.That(result.Hints, Has.Exactly(1).Contains("missing-provider"));
            Assert.That(result.Hints, Has.Exactly(1).Contains(missingProfileId));
            Assert.That(result.Hints, Has.Exactly(1).Contains("missing_tool"));
            Assert.That(result.Hints, Has.None.Contains(TOOL_ID), "The tool exists.");
        });
    }

    [Test]
    public async Task MissingReferencesOfAChatTemplateAreNamed()
    {
        var missingDataSourceId = Guid.NewGuid().ToString();
        var missingProviderId = Guid.NewGuid().ToString();
        var missingProfileId = Guid.NewGuid().ToString();
        var snippet = ExportChatTemplate([], toolIds: [TOOL_ID, "missing_tool"], dataSourceIds: [missingDataSourceId], providerId: missingProviderId, profileId: missingProfileId);
        var result = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(snippet, PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, this.CreateRegistry(new TestTool(Definition())));

        Assert.Multiple(() =>
        {
            Assert.That(result.Hints, Has.Count.EqualTo(4), "One sentence for each kind of reference.");
            Assert.That(result.Hints, Has.Exactly(1).Contains(missingDataSourceId));
            Assert.That(result.Hints, Has.Exactly(1).Contains("missing_tool"));
            Assert.That(result.Hints, Has.Exactly(1).Contains(missingProviderId));
            Assert.That(result.Hints, Has.Exactly(1).Contains(missingProfileId));
        });
    }

    [Test]
    public async Task AChatTemplateWhichStartsWithoutAProfileNamesNothingMissing()
    {
        var snippet = ExportChatTemplate([], profileId: Profile.NO_PROFILE.Id);
        var result = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(snippet, PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, this.CreateRegistry(new TestTool(Definition())));

        Assert.Multiple(() =>
        {
            Assert.That(result.Hints, Is.Empty, "Starting without a profile is a statement, not a reference to a profile which could be missing.");
            Assert.That(((ChatTemplate)result.ConfigurationObject).ProfileId, Is.EqualTo(Profile.NO_PROFILE.Id));
        });
    }

    [Test]
    public async Task AProviderBelowTheConfidenceOfTheChatIsNamed()
    {
        // A chat template names its provider by a GUID, as every provider of the app has one:
        var provider = LessTrustedProvider() with { Id = Guid.NewGuid().ToString() };
        this.SettingsManager.ConfigurationData.Providers.Add(provider);
        this.SettingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        this.SettingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var snippet = ExportChatTemplate([], providerId: provider.Id);
        var result = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(snippet, PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, this.CreateRegistry(new TestTool(Definition())));

        Assert.Multiple(() =>
        {
            Assert.That(result.Hints, Has.Count.EqualTo(1), "The provider exists, but chats started with the template would not use it.");
            Assert.That(result.Hints, Has.Exactly(1).Contains(provider.InstanceName));
        });
    }

    [Test]
    public async Task AProfileOfATemplateWhichForbidsProfilesIsNamed()
    {
        var missingProfileId = Guid.NewGuid().ToString();
        var template = ChatTemplateWith([], null, null, profileId: missingProfileId) with { AllowProfileUsage = false };
        Assert.That(template.TryExportAsConfigurationSection(out var snippet, out var issue), Is.True, issue);

        var result = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(snippet, PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, this.CreateRegistry(new TestTool(Definition())));

        Assert.Multiple(() =>
        {
            Assert.That(result.Hints, Has.Count.EqualTo(1), "Whether the profile exists does not matter, since chats ignore it anyway.");
            Assert.That(result.Hints, Has.None.Contains(missingProfileId));
        });
    }

    [Test]
    public async Task ReferencesWhichExistLeaveNoHint()
    {
        var provider = ExportProvider(PluginConfigurationObjectType.LLM_PROVIDER, null).Original;
        var profile = Export(PluginConfigurationObjectType.PROFILE).Original;
        var dataSource = ExportDataSource(AuthMethod.NONE).Original;
        this.SettingsManager.ConfigurationData.Providers.Add((AIStudio.Settings.Provider)provider);
        this.SettingsManager.ConfigurationData.Profiles.Add((Profile)profile);
        this.SettingsManager.ConfigurationData.DataSources.Add((DataSourceERI_V1)dataSource);
        var registry = this.CreateRegistry(new TestTool(Definition()));

        var policy = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(ExportPolicy(provider.Id, profile.Id, [TOOL_ID]), PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY), this.SettingsManager, registry);
        var template = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(ExportChatTemplate([], [TOOL_ID], [dataSource.Id], provider.Id, profile.Id), PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, registry);

        Assert.Multiple(() =>
        {
            Assert.That(policy.Hints, Is.Empty);
            Assert.That(template.Hints, Is.Empty);
        });
    }

    [Test]
    public async Task AToolOfACollectionIsSelectedAsItsCollection()
    {
        const string COLLECTION_ID = "test_collection";
        var registry = this.CreateRegistry([new TestCollection(COLLECTION_ID, ConfidenceLevel.NONE, TOOL_ID)], new TestTool(Definition()));

        var policy = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(ExportPolicy(allowedToolIds: [TOOL_ID]), PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY), this.SettingsManager, registry);
        var template = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(ExportChatTemplate([], [TOOL_ID]), PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, registry);

        Assert.Multiple(() =>
        {
            Assert.That(((DataDocumentAnalysisPolicy)policy.ConfigurationObject).AllowedToolIds, Is.EquivalentTo(new[] { COLLECTION_ID }));
            Assert.That(((ChatTemplate)template.ConfigurationObject).ToolIds, Is.EquivalentTo(new[] { COLLECTION_ID }));
            Assert.That(policy.Hints, Is.Empty);
            Assert.That(template.Hints, Is.Empty);
        });
    }

    [Test]
    public async Task AToolYouSwitchedOffIsNotMissing()
    {
        var registry = this.CreateRegistry(new TestTool(Definition()));
        registry.SetSwitchedOffByUser(TOOL_ID, true);

        var policy = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(ExportPolicy(allowedToolIds: [TOOL_ID]), PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY), this.SettingsManager, registry);
        var template = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(ExportChatTemplate([], [TOOL_ID]), PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, registry);

        Assert.Multiple(() =>
        {
            Assert.That(policy.Hints, Is.Empty, "The tool exists on this computer; switching it on again makes the policy use it.");
            Assert.That(template.Hints, Is.Empty);
            Assert.That(((DataDocumentAnalysisPolicy)policy.ConfigurationObject).AllowedToolIds, Is.EquivalentTo(new[] { TOOL_ID }), "The import keeps the tool.");
            Assert.That(((ChatTemplate)template.ConfigurationObject).ToolIds, Is.EquivalentTo(new[] { TOOL_ID }));
        });
    }

    [Test]
    public async Task AChatTemplateWhichSaysNothingAboutToolsKeepsSayingNothing()
    {
        var result = await ConfigurationSnippetImport.ResolveReferencesAsync(Import(ExportChatTemplate([], toolIds: null), PluginConfigurationObjectType.CHAT_TEMPLATE), this.SettingsManager, this.CreateRegistry(new TestTool(Definition())));

        Assert.That(((ChatTemplate)result.ConfigurationObject).ToolIds, Is.Null, "No tools at all would be a statement the template never made.");
    }

    private static ConfigurationSnippetImportResult Import(string snippet, PluginConfigurationObjectType type)
    {
        Assert.That(ConfigurationSnippetImport.TryImport(snippet, type, out var result, out var issue), Is.True, issue);
        return result!;
    }

    private static void AssertRefused(string snippet, PluginConfigurationObjectType type)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConfigurationSnippetImport.TryImport(snippet, type, out var result, out var issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.Not.Empty);
        });
    }

    private static void AssertAPIKeyLeftOut(ConfigurationSnippetImportResult result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.Secret, Is.Empty);
            Assert.That(result.Hints, Has.Count.EqualTo(1), "The provider is imported, but the user has to enter a key.");
        });
    }

    /// <summary>
    /// Leaves this computer without the encryption secret of an organization, as most computers are.
    /// </summary>
    private static void RemoveEncryptionSecret() => PluginFactory.InitializeEnterpriseEncryption(encryptionSecret: null);

    /// <summary>
    /// Encrypts in the authenticated format. In the legacy format, a wrong secret passes in rare
    /// cases, so the tests with the secret of another organization would fail now and then.
    /// </summary>
    private static string Encrypt(string secret)
    {
        Assert.That(PluginFactory.EnterpriseEncryption!.TryEncrypt(secret, EnterpriseEncryptionFormat.V2, out var encryptedSecret), Is.True);
        return encryptedSecret;
    }

    private static readonly GlossaryEntry[] GLOSSARY_ENTRIES =
    [
        new("Arbeitszeit", "working time", ["Arbeitszeiten"], "HR only"),
        new("Urlaub", "vacation"),
    ];

    private static Glossary GlossaryWithEntries() => new()
    {
        Num = 1,
        Id = Guid.NewGuid().ToString(),
        Name = "HR terms",
        Type = GlossaryType.TRANSLATION,
        Description = "Terms of our HR department.",
        SourceLanguage = CommonLanguages.DE_DE,
        TargetLanguage = CommonLanguages.EN_US,
        PreselectInTranslation = false,
    };

    [Test]
    public void AnImportedGlossaryKeepsItsDescriptionAndBringsItsEntries()
    {
        var (original, snippet) = Export(PluginConfigurationObjectType.GLOSSARY);
        var imported = (Glossary)Import(snippet, PluginConfigurationObjectType.GLOSSARY).ConfigurationObject;

        Assert.Multiple(() =>
        {
            Assert.That(imported with { Id = original.Id, Num = original.Num, PluginSource = null }, Is.EqualTo(original));

            var entries = imported.PluginSource!.Entries;
            Assert.That(entries.Select(entry => (entry.Term, entry.Value, entry.Note)), Is.EqualTo(GLOSSARY_ENTRIES.Select(entry => (entry.Term, entry.Value, entry.Note))));
            Assert.That(entries[0].Aliases, Is.EqualTo(new[] { "Arbeitszeiten" }));
        });
    }

    [Test]
    public void AGlossaryWhichTakesItsEntriesFromAFileIsRefused()
    {
        var snippet = $$"""
                        CONFIG["GLOSSARIES"][#CONFIG["GLOSSARIES"]+1] = {
                            ["Id"] = "{{Guid.NewGuid()}}",
                            ["Name"] = "HR terms",
                            ["Type"] = "REFERENCE",
                            ["File"] = { ["Path"] = "glossaries/hr.xlsx", ["TermColumn"] = "Begriff", ["ValueColumn"] = "Erklärung" },
                        }
                        """;

        Assert.That(ConfigurationSnippetImport.TryImport(snippet, PluginConfigurationObjectType.GLOSSARY, out _, out var issue), Is.False, "The file belongs to a plugin which is not on this computer.");
        Assert.That(issue, Is.Not.Empty);
    }

    private static string ExportedId(string snippet, PluginConfigurationObjectType type)
    {
        Assert.That(ConfigurationSnippetParser.TryParse(snippet, type, out var table, out var issue), Is.True, issue);
        return table["Id"].Read<string>();
    }

    private static object WithIdentityOf(IConfigurationObject imported, IConfigurationObject original) => imported switch
    {
        ConfigurationBaseObject configObject => configObject with { Id = original.Id, Num = original.Num },
        DataSourceERI_V1 dataSource => dataSource with { Id = original.Id, Num = original.Num },
        _ => throw new ArgumentOutOfRangeException(nameof(imported), imported, "This test cannot compare this type."),
    };

    private static string TokenizerPathOf(IConfigurationObject configObject) => configObject switch
    {
        AIStudio.Settings.Provider provider => provider.TokenizerPath,
        EmbeddingProvider provider => provider.TokenizerPath,
        _ => throw new ArgumentOutOfRangeException(nameof(configObject), configObject, "This type has no tokenizer."),
    };

    private static (ChatRole Role, string Text) Message(ContentBlock block) => (block.Role, (block.Content as ContentText)?.Text ?? string.Empty);

    private string CreateFile(string fileName)
    {
        var path = Path.Combine(this.temporaryDirectory, fileName);
        File.WriteAllText(path, "{}");
        return path;
    }

    /// <summary>
    /// Creates the tokenizer copy of a provider where the runtime stores it, below the data directory.
    /// </summary>
    private string CreateTokenizerCopy(PluginConfigurationObjectType type, string providerId)
    {
        var modelId = type is PluginConfigurationObjectType.LLM_PROVIDER ? TokenizerModelId.ForProviderId(providerId) : TokenizerModelId.ForEmbeddingProviderId(providerId);
        var path = Path.Combine(this.dataDirectory, "tokenizers", modelId, "tokenizer.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        return path;
    }

    /// <summary>
    /// What an export of a provider carries when its tokenizer path was written otherwise, e.g., by
    /// an older version or by hand.
    /// </summary>
    private static string ExportProviderWithTokenizerPath(PluginConfigurationObjectType type, string tokenizerPath)
    {
        const string EMPTY_TOKENIZER_PATH = """["TokenizerPath"] = "",""";
        var snippet = ExportProvider(type, null).Snippet;
        Assert.That(snippet, Does.Contain(EMPTY_TOKENIZER_PATH));

        return snippet.Replace(EMPTY_TOKENIZER_PATH, $"""["TokenizerPath"] = "{LuaTools.EscapeLuaString(tokenizerPath)}",""");
    }

    /// <summary>
    /// What the app exports for one item of each type, along with the item.
    /// </summary>
    /// <remarks>
    /// The items are made here rather than in a test case source: NUnit reads such a source before
    /// TestHost runs, and the provider types need its logger factory. Texts span lines with line
    /// feeds only, as Lua turns every other line break of a long string into one.
    /// </remarks>
    private static (IConfigurationObject Original, string Snippet) Export(PluginConfigurationObjectType type)
    {
        switch (type)
        {
            case PluginConfigurationObjectType.LLM_PROVIDER or PluginConfigurationObjectType.EMBEDDING_PROVIDER or PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER:
                return ExportProvider(type, null);

            case PluginConfigurationObjectType.PROFILE:
                var profile = new Profile(1, Guid.NewGuid().ToString(), "Technical writer", "I write manuals.\nMy readers are engineers.", "Keep the answers short.");
                return (profile, profile.ExportAsConfigurationSection());

            case PluginConfigurationObjectType.CHAT_TEMPLATE:
                var template = ChatTemplateWith([], ["web_search"], [Guid.NewGuid().ToString()], Guid.NewGuid().ToString(), Guid.NewGuid().ToString()) with { WorkspaceName = "Contracts" };
                Assert.That(template.TryExportAsConfigurationSection(out var luaCode, out var issue), Is.True, issue);
                return (template, luaCode);

            case PluginConfigurationObjectType.DATA_SOURCE:
                return ExportDataSource(AuthMethod.TOKEN);

            case PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY:
                var policy = PolicyWith(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), ["web_search", "read_web_page"]);
                return (policy, policy.ExportAsConfigurationSection());

            case PluginConfigurationObjectType.GLOSSARY:
                var glossary = GlossaryWithEntries();
                return (glossary, glossary.ExportAsConfigurationSection(GLOSSARY_ENTRIES));

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "This test knows no export for this type.");
        }
    }

    private static (IConfigurationObject Original, string Snippet) ExportProvider(PluginConfigurationObjectType type, string? encryptedApiKey, string tokenizerPath = "")
    {
        switch (type)
        {
            case PluginConfigurationObjectType.LLM_PROVIDER:
                var provider = new AIStudio.Settings.Provider(
                    1,
                    Guid.NewGuid().ToString(),
                    "Ollama on the workstation",
                    LLMProviders.SELF_HOSTED,
                    new Model("llama-3.3-70b", "Llama 3.3"),
                    IsSelfHosted: true,
                    Hostname: "http://localhost:11434",
                    Host: Host.OLLAMA,
                    AdditionalJsonApiParameters: "\"temperature\": 0.5",
                    TokenizerPath: tokenizerPath,
                    CapabilityOverrides: new ProviderCapabilityOverrides { FunctionCalling = true, ContextWindowTokens = 32_768 });
                return (provider, provider.ExportAsConfigurationSection(encryptedApiKey));

            case PluginConfigurationObjectType.EMBEDDING_PROVIDER:
                var embeddingProvider = new EmbeddingProvider(
                    1,
                    Guid.NewGuid().ToString(),
                    "Embeddings on the workstation",
                    LLMProviders.SELF_HOSTED,
                    new Model("nomic-embed-text", "Nomic"),
                    IsSelfHosted: true,
                    Hostname: "http://localhost:11434",
                    Host: Host.OLLAMA,
                    TokenizerPath: tokenizerPath,
                    EmbeddingBatchSize: 16,
                    TokenLimit: 512);
                return (embeddingProvider, embeddingProvider.ExportAsConfigurationSection(encryptedApiKey));

            case PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER:
                var transcriptionProvider = new TranscriptionProvider(
                    1,
                    Guid.NewGuid().ToString(),
                    "Whisper on the workstation",
                    LLMProviders.SELF_HOSTED,
                    new Model("whisper-large-v3", "Whisper"),
                    IsSelfHosted: true,
                    Hostname: "http://localhost:8080",
                    Host: Host.WHISPER_CPP);
                return (transcriptionProvider, transcriptionProvider.ExportAsConfigurationSection(encryptedApiKey));

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "This is no type of provider.");
        }
    }

    private static (IConfigurationObject Original, string Snippet) ExportDataSource(AuthMethod authMethod, string username = "", DataSourceERIUsernamePasswordMode usernamePasswordMode = DataSourceERIUsernamePasswordMode.USER_MANAGED)
    {
        var dataSource = new DataSourceERI_V1
        {
            Num = 1,
            Id = Guid.NewGuid().ToString(),
            Name = "Contracts",
            Type = DataSourceType.ERI_V1,
            Hostname = "https://eri.example.org",
            Port = 8443,
            AuthMethod = authMethod,
            Username = username,
            SecurityPolicy = DataSourceSecurity.SELF_HOSTED,

            // Not the default, so that a parser which ignores the level does not pass unnoticed. The
            // stored level travels, not the stricter one the security policy demands at runtime:
            OutboundDataRestriction = OutboundDataRestriction.UNRESTRICTED,
            SelectedRetrievalId = "semantic-search",
            MaxMatches = 25,
        };

        var encryptedSecret = authMethod is AuthMethod.TOKEN or AuthMethod.USERNAME_PASSWORD ? Encrypt(TOKEN) : null;
        return (dataSource, dataSource.ExportAsConfigurationSection(encryptedSecret, usernamePasswordMode));
    }

    private static string ExportChatTemplate(IReadOnlyList<string> attachmentPaths, IReadOnlyList<string>? toolIds = null, IReadOnlyList<string>? dataSourceIds = null, string? providerId = null, string? profileId = null)
    {
        var template = ChatTemplateWith(attachmentPaths, toolIds, dataSourceIds, providerId, profileId);
        Assert.That(template.TryExportAsConfigurationSection(out var luaCode, out var issue), Is.True, issue);
        return luaCode;
    }

    private static ChatTemplate ChatTemplateWith(IReadOnlyList<string> attachmentPaths, IReadOnlyList<string>? toolIds, IReadOnlyList<string>? dataSourceIds, string? providerId = null, string? profileId = null) => new()
    {
        Num = 1,
        Id = Guid.NewGuid().ToString(),
        Name = "Contract review",
        SystemPrompt = "You review contracts.\nYou quote every clause you mention.",
        PredefinedUserPrompt = "Please review: ",
        AllowProfileUsage = true,
        FileAttachments = attachmentPaths.Select(path => new FileAttachment(FileAttachmentType.DOCUMENT, Path.GetFileName(path), path, 0)).ToList(),
        ExampleConversation =
        [
            new ContentBlock { ContentType = ContentType.TEXT, Role = ChatRole.USER, Content = new ContentText { Text = "Is clause 4 fair?" } },
            new ContentBlock { ContentType = ContentType.TEXT, Role = ChatRole.AI, Content = new ContentText { Text = "Clause 4 says:\n> The tenant pays." } },
        ],
        ToolIds = toolIds?.ToHashSet(StringComparer.Ordinal),
        DataSourceOptions = dataSourceIds is null ? null : new() { DisableDataSources = false, AutomaticValidation = true, PreselectedDataSourceIds = [..dataSourceIds] },
        ProviderId = providerId,
        ProfileId = profileId,
    };

    private static string ExportPolicy(string preselectedProvider = "", string preselectedProfile = "", IReadOnlyList<string>? allowedToolIds = null) => PolicyWith(preselectedProvider, preselectedProfile, allowedToolIds ?? []).ExportAsConfigurationSection();

    private static DataDocumentAnalysisPolicy PolicyWith(string preselectedProvider, string preselectedProfile, IReadOnlyList<string> allowedToolIds) => new()
    {
        Num = 1,
        Id = Guid.NewGuid().ToString(),
        PolicyName = "Contract check",
        PolicyDescription = "Checks contracts for the clauses our legal department cares about.",
        AnalysisRules = "Find every clause on liability.\nFind every clause on termination.",
        OutputRules = "- Answer in a table.\n- Quote the clause.",
        MinimumProviderConfidence = ConfidenceLevel.MEDIUM,
        AllowedToolIds = allowedToolIds.ToHashSet(StringComparer.Ordinal),
        PreselectedProvider = preselectedProvider,
        PreselectedProfile = preselectedProfile,
        HidePolicyDefinition = true,
        IsProtected = true,
    };
}