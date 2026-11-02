using System.Text.Json;

using AIStudio.Provider;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Writes requests the way AI Studio sends them, and checks that they get sent just as before.
/// </summary>
/// <remarks>
/// The files in the folder Fixtures were written with the contracts of reflection, before AI Studio got
/// contracts of its own. A provider reads every byte of a request, and one which got written otherwise, e.g.,
/// with a field under another name or without the tools, gets refused or misread. The requests are made up,
/// see <see cref="JsonSampleRequests"/>.
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
    }

    [TestCaseSource(nameof(GoldenRequests))]
    public void ARequestIsSentAsBefore(JsonGoldenRequest goldenRequest)
    {
        var request = goldenRequest.Create();
        Assert.That(JsonSerializer.Serialize(request, request.GetType(), goldenRequest.Options), Is.EqualTo(JsonGoldenFileTests.ReadFixture(goldenRequest.FileName)));
    }
}