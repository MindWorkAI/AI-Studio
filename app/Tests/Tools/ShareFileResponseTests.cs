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
/// a share sheet which did not respond would go unnoticed again. The answers are read with the options
/// of RustService itself.
/// </remarks>
[TestFixture]
public sealed class ShareFileResponseTests
{
    [TestCase("opened", ShareSheetOutcome.OPENED)]
    [TestCase("failed", ShareSheetOutcome.FAILED)]
    [TestCase("unresponsive", ShareSheetOutcome.UNRESPONSIVE)]
    public void TheAppReadsEveryOutcomeOfTheRuntime(string outcome, ShareSheetOutcome expectedOutcome)
    {
        var response = JsonSerializer.Deserialize<ShareFileResponse>($$"""{"outcome":"{{outcome}}","issue":"The main window is not available."}""", RustService.JSON_OPTIONS);

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
        var response = JsonSerializer.Deserialize<ShareFileResponse>("""{"outcome":"vanished","issue":""}""", RustService.JSON_OPTIONS);

        Assert.That(response?.Outcome, Is.EqualTo(ShareSheetOutcome.UNKNOWN));
    }
}