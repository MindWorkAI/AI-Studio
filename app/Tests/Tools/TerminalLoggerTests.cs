using System.Globalization;

using AIStudio.Tools;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Pins the format in which log events travel to the Rust runtime.
/// </summary>
/// <remarks>
/// The Rust runtime reads this format to log each event with the time it happened. Its tests in
/// runtime/src/log.rs parse the same text, so both sides have to change together.
/// </remarks>
[TestFixture]
public sealed class TerminalLoggerTests
{
    private const string TRANSPORT_TIMESTAMP = "2026-10-09T17:53:54.5629130+00:00";

    private static readonly DateTimeOffset EVENT_TIME = new DateTimeOffset(2026, 10, 9, 17, 53, 54, TimeSpan.Zero).AddTicks(5_629_130);

    [Test]
    public void TheTransportTimestampIsPinned()
    {
        Assert.That(TerminalLogger.FormatTransportTimestamp(EVENT_TIME), Is.EqualTo(TRANSPORT_TIMESTAMP));
    }

    [Test]
    public void TheTransportTimestampIgnoresTheCurrentCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.DateTimeFormat.TimeSeparator = ".";

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            Assert.Multiple(() =>
            {
                Assert.That(EVENT_TIME.ToString("yyyy-MM-dd HH:mm:ss.fff"), Is.EqualTo("2026-10-09 17.53.54.562"), "The format sent before followed the time separator of the current culture.");
                Assert.That(TerminalLogger.FormatTransportTimestamp(EVENT_TIME), Is.EqualTo(TRANSPORT_TIMESTAMP));
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}