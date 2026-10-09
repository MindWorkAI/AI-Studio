using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that a plugin whose code never finishes can be stopped.
/// </summary>
/// <remarks>
/// Every plugin gets a time limit of its own while it is loaded and started, so that one plugin
/// cannot keep all the others from starting. That limit is only worth something when the Lua
/// runtime gives up on a cancelled token, even in the middle of a loop which never yields. If it
/// did not, a single plugin like that would hang the start of AI Studio for good.
/// </remarks>
[TestFixture]
public sealed class PluginTimeoutTests
{
    private const string ENDLESS_PLUGIN = """
                                          ID = "0b7e5c3a-4f1d-4e8b-9a6c-2d3f4e5a6b7c"
                                          while true do end
                                          """;

    [Test]
    public async Task ALoopWhichNeverEndsIsStopped()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var loading = Task.Run(() => PluginFactory.Load(null, ENDLESS_PLUGIN, cancellationToken: timeout.Token));

        var finishedInTime = await Task.WhenAny(loading, Task.Delay(TimeSpan.FromSeconds(10))) == loading;

        Assert.That(finishedInTime, Is.True, "The Lua runtime ignored the cancelled token.");
        Assert.That(async () => await loading, Throws.InstanceOf<OperationCanceledException>(), "Only a cancellation tells a time limit apart from a broken plugin.");
    }
}