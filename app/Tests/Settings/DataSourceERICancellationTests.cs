using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;
using AIStudio.Tools.RAG;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using ChatThread = AIStudio.Chat.ChatThread;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks what a search of an ERI data source does when it is cancelled, and when its server fails.
/// </summary>
/// <remarks>
/// The ERI client answers a cancellation like any other failure, because its other callers pass a
/// timeout of their own and expect an answer. A search the user cancelled has to stop, though,
/// rather than report the data source as not searched and carry on. A server which fails without
/// anybody cancelling still makes the data source count as not searched.
/// </remarks>
[TestFixture]
public sealed class DataSourceERICancellationTests
{
    // Nothing listens on port 1 of the own machine, so a request fails right away:
    private static readonly DataSourceERI_V1 UNREACHABLE = new()
    {
        Id = "33333333-3333-3333-3333-333333333333",
        Name = "Intranet",
        Type = DataSourceType.ERI_V1,
        Hostname = "http://127.0.0.1",
        Port = 1,
        AuthMethod = AuthMethod.NONE,
        SecurityPolicy = DataSourceSecurity.ALLOW_ANY,
        MaxMatches = 10,
    };

    private RustService rustService = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void PrepareServices()
    {
        this.rustService = new RustService("1", "unused");
        var settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, this.rustService);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection()
            .AddSingleton(this.rustService)
            .AddSingleton(settingsManager)
            .AddLogging()
            .BuildServiceProvider();

        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
        this.rustService.Dispose();
    }

    [Test]
    public async Task ACancelledSearchStops()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(async () => await UNREACHABLE.RetrieveDataAsync("query", 1, new ChatThread(), cancellation.Token), "Reporting the data source as not searched would carry on after the user cancelled.");
    }

    [Test]
    public async Task AFailingServerMakesTheDataSourceCountAsNotSearched()
    {
        var page = await UNREACHABLE.RetrieveDataAsync("query", 1, new ChatThread());

        Assert.Multiple(() =>
        {
            Assert.That(page.Contexts, Is.Empty);
            Assert.That(page.Gaps, Is.EqualTo(new[] { RetrievalGap.NOT_SEARCHED }), "Nobody cancelled, so the model learns that this data source is missing rather than empty.");
        });
    }
}