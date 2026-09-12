using AIStudio.Models;
using AIStudio.Models.Registry;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks which tokenizer the rules name for a model.
/// </summary>
/// <remarks>
/// Naming one changes nothing about the counting today: AI Studio counts with the tokenizer it
/// ships unless somebody points it at a tokenizer.json file, and none of the references below is
/// such a file. What they are for is the sentence in the provider dialog, which until now let a
/// person guess -- including the ones who go looking for a file which was never published.
///
/// The kind matters as much as the name, and that is what the cases here pin. "o200k_base" is an
/// encoding nobody can download, "/v1/messages/count_tokens" is an endpoint nobody can select in a
/// file dialog, and telling them apart is the whole point of recording the kind alongside the name.
/// </remarks>
[TestFixture]
public sealed class TokenizerRuleTests
{
    [TestCase(LLMProviders.OPEN_AI, "gpt-5.1", "o200k_base")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-5.6", "o200k_base", Description = "Every model of the 5 line inherits the encoding of its prefix.")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-5-chat-latest", "o200k_base")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-4o", "o200k_base")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-4o-mini-search-preview", "o200k_base", Description = "Stated in full rather than inherited, so it has to say this itself.")]
    [TestCase(LLMProviders.OPEN_AI, "o1", "o200k_base")]
    [TestCase(LLMProviders.OPEN_AI, "o1-mini", "o200k_base")]
    [TestCase(LLMProviders.OPEN_AI, "o3-mini", "o200k_base")]
    [TestCase(LLMProviders.OPEN_AI, "o4-mini", "o200k_base")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-4", "cl100k_base", Description = "The older encoding, which is where the 4 line stayed.")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-4-turbo", "cl100k_base")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-3.5-turbo", "cl100k_base")]
    public void OpenAINamesAnEncodingRatherThanAFile(LLMProviders provider, string modelId, string encoding)
    {
        var tokenizer = provider.GetModelProfile(new Model(modelId, null)).Tokenizer;

        Assert.Multiple(() =>
        {
            Assert.That(tokenizer.Kind, Is.EqualTo(TokenizerKind.TIKTOKEN));
            Assert.That(tokenizer.Id, Is.EqualTo(encoding));
        });
    }

    [TestCase(LLMProviders.ANTHROPIC, "claude-opus-5", "/v1/messages/count_tokens")]
    [TestCase(LLMProviders.ANTHROPIC, "claude-3-5-haiku-latest", "/v1/messages/count_tokens")]
    [TestCase(LLMProviders.GOOGLE, "gemini-3-pro", "countTokens")]
    [TestCase(LLMProviders.GOOGLE, "gemini-2.5-flash-lite", "countTokens")]
    public void AnthropicAndGoogleNameAnEndpointBecauseTheyPublishNoFile(LLMProviders provider, string modelId, string endpoint)
    {
        var tokenizer = provider.GetModelProfile(new Model(modelId, null)).Tokenizer;

        Assert.Multiple(() =>
        {
            Assert.That(tokenizer.Kind, Is.EqualTo(TokenizerKind.PROVIDER_API));
            Assert.That(tokenizer.Id, Is.EqualTo(endpoint));
        });
    }

    [TestCase(LLMProviders.OPEN_AI, "gpt-6-astra", Description = "Newer than the mapping OpenAI publishes, so nothing is claimed for it.")]
    [TestCase(LLMProviders.OPEN_AI, "gpt-oss-120b", Description = "Open weights, and not in OpenAI's encoding table either.")]
    [TestCase(LLMProviders.SELF_HOSTED, "some-model-nobody-wrote-a-rule-for")]
    [TestCase(LLMProviders.MISTRAL, "mistral-large-2512")]
    public void AModelNobodyNamedATokenizerForSaysSo(LLMProviders provider, string modelId)
    {
        var tokenizer = provider.GetModelProfile(new Model(modelId, null)).Tokenizer;

        Assert.Multiple(() =>
        {
            Assert.That(tokenizer.IsKnown, Is.False);
            Assert.That(tokenizer.Kind, Is.EqualTo(TokenizerKind.UNKNOWN), "Which means the built-in tokenizer, the same as today.");
        });
    }

    [Test]
    public void TheSameModelThroughAGatewayKeepsItsTokenizer()
    {
        //
        // A gateway cuts what its transport cannot carry, which is about APIs. Which tokenizer a
        // model was trained with is a property of the model and survives the trip.
        //
        var directly = ModelRegistry.Shared.Profile(LLMProviders.OPEN_AI, "gpt-5.1");
        var throughAGateway = ModelRegistry.Shared.Profile(LLMProviders.OPEN_ROUTER, "openai/gpt-5.1");

        Assert.That(throughAGateway.Tokenizer, Is.EqualTo(directly.Tokenizer));
    }

    [Test]
    public void ANameWithoutAKindIsNotAReference()
    {
        //
        // Both halves have to be there. A kind without a name says nothing to act on, and a name
        // without a kind cannot be told apart from any other string -- whether it is a repository,
        // an encoding or an endpoint decides what a person can do with it.
        //
        Assert.Multiple(() =>
        {
            Assert.That(new TokenizerRef(TokenizerKind.HUGGING_FACE, string.Empty).IsKnown, Is.False);
            Assert.That(new TokenizerRef(TokenizerKind.UNKNOWN, "o200k_base").IsKnown, Is.False);
            Assert.That(TokenizerRef.UNKNOWN.IsKnown, Is.False);
            Assert.That(new TokenizerRef(TokenizerKind.TIKTOKEN, "o200k_base").IsKnown, Is.True);
        });
    }
}