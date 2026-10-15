using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks that indexing waits for the plugins before its first run, but never for good.
/// </summary>
/// <remarks>
/// The embedding providers of an organization come with its configuration plugin. Right after a
/// backup was restored, the settings do not hold them yet, and a first run which starts before the
/// plugins were loaded finds the data sources of the user without their embedding provider. Should
/// loading fail, though, waiting for it must not keep every data source from being indexed.
/// </remarks>
[TestFixture]
public sealed class FirstPluginLoadTests
{
    [Test]
    public async Task TheFirstRunWaitsUntilThePluginsWereLoaded()
    {
        var firstLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wait = DataSourceEmbeddingService.WaitForFirstPluginLoadAsync(firstLoad.Task, TimeSpan.FromMinutes(1), CancellationToken.None);

        await Task.Delay(50);
        Assert.That(wait.IsCompleted, Is.False, "The plugins are still loading.");

        firstLoad.SetResult();
        Assert.That(await wait, Is.True);
    }

    [Test]
    public async Task TheFirstRunStartsAnywayWhenLoadingThePluginsTakesTooLong()
    {
        var neverLoaded = new TaskCompletionSource();

        var loadedInTime = await DataSourceEmbeddingService.WaitForFirstPluginLoadAsync(neverLoaded.Task, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.That(loadedInTime, Is.False, "Loading may fail for good, and the data sources must be indexed all the same.");
    }

    [Test]
    public void StoppingAIStudioEndsTheWait()
    {
        var neverLoaded = new TaskCompletionSource();
        using var stopping = new CancellationTokenSource();
        stopping.Cancel();

        var waiting = DataSourceEmbeddingService.WaitForFirstPluginLoadAsync(neverLoaded.Task, TimeSpan.FromMinutes(1), stopping.Token);
        Assert.That(async () => await waiting, Throws.InstanceOf<OperationCanceledException>());
    }
}