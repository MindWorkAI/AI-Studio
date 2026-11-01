using System.Text.Json;

using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that the app reads every outcome which the runtime sends after a share request.
/// </summary>
/// <remarks>
/// ShareFileResponse in runtime/src/share_sheet.rs names the outcome in snake case. A name which the
/// app does not know would not fail anywhere: the enum converter falls back to the first member, and
/// a share sheet which did not respond would go unnoticed again. The options mirror what RustService
/// deserializes with.
/// </remarks>
[TestFixture]
public sealed class ShareFileResponseTests
{
    private static readonly JsonSerializerOptions AS_THE_RUST_SERVICE_READS_IT = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new RustEnumConverter(),
        },
    };

    [TestCase("opened", ShareSheetOutcome.OPENED)]
    [TestCase("failed", ShareSheetOutcome.FAILED)]
    [TestCase("unresponsive", ShareSheetOutcome.UNRESPONSIVE)]
    public void TheAppReadsEveryOutcomeOfTheRuntime(string outcome, ShareSheetOutcome expectedOutcome)
    {
        var response = JsonSerializer.Deserialize<ShareFileResponse>($$"""{"outcome":"{{outcome}}","issue":"The main window is not available."}""", AS_THE_RUST_SERVICE_READS_IT);

        Assert.That(response, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(response!.Outcome, Is.EqualTo(expectedOutcome));
            Assert.That(response.Issue, Is.EqualTo("The main window is not available."));
        }
    }

    [Test]
    public void AnUnknownOutcomeNeverCountsAsAnOpenedShareSheet()
    {
        var response = JsonSerializer.Deserialize<ShareFileResponse>("""{"outcome":"vanished","issue":""}""", AS_THE_RUST_SERVICE_READS_IT);

        Assert.That(response?.Outcome, Is.EqualTo(ShareSheetOutcome.UNKNOWN));
    }
}