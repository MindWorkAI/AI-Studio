using System.Globalization;
using System.Text;

using SharedTools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that AppendLineLF ends every line with LF, whatever the system.
/// </summary>
/// <remarks>
/// StringBuilder.AppendLine writes CR LF on Windows. These tests would show it there, while on macOS and Linux they
/// pass with both.
/// </remarks>
[TestFixture]
public sealed class StringBuilderExtensionsTests
{
    [Test]
    public void EveryKindOfLineEndsWithLf()
    {
        var number = 4711;
        var text = new StringBuilder()
            .AppendLineLF("Erste Zeile")
            .AppendLineLF()
            .AppendLineLF($"Nummer {number}")
            .AppendLineLF(null)
            .ToString();

        Assert.That(text, Is.EqualTo("Erste Zeile\n\nNummer 4711\n\n"));
    }

    [Test]
    public void AnInterpolatedTextGoesIntoTheSameBuilderInItsPlace()
    {
        var value = 1.5;
        var builder = new StringBuilder("Anfang ");

        var returned = builder.AppendLineLF($"{value.ToString(CultureInfo.InvariantCulture)} und {"Text"}").Append("Ende");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(returned, Is.SameAs(builder));
            Assert.That(builder.ToString(), Is.EqualTo("Anfang 1.5 und Text\nEnde"));
        }
    }
}