using AIStudio.Tools;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks which API keys of configuration plugins AI Studio stores in the keyring.
/// </summary>
/// <remarks>
/// Nobody on the device can change a key which the organization provides. A key which an HTTP header
/// cannot carry would therefore fail with every request, for good, and stays out of the keyring. The
/// queue is process-wide state, so these tests empty it before and after each run.
/// </remarks>
[TestFixture]
[NonParallelizable]
public sealed class PendingEnterpriseApiKeysTests
{
    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();

    [SetUp]
    [TearDown]
    public void EmptyTheQueue() => PendingEnterpriseApiKeys.GetAndClear();

    private static PendingEnterpriseApiKey Key(string apiKey) => new("config-plugin::provider", "Provider", apiKey, SecretStoreType.LLM_PROVIDER);

    [Test]
    public void TheWhitespaceAroundAKeyGoes()
    {
        var issue = PendingEnterpriseApiKeys.Add(Key("sk-proj-AbC123\n"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issue, Is.EqualTo(APIKeyIssue.NONE));
            Assert.That(PendingEnterpriseApiKeys.GetAndClear().Select(key => key.ApiKey), Is.EqualTo(new[] { "sk-proj-AbC123" }));
        }
    }

    [Test]
    public void AKeyWhichCannotBeSentStaysOut()
    {
        var issue = PendingEnterpriseApiKeys.Add(Key($"sk-proj{ZERO_WIDTH_SPACE}AbC123"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(issue.Kind, Is.EqualTo(APIKeyIssueKind.INVISIBLE_CHARACTER));
            Assert.That(PendingEnterpriseApiKeys.GetAndClear(), Is.Empty);
        }
    }
}