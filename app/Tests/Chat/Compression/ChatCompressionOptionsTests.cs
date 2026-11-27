using AIStudio.Chat.Compression;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tests.Chat.Compression;

/// <summary>
/// Checks how the settings of compressing a chat are read for one decision or one run.
/// </summary>
/// <remarks>
/// A configuration plugin can only set values within their ranges, but the settings file can hold any value somebody
/// wrote into it by hand. Such a value must not make AI Studio ask before every message, or never.
/// </remarks>
[TestFixture]
public sealed class ChatCompressionOptionsTests
{
    [Test]
    public void TheDefaultsKeepTheBehaviorOfEarlierVersions()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatCompressionOptions.DEFAULT.ShareToAsk, Is.EqualTo(0.95));
            Assert.That(ChatCompressionOptions.DEFAULT.MaxSummaryTokens, Is.EqualTo(8_192));
            Assert.That(ChatCompressionOptions.DEFAULT.PreselectedFocus, Is.Empty);
            Assert.That(ChatCompressionOptions.DEFAULT.WhenContextWindowFills, Is.EqualTo(ContextWindowFullBehavior.ASK));
        }
    }

    [TestCase(80, 0.8)]
    [TestCase(10, 0.5)]
    [TestCase(200, 0.95)]
    public void TheShareStaysWithinItsRange(int percent, double expected)
    {
        var settings = new DataChatCompression { ContextWindowThresholdPercent = percent };

        Assert.That(ChatCompressionOptions.Of(settings).ShareToAsk, Is.EqualTo(expected).Within(1e-9));
    }

    [TestCase(4_096, 4_096)]
    [TestCase(100, DataChatCompression.MIN_SUMMARY_TOKENS)]
    [TestCase(1_000_000, DataChatCompression.MAX_SUMMARY_TOKENS)]
    public void TheLengthOfTheSummaryStaysWithinItsRange(int maxSummaryTokens, int expected)
    {
        var settings = new DataChatCompression { MaxSummaryTokens = maxSummaryTokens };

        Assert.That(ChatCompressionOptions.Of(settings).MaxSummaryTokens, Is.EqualTo(expected));
    }

    [Test]
    public void TheFocusAndTheBehaviorAreTakenAsTheyAre()
    {
        var settings = new DataChatCompression { PreselectedFocus = "Keep every figure.", ContextWindowFullBehavior = ContextWindowFullBehavior.COMPRESS_AND_REPLACE };
        var options = ChatCompressionOptions.Of(settings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.PreselectedFocus, Is.EqualTo("Keep every figure."));
            Assert.That(options.WhenContextWindowFills, Is.EqualTo(ContextWindowFullBehavior.COMPRESS_AND_REPLACE));
        }
    }
}