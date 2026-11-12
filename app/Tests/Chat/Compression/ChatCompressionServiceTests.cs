using AIStudio.Chat.Compression;
using AIStudio.Models;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks which models may compress a chat at all.
/// </summary>
/// <remarks>
/// Below the minimum, the compress button stays off and the notice before sending offers no
/// compressing, so a wrong answer here either hides the feature or offers one which always fails.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionServiceTests
{
    [Test]
    public void AWindowBelowTheMinimumCannotSummarize() => Assert.That(ChatCompressionService.CanSummarizeWith(ContextWindow.Of(ChatCompressionService.MIN_CONTEXT_WINDOW - 1)), Is.False);

    [Test]
    public void AWindowOfTheMinimumCanSummarize() => Assert.That(ChatCompressionService.CanSummarizeWith(ContextWindow.Of(ChatCompressionService.MIN_CONTEXT_WINDOW)), Is.True);

    [Test]
    public void AnUnknownWindowCanSummarize() => Assert.That(ChatCompressionService.CanSummarizeWith(ContextWindow.UNKNOWN), Is.True);
}