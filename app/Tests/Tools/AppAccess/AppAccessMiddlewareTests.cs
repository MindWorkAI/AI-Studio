using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.AppAccess;

/// <summary>
/// Checks which requests the app server lets through to the app.
/// </summary>
/// <remarks>
/// A request which a script sends carries the origin of its page. Only the pages of the app may send
/// requests to the app server: a page from anywhere else, and a sandboxed iframe with its origin
/// "null", get refused.
/// </remarks>
[TestFixture]
public sealed class AppAccessMiddlewareTests
{
    private const int APP_PORT = 61153;

    /// <summary>
    /// The status a request gets when it passes the middleware.
    /// </summary>
    private const int PASSED = StatusCodes.Status204NoContent;

    private static async Task<int> SendAsync(params string[] origins)
    {
        var middleware = new AppAccessMiddleware(Pass, new AppAccessPolicy(APP_PORT), NullLogger<AppAccessMiddleware>.Instance);
        var context = new DefaultHttpContext { Request = { Method = HttpMethods.Post, Path = "/_blazor/negotiate" } };
        if (origins.Length > 0)
            context.Request.Headers.Origin = origins;

        await middleware.InvokeAsync(context);
        return context.Response.StatusCode;
    }

    private static Task Pass(HttpContext context)
    {
        context.Response.StatusCode = PASSED;
        return Task.CompletedTask;
    }

    [Test]
    public async Task ARequestWithoutAnOriginPasses()
    {
        Assert.That(await SendAsync(), Is.EqualTo(PASSED));
    }

    [TestCase("http://127.0.0.1:61153")]
    [TestCase("http://localhost:61153")]
    [TestCase("http://[::1]:61153")]
    public async Task ARequestFromTheAppPasses(string origin)
    {
        Assert.That(await SendAsync(origin), Is.EqualTo(PASSED));
    }

    [TestCase("http://rebind.example:61153")]
    [TestCase("null")]
    [TestCase("http://127.0.0.1:61154")]
    [TestCase("https://127.0.0.1:61153")]
    [TestCase("http://127.0.0.1")]
    public async Task ARequestFromAnotherOriginIsRefused(string origin)
    {
        Assert.That(await SendAsync(origin), Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task ARequestWithSeveralOriginsIsRefused()
    {
        Assert.That(await SendAsync("http://127.0.0.1:61153", "http://rebind.example:61153"), Is.EqualTo(StatusCodes.Status403Forbidden));
    }
}