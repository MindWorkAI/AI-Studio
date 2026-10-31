using Microsoft.Extensions.Primitives;

namespace AIStudio.Tools.Security;

/// <summary>
/// Refuses every request to the app server which does not come from the app window.
/// </summary>
/// <remarks>
/// The middleware runs before every other one, so it covers pages, static files, the Blazor hub
/// with all of its transports, and every endpoint alike. A release build makes no exception: a
/// request without the session cookie gets refused, whatever it asks for. Only a development build
/// also takes its boot link, with which another browser gets the cookie.
///
/// A request which carries an Origin header must, in addition, come from a page of the app.
/// Browsers send that header with requests made by a script, the WebSocket of the Blazor hub
/// included, but not when they load a page. A sandboxed iframe sends "null", which gets refused as
/// well.
/// </remarks>
/// <param name="next">The next middleware.</param>
/// <param name="policy">Decides which requests may pass.</param>
/// <param name="logger">The logger.</param>
public sealed class AppAccessMiddleware(RequestDelegate next, AppAccessPolicy policy, ILogger<AppAccessMiddleware> logger)
{
    /// <summary>
    /// The shortest interval between two log entries about refused requests. Any process on this
    /// machine can send requests, and an entry for each of them would flood the log.
    /// </summary>
    private static readonly TimeSpan REFUSAL_LOG_INTERVAL = TimeSpan.FromMinutes(1);

    /// <summary>
    /// What a refused request gets to read. The app window only shows it when it lost its cookie,
    /// and a new start gives it a new one.
    /// </summary>
    private const string REFUSAL_TEXT = "This window cannot access AI Studio. Please restart AI Studio.";

    private readonly Lock refusalLock = new();
    private DateTimeOffset lastRefusalLog = DateTimeOffset.MinValue;
    private int refusalsSinceLastLog;

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin;
        if (!StringValues.IsNullOrEmpty(origin) && (origin.Count != 1 || !policy.IsAppOrigin(origin.ToString())))
        {
            await this.RefuseAsync(context, "another origin");
            return;
        }

#if DEBUG
        if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path.Equals(DEV_BOOT_PATH, StringComparison.Ordinal))
        {
            await this.BootAsync(context);
            return;
        }
#endif

        if (!policy.HasSessionCookie(context.Request))
        {
            await this.RefuseAsync(context, "a client without the session cookie");
            return;
        }

        await next(context);
    }

#if DEBUG
    /// <summary>
    /// The path of the boot link which development builds write for other browsers, such as the
    /// one of an agent. A release build knows neither the link nor this path.
    /// </summary>
    private const string DEV_BOOT_PATH = "/dev/boot";

    /// <summary>
    /// Trades the token of a boot link for the session cookie and sends the browser on to the app.
    /// The browser opens the link by itself, so it sends the cookie along after the redirect.
    /// </summary>
    private async Task BootAsync(HttpContext context)
    {
        var token = context.Request.Query["token"].ToString();
        if (!policy.IsUIToken(token))
        {
            await this.RefuseAsync(context, "a boot link with a wrong token");
            return;
        }

        context.Response.Cookies.Append(AppAccessPolicy.SESSION_COOKIE_NAME, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true
        });

        context.Response.Redirect("/");
    }
#endif

    private async Task RefuseAsync(HttpContext context, string reason)
    {
        this.LogRefusal(context, reason);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(REFUSAL_TEXT);
    }

    /// <summary>
    /// Logs a refused request, at most once per interval, together with the number of requests
    /// refused since the last entry. The entry names the path but never the query or any header.
    /// </summary>
    private void LogRefusal(HttpContext context, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        int refusals;
        lock (this.refusalLock)
        {
            this.refusalsSinceLastLog++;
            if (now - this.lastRefusalLog < REFUSAL_LOG_INTERVAL)
                return;

            refusals = this.refusalsSinceLastLog;
            this.refusalsSinceLastLog = 0;
            this.lastRefusalLog = now;
        }

        logger.LogWarning("Refused {Refusals} request(s) to the app server since the last report. The latest one, '{Method} {Path}', came from {Reason}.", refusals, context.Request.Method, context.Request.Path.Value, reason);
    }
}