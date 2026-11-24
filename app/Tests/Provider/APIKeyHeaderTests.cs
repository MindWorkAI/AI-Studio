using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AIStudio.Provider;
using AIStudio.Provider.Anthropic;
using AIStudio.Provider.Helmholtz;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.SelfHosted;
using AIStudio.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks that no API key reaches a header which the header cannot carry.
/// </summary>
/// <remarks>
/// <para>
/// .NET refuses such a key only while sending, with a message nobody understands, and two of its
/// messages about line breaks even quote the key. A provider therefore never decrypts a stored key
/// on its own: BaseProvider checks every key before it goes into a header, and the providers which
/// load their models on their own ways check the key they get from there.
/// </para>
/// <para>
/// The models are loaded with a key the user is entering, which needs neither the keyring nor the
/// network: a key which cannot be sent is refused before any request. Building a provider reads the
/// settings, so they are reached through Program.SERVICE_PROVIDER, as in the app.
/// </para>
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed partial class APIKeyHeaderTests
{
    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();

    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
    {
        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(new SettingsManager(NullLogger<SettingsManager>.Instance, null!)).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public void OnlyBaseProviderDecryptsAKeyForAHeader()
    {
        var providerDirectory = Path.Combine(AppSourceDirectory(), "Provider");
        var decryptions = Directory.EnumerateFiles(providerDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path.GetRelativePath(providerDirectory, path), DecryptionRegex().Count(File.ReadAllText(path))))
            .Where(file => file.Item2 > 0)
            .ToDictionary(file => file.Item1, file => file.Item2);

        Assert.That(decryptions, Is.EqualTo(new Dictionary<string, int> { ["BaseProvider.cs"] = 2 }), "A stored key goes into a header through DecryptAPIKey only; GetModelLoadingSecretKey is the other place which may decrypt one.");
    }

    private static IEnumerable<TestCaseData> ProvidersWhichLoadModels()
    {
        yield return new TestCaseData(() => new ProviderOpenAI()).SetArgDisplayNames("OpenAI");
        yield return new TestCaseData(() => new ProviderAnthropic()).SetArgDisplayNames("Anthropic");
        yield return new TestCaseData(() => new ProviderHelmholtz()).SetArgDisplayNames("Helmholtz");
        yield return new TestCaseData(() => new ProviderSelfHosted(Host.OLLAMA, "http://localhost:11434")).SetArgDisplayNames("Ollama");
        yield return new TestCaseData(() => new ProviderSelfHosted(Host.LLAMA_CPP, "http://localhost:8080")).SetArgDisplayNames("llama.cpp");
    }

    [TestCaseSource(nameof(ProvidersWhichLoadModels))]
    public async Task AKeyWhichCannotBeSentLoadsNoModels(Func<BaseProvider> createProvider)
    {
        var provider = createProvider();
        var result = await provider.GetTextModels($"sk-proj{ZERO_WIDTH_SPACE}AbC123");

        // Anthropic adds the models it knows anyway, so only the reason tells:
        Assert.That(result.FailureReason, Is.EqualTo(ModelLoadFailureReason.INVALID_API_KEY_CHARACTERS));
    }

    [Test]
    public void TheUserLearnsWhatToDo()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModelLoadFailureReason.INVALID_API_KEY_CHARACTERS.ToUserMessage("My provider"), Does.Contain("My provider"));
            Assert.That(ProviderRequestFailureReason.INVALID_API_KEY_CHARACTERS.Name, Is.Not.Empty);
            Assert.That(ProviderRequestFailureReason.INVALID_API_KEY_CHARACTERS.IsFixedInProviderSettings, Is.True);
        }
    }

    [GeneratedRegex(@"\.Decrypt\(Program\.ENCRYPTION\)")]
    private static partial Regex DecryptionRegex();

    private static string AppSourceDirectory([CallerFilePath] string sourceFilePath = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "..", "..", "MindWork AI Studio"));
}