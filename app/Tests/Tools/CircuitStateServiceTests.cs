using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks when the circuit state reports that the browser connection came back.
/// </summary>
/// <remarks>
/// The report is there to fetch again what the browser sent while the connection was down, because Blazor
/// drops it. The layout reads the color theme anew on it, for instance, since the machine may have switched
/// its theme during sleep. So it has to come after every loss, and only then: missing one leaves the app
/// with stale state, while a connection which was never lost has nothing to fetch again.
/// </remarks>
[TestFixture]
public sealed class CircuitStateServiceTests
{
    [Test]
    public void AConnectionWhichReturnsAfterALossIsReportedOnce()
    {
        var circuitState = new CircuitStateService();
        var numReports = 0;
        circuitState.ConnectionRestored += () => numReports++;

        circuitState.MarkAsDisconnected();
        circuitState.MarkAsConnected();

        Assert.That(numReports, Is.EqualTo(1), "The connection was lost and came back, so whatever the browser sent in between is gone.");
        Assert.That(circuitState.IsConnected, Is.True);
    }

    [Test]
    public void TheFirstConnectionIsNotReported()
    {
        var circuitState = new CircuitStateService();
        var numReports = 0;
        circuitState.ConnectionRestored += () => numReports++;

        circuitState.MarkAsConnected();

        Assert.That(numReports, Is.Zero, "A circuit starts out connected, so its first connection has not lost anything.");
    }

    [Test]
    public void AConnectionWhichWasNotLostIsNotReportedAgain()
    {
        var circuitState = new CircuitStateService();
        var numReports = 0;
        circuitState.ConnectionRestored += () => numReports++;

        circuitState.MarkAsDisconnected();
        circuitState.MarkAsConnected();
        circuitState.MarkAsConnected();

        Assert.That(numReports, Is.EqualTo(1), "The second call follows a connection which was up all along.");
    }

    [Test]
    public void EveryLossIsReportedOnItsOwn()
    {
        var circuitState = new CircuitStateService();
        var numReports = 0;
        circuitState.ConnectionRestored += () => numReports++;

        circuitState.MarkAsDisconnected();
        circuitState.MarkAsConnected();
        circuitState.MarkAsDisconnected();
        circuitState.MarkAsConnected();

        Assert.That(numReports, Is.EqualTo(2), "The machine went to sleep twice, and each time something may have been lost.");
    }
}