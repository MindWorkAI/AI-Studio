using AIStudio.Models;

namespace AIStudio.Tests.Models;

/// <summary>
/// Checks the three types which have to be able to say "nobody knows".
/// </summary>
/// <remarks>
/// They are tested together because they are tested for the same thing. Each of them is a value
/// type sitting inside a model profile, so each of them has a default value somebody will read
/// before anything was written into it, and that default has to mean unknown rather than zero. The
/// day one of them answers "a context window of zero tokens" instead, a feature built on top of it
/// will quietly do the wrong thing.
/// </remarks>
[TestFixture]
public sealed class ModelFactsTests
{
    [Test]
    public void AContextWindowNobodyWroteDownIsUnknown()
    {
        ContextWindow untouched = default;

        Assert.Multiple(() =>
        {
            Assert.That(untouched.IsKnown, Is.False);
            Assert.That(untouched, Is.EqualTo(ContextWindow.UNKNOWN));
        });
    }

    [Test]
    public void AContextWindowStatesWhatItShipsWithAndWhatItCanBeRaisedTo()
    {
        var window = ContextWindow.Of(128_000, 1_000_000);

        Assert.Multiple(() =>
        {
            Assert.That(window.IsKnown, Is.True);
            Assert.That(window.DefaultTokens, Is.EqualTo(128_000));
            Assert.That(window.RaisableToTokens, Is.EqualTo(1_000_000));
        });
    }

    [Test]
    public void AContextWindowWhichCannotBeRaisedSaysSoWithNothingRatherThanWithItsOwnSize()
    {
        var window = ContextWindow.Of(32_768);

        Assert.That(window.RaisableToTokens, Is.Null);
    }

    [Test]
    public void AContextWindowOfNoTokensCannotBeStated() => Assert.Throws<ArgumentOutOfRangeException>(() => ContextWindow.Of(0));

    [Test]
    public void AContextWindowCannotBeRaisedToLessThanItAlreadyIs() => Assert.Throws<ArgumentOutOfRangeException>(() => ContextWindow.Of(128_000, 32_768));

    [Test]
    public void ATokenizerNobodyWroteDownIsTheBuiltInOne()
    {
        TokenizerRef untouched = default;

        Assert.Multiple(() =>
        {
            Assert.That(untouched.IsKnown, Is.False);
            Assert.That(untouched.Kind, Is.EqualTo(TokenizerKind.UNKNOWN));
        });
    }

    [Test]
    public void ATokenizerWithoutANameIsNotKnownEvenWhenItsKindIs()
    {
        var nameless = new TokenizerRef(TokenizerKind.HUGGING_FACE, string.Empty);

        Assert.That(nameless.IsKnown, Is.False);
    }

    [Test]
    public void ImageLimitsNobodyWroteDownAreUnknown()
    {
        ImageLimits untouched = default;

        Assert.Multiple(() =>
        {
            Assert.That(untouched.IsKnown, Is.False);
            Assert.That(untouched.MaxPerMessage, Is.Null);
            Assert.That(untouched.MaxPerRequest, Is.Null);
        });
    }

    [Test]
    public void ImageLimitsTellNoImagesApartFromNobodyHavingSaid()
    {
        var noImages = new ImageLimits(MaxPerMessage: 0, MaxPerRequest: null);

        Assert.Multiple(() =>
        {
            Assert.That(noImages.IsKnown, Is.True, "Zero images is a statement an operator can make.");
            Assert.That(noImages.MaxPerMessage, Is.EqualTo(0));
        });
    }
}