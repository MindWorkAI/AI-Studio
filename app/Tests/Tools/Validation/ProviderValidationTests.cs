using AIStudio.Provider;
using AIStudio.Tools.Validation;

namespace AIStudio.Tests.Tools.Validation;

/// <summary>
/// Checks which API keys the dialogs of the LLM, embedding, and transcription providers accept.
/// </summary>
/// <remarks>
/// Nothing here asserts on the wording of a message: I18N is process-wide state without a reset.
/// </remarks>
[TestFixture]
public sealed class ProviderValidationTests
{
    private static readonly string ZERO_WIDTH_SPACE = ((char)0x200B).ToString();

    private static ProviderValidation For(LLMProviders provider, string storageIssue = "") => new()
    {
        GetProvider = () => provider,
        GetAPIKeyStorageIssue = () => storageIssue,
    };

    [TestCase(LLMProviders.OPEN_AI, ExpectedResult = false)]
    [TestCase(LLMProviders.ANTHROPIC, ExpectedResult = false)]
    [TestCase(LLMProviders.SELF_HOSTED, ExpectedResult = true)]
    public bool OnlyASelfHostedServerMayGoWithoutAKey(LLMProviders provider) => For(provider).ValidatingAPIKey("  ") is null;

    [TestCase(LLMProviders.OPEN_AI)]
    [TestCase(LLMProviders.SELF_HOSTED)]
    public void AKeyWhichCannotBeSentIsRejected(LLMProviders provider)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(For(provider).ValidatingAPIKey($"sk-proj{ZERO_WIDTH_SPACE}AbC123"), Is.Not.Null);
            Assert.That(For(provider).ValidatingAPIKey("sk-proj“AbC123”"), Is.Not.Null);
            Assert.That(For(provider).ValidatingAPIKey("sk-proj AbC123"), Is.Not.Null);
        }
    }

    [TestCase(LLMProviders.OPEN_AI)]
    [TestCase(LLMProviders.SELF_HOSTED)]
    public void WhitespaceAroundTheKeyIsFine(LLMProviders provider) => Assert.That(For(provider).ValidatingAPIKey(" sk-proj-AbC123\n"), Is.Null);

    [TestCase(LLMProviders.OPEN_AI)]
    [TestCase(LLMProviders.SELF_HOSTED)]
    public void AStorageIssueComesFirst(LLMProviders provider) => Assert.That(For(provider, "The keyring is locked.").ValidatingAPIKey("sk-proj-AbC123"), Is.EqualTo("The keyring is locked."));
}