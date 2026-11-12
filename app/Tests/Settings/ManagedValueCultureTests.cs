using System.Globalization;

using AIStudio.Settings;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks that the stored form of a managed setting does not depend on the region settings of the computer.
/// </summary>
/// <remarks>
/// The settings keep the last value an organization applied as an editable default in this form, and compare it with
/// the current value to see whether the user changed it. A form which followed the culture would make the value look
/// changed once the computer, or its region settings, changed. All cases run under de-DE, which writes a decimal comma.
/// </remarks>
[TestFixture]
[SetCulture("de-DE")]
public sealed class ManagedValueCultureTests
{
    [Test]
    public void TheCultureWritesADecimalComma()
    {
        Assert.That(1.5.ToString(CultureInfo.CurrentCulture), Is.EqualTo("1,5"), "Without a decimal comma, the other cases check nothing.");
    }

    [Test]
    public void TheItemsOfAListAreWrittenTheSameOnEveryComputer()
    {
        Assert.That(ManagedConfiguration.SerializeManagedScalarValue(new List<double> { 2.25, 1.5 }), Is.EqualTo("1.5;2.25"));
    }

    [Test]
    public void TheKeysAndValuesOfATableAreWrittenTheSameOnEveryComputer()
    {
        var table = new Dictionary<int, double> { [2] = 0.5, [10] = 1.25 };

        Assert.That(ManagedConfiguration.SerializeManagedScalarValue(table), Is.EqualTo("10:1.25;2:0.5"));
    }

    [Test]
    public void WholeNumbersKeepTheFormTheyWereStoredInBefore()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ManagedConfiguration.SerializeManagedScalarValue(new List<int> { 12, 3 }), Is.EqualTo("12;3"));
            Assert.That(ManagedConfiguration.SerializeManagedScalarValue(new Dictionary<int, string> { [1] = "one" }), Is.EqualTo("1:one"));
        }
    }
}