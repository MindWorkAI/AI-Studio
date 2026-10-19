using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how long the answer of an ERI server to the check of its security requirements is used.
/// </summary>
/// <remarks>
/// One message checks the ERI data sources several times, and every check signs in. A server
/// which limits sign-ins would start refusing, so an answer is kept for a minute. A server which
/// did not answer is asked again sooner, and a check which is still running is joined.
/// </remarks>
[TestFixture]
public sealed class DataSourceSecurityCheckCacheTests
{
    [Test]
    public void ARunningCheckIsJoined()
    {
        var running = new TaskCompletionSource<(bool IsReachable, SecurityRequirements? Requirements)>();

        Assert.That(DataSourceService.IsSecurityCheckValid(running.Task, TimeSpan.FromMinutes(5)), Is.True, "Starting a second check would sign in a second time.");
    }

    [Test]
    public void AnAnswerIsUsedForOneMinute()
    {
        var answered = Task.FromResult<(bool IsReachable, SecurityRequirements? Requirements)>((true, null));

        Assert.Multiple(() =>
        {
            Assert.That(DataSourceService.IsSecurityCheckValid(answered, TimeSpan.FromSeconds(59)), Is.True, "The rounds of one answer share a single sign-in.");
            Assert.That(DataSourceService.IsSecurityCheckValid(answered, TimeSpan.FromSeconds(61)), Is.False, "The operator may change the requirements at any time.");
        });
    }

    [Test]
    public void AServerWithoutAnswerIsAskedAgainSooner()
    {
        var unanswered = Task.FromResult<(bool IsReachable, SecurityRequirements? Requirements)>((false, null));

        Assert.Multiple(() =>
        {
            Assert.That(DataSourceService.IsSecurityCheckValid(unanswered, TimeSpan.FromSeconds(14)), Is.True, "The rounds of one answer do not wait six seconds each for the same server.");
            Assert.That(DataSourceService.IsSecurityCheckValid(unanswered, TimeSpan.FromSeconds(16)), Is.False, "The server may well be back.");
        });
    }

    [Test]
    public void AFailedCheckIsNeverUsed()
    {
        var failed = Task.FromException<(bool IsReachable, SecurityRequirements? Requirements)>(new InvalidOperationException());

        Assert.That(DataSourceService.IsSecurityCheckValid(failed, TimeSpan.Zero), Is.False);
    }
}