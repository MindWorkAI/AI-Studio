using System.Globalization;

using AIStudio.Chat;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks how a number of tokens is written under the input field.
/// </summary>
/// <remarks>
/// The culture is an argument rather than something taken from the machine, and that is the point
/// being checked as much as the digits are: AI Studio's language is chosen in its own settings, so
/// the thread's culture says nothing about which separators a person expects to read.
/// </remarks>
[TestFixture]
public sealed class TokenAmountTests
{
    private static readonly CultureInfo AMERICAN = CultureInfo.GetCultureInfo("en-US");

    private static readonly CultureInfo GERMAN = CultureInfo.GetCultureInfo("de-DE");

    [TestCase(0, "0")]
    [TestCase(7, "7")]
    [TestCase(847, "847")]
    [TestCase(999, "999", Description = "The last number written out in full.")]
    [TestCase(1_000, "1.00k")]
    [TestCase(1_234, "1.23k")]
    [TestCase(12_347, "12.35k")]
    [TestCase(128_000, "128.00k")]
    [TestCase(400_000, "400.00k")]
    [TestCase(999_499, "999.50k")]
    [TestCase(999_999, "1.00M", Description = "Rounded before the unit is chosen, so it does not read as 1,000.00k.")]
    [TestCase(1_000_000, "1.00M")]
    [TestCase(1_048_576, "1.05M")]
    [TestCase(1_050_000, "1.05M", Description = "Which is how OpenAI writes it themselves.")]
    [TestCase(2_000_000, "2.00M")]
    public void ANumberOfTokensIsWrittenTheWayItIsRead(int tokens, string wanted)
    {
        Assert.That(TokenAmount.Format(tokens, AMERICAN), Is.EqualTo(wanted));
    }

    [TestCase(999, "999")]
    [TestCase(1_234, "1,23k")]
    [TestCase(400_000, "400,00k")]
    [TestCase(1_048_576, "1,05M")]
    public void TheSeparatorsAreTheOnesTheUserKnows(int tokens, string wanted)
    {
        //
        // A German reads 1,23k where an American reads 1.23k. Writing either of them the other way
        // around reads as a number a thousand times off.
        //
        Assert.That(TokenAmount.Format(tokens, GERMAN), Is.EqualTo(wanted));
    }
}
