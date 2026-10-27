using System.Globalization;

using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

public sealed class LongExtensionsTests
{
    [TestCase(0L, "0 B")]
    [TestCase(1_023L, "1023 B")]
    [TestCase(1_536L, "1.5 kB")]
    [TestCase(1_992_294L, "1.9 MB")]
    [TestCase(1_073_741_824L, "1 GB")]
    [TestCase(1_572_864_000L, "1.46 GB")]
    public void FileSizesKeepTheirFraction(long sizeBytes, string expected)
    {
        Assert.That(sizeBytes.FileSize(CultureInfo.InvariantCulture), Is.EqualTo(expected));
    }

    [Test]
    public void FileSizesUseTheDecimalSeparatorOfTheCulture()
    {
        Assert.That(1_992_294L.FileSize(CultureInfo.GetCultureInfo("de-DE")), Is.EqualTo("1,9 MB"));
    }
}