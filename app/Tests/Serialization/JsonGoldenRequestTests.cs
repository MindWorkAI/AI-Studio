using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Writes requests the way AI Studio sends them, and checks that they get sent just as before.
/// </summary>
/// <remarks>
/// The files in the folder Fixtures were written with the contracts of reflection, before AI Studio got
/// contracts of its own. A provider or the runtime reads every byte of a request, and one which got written
/// otherwise, e.g., with a field under another name or without the tools, gets refused or misread. The requests
/// are made up, see <see cref="JsonSampleRequests"/>.
/// </remarks>
[TestFixture]
public sealed class JsonGoldenRequestTests
{
    private static IEnumerable<JsonGoldenRequest> GoldenRequests()
    {
        TestHost.PrepareStaticState();
        yield return new("chat-completion-request.json", JsonSampleRequests.ChatCompletion, ProviderJsonOptions.OPTIONS);
        yield return new("responses-request.json", JsonSampleRequests.Responses, ProviderJsonOptions.OPTIONS);
        yield return new("anthropic-messages-request.json", () => JsonSampleRequests.Anthropic(), ProviderJsonOptions.OPTIONS);
        yield return new("embedding-request.json", JsonSampleRequests.Embedding, ProviderJsonOptions.OPTIONS);
        yield return new("google-embedding-request.json", JsonSampleRequests.GoogleEmbedding, ProviderJsonOptions.OPTIONS);

        // The requests to the runtime which were anonymous types once:
        yield return new("runtime-validate-tokenizer-request.json", () => new ValidateTokenizerRequest("/samples/tokenizer.json"), RustService.JSON_OPTIONS);
        yield return new("runtime-store-tokenizer-request.json", () => new StoreTokenizerRequest("model-a", "/samples/tokenizer.json"), RustService.JSON_OPTIONS);
        yield return new("runtime-delete-tokenizer-request.json", () => new DeleteTokenizerRequest("model-a"), RustService.JSON_OPTIONS);
        yield return new("runtime-count-tokens-request.json", () => new CountTokensRequest("Count these tokens.", "/samples/tokenizer.json"), RustService.JSON_OPTIONS);
        yield return new("runtime-prepare-image-request.json", () => new PrepareImageRequest("/samples/picture.png", true), RustService.JSON_OPTIONS);
        yield return new("runtime-prepare-image-for-model-request.json", () => new PrepareImageForModelRequest("/samples/picture.png", 5_242_880), RustService.JSON_OPTIONS);
    }

    [TestCaseSource(nameof(GoldenRequests))]
    public void ARequestIsSentAsBefore(JsonGoldenRequest goldenRequest)
    {
        var request = goldenRequest.Create();
        Assert.That(JsonSerializer.Serialize(request, request.GetType(), goldenRequest.Options), Is.EqualTo(JsonGoldenFileTests.ReadFixture(goldenRequest.FileName)));
    }
}