using AIStudio.Chat;
using AIStudio.Models;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks what the chat says about the images a conversation carries.
/// </summary>
/// <remarks>
/// The visual briefing refuses a build with too many pictures, because that build is expensive and
/// fails late. A chat cannot refuse anything: the pictures are already in the conversation, and
/// taking them back out means deleting messages. So the chat says so instead, and what is checked
/// here is that it says so at the right moment -- and, more importantly, that it stays quiet when
/// nobody wrote a limit down.
/// </remarks>
[TestFixture]
public sealed class ConversationTokensTests
{
    private const double SHARE_TO_ASK = 0.95;

    [TestCase(1, 100, false)]
    [TestCase(100, 100, false, Description = "Exactly the limit still fits. It is a maximum, not a threshold.")]
    [TestCase(101, 100, true)]
    [TestCase(3_601, 3_600, true)]
    public void TooManyPicturesIsAQuestionOfTheNumberTheVendorStated(int images, int allowed, bool tooMany)
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            Images = images,
            ImageLimits = new ImageLimits(null, allowed),
        };

        Assert.That(counted.TooManyImages, Is.EqualTo(tooMany));
    }

    [Test]
    public void WithoutAStatedLimitThereIsNoSuchThingAsTooMany()
    {
        //
        // The common case. Most models are served at whatever their operator configured, and an app
        // which warned about the seventh picture would be inventing a ceiling nobody wrote.
        //
        var counted = new ConversationTokens
        {
            IsKnown = true,
            Images = 500,
            ImageLimits = ImageLimits.UNKNOWN,
        };

        Assert.That(counted.TooManyImages, Is.False);
    }

    [Test]
    public void TheSmallerOfTwoStatedLimitsIsTheOneWhichDecides()
    {
        //
        // A message is part of a request, so a conversation which fits the request limit can still
        // be too much for one message. Both are compared against the same number of pictures,
        // because a chat sends all of them in one message.
        //
        var counted = new ConversationTokens
        {
            IsKnown = true,
            Images = 20,
            ImageLimits = new ImageLimits(8, 100),
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(counted.TooManyImages, Is.True);
            Assert.That(counted.ImageLimits.MaxInOneMessage, Is.EqualTo(8));
        }
    }

    [Test]
    public void AConversationWithoutPicturesNeverComplainsAboutThem()
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            Images = 0,
            ImageLimits = new ImageLimits(null, 0),
        };

        Assert.That(counted.TooManyImages, Is.False, "Not even against a model which takes none at all.");
    }

    [Test]
    public void AnUnavailableCountClaimsNothingAboutPictures()
    {
        //
        // Nothing could be counted, so nothing is known -- including how many pictures travel. A
        // warning built on that would be made up.
        //
        Assert.That(ConversationTokens.UNAVAILABLE.TooManyImages, Is.False);
    }

    [Test]
    public void PicturesAProviderCountedAreNotCalledUncounted()
    {
        //
        // What the provider reported for the conversation so far includes its pictures, however
        // it charges them. Only those still waiting in the composer are left for nobody to count.
        //
        var reported = new ConversationTokens
        {
            IsKnown = true,
            HistoryIsReported = true,
            Images = 3,
            DraftImages = 1,
        };

        var estimated = reported with { HistoryIsReported = false };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reported.UncountedImages, Is.EqualTo(1), "The provider counted the two which were sent.");
            Assert.That(estimated.UncountedImages, Is.EqualTo(3), "Without a report, nobody counted any of them.");
        }
    }

    [Test]
    public void TooManyPicturesStaysTooManyWhenTheProviderCountedThem()
    {
        //
        // The limit is on how many pictures travel, not on what they cost. A provider which has
        // counted them still refuses the request which carries one too many.
        //
        var counted = new ConversationTokens
        {
            IsKnown = true,
            HistoryIsReported = true,
            Images = 10,
            ImageLimits = new ImageLimits(8, null),
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(counted.UncountedImages, Is.Zero);
            Assert.That(counted.TooManyImages, Is.True);
        }
    }

    [Test]
    public void TheWholeNumberIsTheConversationAndTheDraft()
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            HistoryTokens = 12_400,
            ToolTokens = 9_000,
            DraftTokens = 340,
        };

        Assert.That(counted.Tokens, Is.EqualTo(12_740), "The tools' share is part of the conversation, not added on top of it.");
    }

    [TestCase(5_000, 0, 10_000, false, TestName = "Below the share nothing is asked")]
    [TestCase(9_400, 99, 10_000, false, TestName = "Just below the share nothing is asked")]
    [TestCase(9_400, 100, 10_000, true, TestName = "The message about to be sent counts as well")]
    [TestCase(12_000, 0, 10_000, true, TestName = "Beyond the window it asks as well")]
    public void AsksBeforeSendingFromTheShareOn(int historyTokens, int draftTokens, int window, bool asks)
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            HistoryTokens = historyTokens,
            DraftTokens = draftTokens,
            Window = ContextWindow.Of(window),
        };

        Assert.That(counted.AsksBeforeSending(SHARE_TO_ASK), Is.EqualTo(asks));
    }

    [TestCase(5_999, 0.6, false, TestName = "Below a lower share nothing is asked")]
    [TestCase(6_000, 0.6, true, TestName = "A lower share asks earlier")]
    public void TheShareOfTheSettingsDecides(int historyTokens, double shareToAsk, bool asks)
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            HistoryTokens = historyTokens,
            Window = ContextWindow.Of(10_000),
        };

        Assert.That(counted.AsksBeforeSending(shareToAsk), Is.EqualTo(asks));
    }

    [Test]
    public void AnUnknownWindowIsNeverFull()
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            HistoryTokens = 1_000_000,
            Window = ContextWindow.UNKNOWN,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(counted.FillShare, Is.Zero, "A share of an unknown total would be a number made up on the spot.");
            Assert.That(counted.AsksBeforeSending(SHARE_TO_ASK), Is.False, "Nobody wrote the window down, so nobody can say that it is full.");
        }
    }
}