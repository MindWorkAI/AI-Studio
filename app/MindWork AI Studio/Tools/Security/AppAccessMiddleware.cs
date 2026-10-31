using Microsoft.Extensions.Primitives;

namespace AIStudio.Tools.Security;

/// <summary>
/// Refuses every request to the app server which does not come from the app window.
/// </summary>
/// <remarks>
/// The middleware runs before every other one, so it covers pages, static files, the Blazor hub
/// with all of its transports, and every endpoint alike. A request which carries an Origin header
/// must come from a page of the app. Browsers send that header with requests made by a script, the
/// WebSocket of the Blazor hub included, but not when they load a page. A sandboxed iframe sends
/// "null", which gets refused as well.
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

    private readonly Lock refusalLock = new();
    private DateTimeOffset lastRefusalLog = DateTimeOffset.MinValue;
    private int refusalsSinceLastLog;

    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin;
        if (!StringValues.IsNullOrEmpty(origin) && (origin.Count != 1 || !policy.IsAppOrigin(origin.ToString())))
        {
            this.Refuse(context, "another origin");
            return;
        }

        await next(context);
    }

    private void Refuse(HttpContext context, string reason)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        this.LogRefusal(context, reason);
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