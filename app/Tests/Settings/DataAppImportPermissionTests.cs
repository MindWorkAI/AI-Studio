using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which switches decide whether the user may import a configuration somebody else exported.
/// </summary>
/// <remarks>
/// An organization that switches off adding providers must not see them come back through an
/// import, and the master switch must close every type at once. Both are easy to lose when a new
/// type joins, which is why every type is checked against every switch.
/// </remarks>
[TestFixture]
public sealed class DataAppImportPermissionTests
{
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

    [TestCaseSource(nameof(IMPORTABLE_TYPES))]
    public void EveryTypeMayBeImportedByDefault(PluginConfigurationObjectType type) => Assert.That(new DataApp().CanImportConfigurationSnippet(type), Is.True);

    [TestCaseSource(nameof(IMPORTABLE_TYPES))]
    public void TheMasterSwitchLocksEveryType(PluginConfigurationObjectType type) => Assert.That(new DataApp { AllowUserToImportConfigurationSnippets = false }.CanImportConfigurationSnippet(type), Is.False);

    [TestCaseSource(nameof(IMPORTABLE_TYPES))]
    public void TheSwitchOfATypeLocksThatTypeOnly(PluginConfigurationObjectType type)
    {
        var app = new DataApp();
        SwitchOff(app, type);

        Assert.Multiple(() =>
        {
            foreach (var other in IMPORTABLE_TYPES)
                Assert.That(app.CanImportConfigurationSnippet(other), Is.EqualTo(other != type), $"Switched off: {type}, checked: {other}");
        });
    }

    [TestCase(PluginConfigurationObjectType.LLM_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.EMBEDDING_PROVIDER)]
    [TestCase(PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER)]
    public void AProviderMayOnlyBeImportedWhereItMayBeAdded(PluginConfigurationObjectType type)
    {
        var withoutAnyProvider = new DataApp { AllowUserToAddProvider = false };
        var withoutThisProvider = new DataApp
        {
            AllowUserToAddLLMProvider = type is not PluginConfigurationObjectType.LLM_PROVIDER,
            AllowUserToAddEmbeddingProvider = type is not PluginConfigurationObjectType.EMBEDDING_PROVIDER,
            AllowUserToAddTranscriptionProvider = type is not PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER,
        };

        Assert.Multiple(() =>
        {
            Assert.That(withoutAnyProvider.CanImportConfigurationSnippet(type), Is.False, "No provider may be added at all.");
            Assert.That(withoutThisProvider.CanImportConfigurationSnippet(type), Is.False, "This type of provider may not be added.");
        });
    }

    [Test]
    public void AddingProvidersHasNoSayOverTheOtherTypes()
    {
        var app = new DataApp
        {
            AllowUserToAddProvider = false,
            AllowUserToAddLLMProvider = false,
            AllowUserToAddEmbeddingProvider = false,
            AllowUserToAddTranscriptionProvider = false,
        };

        Assert.Multiple(() =>
        {
            Assert.That(app.CanImportConfigurationSnippet(PluginConfigurationObjectType.PROFILE), Is.True);
            Assert.That(app.CanImportConfigurationSnippet(PluginConfigurationObjectType.CHAT_TEMPLATE), Is.True);
            Assert.That(app.CanImportConfigurationSnippet(PluginConfigurationObjectType.DATA_SOURCE), Is.True);
            Assert.That(app.CanImportConfigurationSnippet(PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY), Is.True);
        });
    }

    [TestCase(PluginConfigurationObjectType.NONE)]
    [TestCase(PluginConfigurationObjectType.UNKNOWN)]
    public void NoneAndUnknownAreNeverImported(PluginConfigurationObjectType type) => Assert.That(new DataApp().CanImportConfigurationSnippet(type), Is.False);

    [Test]
    public void EveryTypeOfConfigurationObjectIsCovered() => Assert.That(IMPORTABLE_TYPES, Is.EquivalentTo(PluginConfigurationObjectDescriptors.ALL.Select(descriptor => descriptor.Type)), "A new type of configuration object needs a decision whether it may be imported, and its switch here.");

    private static void SwitchOff(DataApp app, PluginConfigurationObjectType type)
    {
        switch (type)
        {
            case PluginConfigurationObjectType.LLM_PROVIDER:
                app.AllowUserToImportLLMProvider = false;
                break;

            case PluginConfigurationObjectType.EMBEDDING_PROVIDER:
                app.AllowUserToImportEmbeddingProvider = false;
                break;

            case PluginConfigurationObjectType.TRANSCRIPTION_PROVIDER:
                app.AllowUserToImportTranscriptionProvider = false;
                break;

            case PluginConfigurationObjectType.PROFILE:
                app.AllowUserToImportProfile = false;
                break;

            case PluginConfigurationObjectType.CHAT_TEMPLATE:
                app.AllowUserToImportChatTemplate = false;
                break;

            case PluginConfigurationObjectType.DATA_SOURCE:
                app.AllowUserToImportERIDataSource = false;
                break;

            case PluginConfigurationObjectType.DOCUMENT_ANALYSIS_POLICY:
                app.AllowUserToImportDocumentAnalysisPolicy = false;
                break;

            case PluginConfigurationObjectType.GLOSSARY:
                app.AllowUserToImportGlossary = false;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "This test knows no switch for this type.");
        }
    }
}