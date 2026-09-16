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
    [TestCase(1, 100, false)]
    [TestCase(100, 100, false, Description = "Exactly the limit still fits. It is a maximum, not a threshold.")]
    [TestCase(101, 100, true)]
    [TestCase(3_601, 3_600, true)]
    public void TooManyPicturesIsAQuestionOfTheNumberTheVendorStated(int images, int allowed, bool tooMany)
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            UncountedImages = images,
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
            UncountedImages = 500,
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
            UncountedImages = 20,
            ImageLimits = new ImageLimits(8, 100),
        };

        Assert.Multiple(() =>
        {
            Assert.That(counted.TooManyImages, Is.True);
            Assert.That(counted.ImageLimits.MaxInOneMessage, Is.EqualTo(8));
        });
    }

    [Test]
    public void AConversationWithoutPicturesNeverComplainsAboutThem()
    {
        var counted = new ConversationTokens
        {
            IsKnown = true,
            UncountedImages = 0,
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
}