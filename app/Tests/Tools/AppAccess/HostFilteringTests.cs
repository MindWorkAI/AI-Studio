using AIStudio.Tools.Security;

using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AIStudio.Tests.Tools.AppAccess;

/// <summary>
/// Checks which host names the app server answers to.
/// </summary>
/// <remarks>
/// A website may point a name of its own at the loopback address. The browser then sends that name
/// as the host, and the app server must refuse the request. The test runs the host filtering of
/// ASP.NET Core with the options the app configures.
/// </remarks>
[TestFixture]
public sealed class HostFilteringTests
{
    /// <summary>
    /// The status a request gets when it passes the filter.
    /// </summary>
    private const int PASSED = StatusCodes.Status204NoContent;

    private static async Task<int> SendAsync(string host)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .Configure<HostFilteringOptions>(AppAccessPolicy.ConfigureHostFiltering)
            .BuildServiceProvider();

        var middleware = ActivatorUtilities.CreateInstance<HostFilteringMiddleware>(services, (RequestDelegate)Pass);
        var context = new DefaultHttpContext { Request = { Host = new HostString(host) } };

        await middleware.Invoke(context);
        return context.Response.StatusCode;
    }

    private static Task Pass(HttpContext context)
    {
        context.Response.StatusCode = PASSED;
        return Task.CompletedTask;
    }

    [TestCase("127.0.0.1:61153")]
    [TestCase("localhost:61153")]
    [TestCase("[::1]:61153")]
    public async Task TheLoopbackNamesPass(string host)
    {
        Assert.That(await SendAsync(host), Is.EqualTo(PASSED));
    }

    [TestCase("rebind.example:61153")]
    [TestCase("127.0.0.1.rebind.example:61153")]
    [TestCase("localhost.rebind.example")]
    public async Task AnyOtherNameIsRefused(string host)
    {
        Assert.That(await SendAsync(host), Is.EqualTo(StatusCodes.Status400BadRequest));
    }

    [Test]
    public async Task ARequestWithoutAHostIsRefused()
    {
        Assert.That(await SendAsync(string.Empty), Is.EqualTo(StatusCodes.Status400BadRequest));
    }
}