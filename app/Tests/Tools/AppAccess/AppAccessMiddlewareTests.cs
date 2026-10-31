using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.AppAccess;

/// <summary>
/// Checks which requests the app server lets through to the app.
/// </summary>
/// <remarks>
/// Only the app window has the session cookie, so every request without it gets refused, whatever
/// it asks for: pages, static files, the Blazor hub, the local files, and the preview of a visual
/// briefing alike. A request which a script sends also carries the origin of its page. Only the
/// pages of the app may send such requests: a page from anywhere else, and a sandboxed iframe with
/// its origin "null", get refused even with the cookie.
/// </remarks>
[TestFixture]
public sealed class AppAccessMiddlewareTests
{
    private const int APP_PORT = 61153;

    private const string UI_TOKEN = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// The status a request gets when it passes the middleware.
    /// </summary>
    private const int PASSED = StatusCodes.Status204NoContent;

    private static readonly string[] PATHS =
    [
        "/",
        "/chat",
        "/_blazor/negotiate",
        "/_blazor",
        "/_framework/blazor.web.js",
        "/local/file",
        "/visual-briefing/preview/0b6c1c43-6d2e-4f0b-9a35-6f6f3c1f2a10/6a4e7d6e-2a8c-4bb0-8c55-7a2b7b3c9e01",
        "/system/MudBlazor/MudBlazor.min.css",
        "/app.css",
        "/changelog/v26.10.1.md",
        "/does-not-exist"
    ];

    private static async Task<int> SendAsync(string path, string? sessionCookie, params string[] origins)
    {
        var middleware = new AppAccessMiddleware(Pass, new AppAccessPolicy(APP_PORT, UI_TOKEN), NullLogger<AppAccessMiddleware>.Instance);
        var context = new DefaultHttpContext { Request = { Method = HttpMethods.Get, Path = path } };
        if (sessionCookie is not null)
            context.Request.Headers.Cookie = $"{AppAccessPolicy.SESSION_COOKIE_NAME}={sessionCookie}";

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

    [TestCaseSource(nameof(PATHS))]
    public async Task ARequestWithTheSessionCookiePasses(string path)
    {
        Assert.That(await SendAsync(path, UI_TOKEN), Is.EqualTo(PASSED));
    }

    [TestCaseSource(nameof(PATHS))]
    public async Task ARequestWithoutTheSessionCookieIsRefused(string path)
    {
        Assert.That(await SendAsync(path, null), Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdee")]
    [TestCase("0123456789abcdef")]
    [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    [TestCase("")]
    public async Task ARequestWithAWrongSessionCookieIsRefused(string sessionCookie)
    {
        Assert.That(await SendAsync("/", sessionCookie), Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task TheTokenUnderAnotherCookieNameIsRefused()
    {
        var middleware = new AppAccessMiddleware(Pass, new AppAccessPolicy(APP_PORT, UI_TOKEN), NullLogger<AppAccessMiddleware>.Instance);
        var context = new DefaultHttpContext { Request = { Method = HttpMethods.Get, Path = "/", Headers = { Cookie = $"session={UI_TOKEN}" } } };

        await middleware.InvokeAsync(context);
        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [TestCase("http://127.0.0.1:61153")]
    [TestCase("http://localhost:61153")]
    [TestCase("http://[::1]:61153")]
    public async Task ARequestFromTheAppPasses(string origin)
    {
        Assert.That(await SendAsync("/_blazor/negotiate", UI_TOKEN, origin), Is.EqualTo(PASSED));
    }

    [TestCase("http://rebind.example:61153")]
    [TestCase("null")]
    [TestCase("http://127.0.0.1:61154")]
    [TestCase("https://127.0.0.1:61153")]
    [TestCase("http://127.0.0.1")]
    public async Task ARequestFromAnotherOriginIsRefusedEvenWithTheSessionCookie(string origin)
    {
        Assert.That(await SendAsync("/_blazor/negotiate", UI_TOKEN, origin), Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task ARequestWithSeveralOriginsIsRefused()
    {
        Assert.That(await SendAsync("/_blazor/negotiate", UI_TOKEN, "http://127.0.0.1:61153", "http://rebind.example:61153"), Is.EqualTo(StatusCodes.Status403Forbidden));
    }

#if DEBUG
    private static async Task<HttpResponse> BootAsync(string method, string? token)
    {
        var middleware = new AppAccessMiddleware(Pass, new AppAccessPolicy(APP_PORT, UI_TOKEN), NullLogger<AppAccessMiddleware>.Instance);
        var context = new DefaultHttpContext { Request = { Method = method, Path = "/dev/boot" } };
        if (token is not null)
            context.Request.QueryString = QueryString.Create("token", token);

        await middleware.InvokeAsync(context);
        return context.Response;
    }

    [Test]
    public async Task TheBootLinkOfADevelopmentBuildHandsOutTheSessionCookie()
    {
        var response = await BootAsync(HttpMethods.Get, UI_TOKEN);
        var cookie = response.Headers.SetCookie.ToString().ToLowerInvariant();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(StatusCodes.Status302Found));
            Assert.That(response.Headers.Location.ToString(), Is.EqualTo("/"));
            Assert.That(cookie, Does.StartWith($"{AppAccessPolicy.SESSION_COOKIE_NAME}={UI_TOKEN}"));
            Assert.That(cookie, Does.Contain("httponly"));
            Assert.That(cookie, Does.Contain("samesite=strict"));
            Assert.That(cookie, Does.Contain("path=/"));
            Assert.That(cookie, Does.Not.Contain("expires"));
        });
    }

    [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdee")]
    [TestCase("")]
    [TestCase(null)]
    public async Task TheBootLinkWithAWrongTokenIsRefused(string? token)
    {
        var response = await BootAsync(HttpMethods.Get, token);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(response.Headers.SetCookie.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task TheBootLinkAnswersOnlyAGet()
    {
        var response = await BootAsync(HttpMethods.Post, UI_TOKEN);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(response.Headers.SetCookie.ToString(), Is.Empty);
        });
    }
#endif
}