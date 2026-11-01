using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.HostFiltering;

namespace AIStudio.Tools.Security;

/// <summary>
/// Decides which requests may reach the app server.
/// </summary>
/// <remarks>
/// The Rust runtime makes a new token for every start. It gives the token to this server through
/// an environment variable, and to the app window as the value of the session cookie. Nothing else
/// on this machine has the cookie, so a request with it comes from the app window.
/// </remarks>
/// <param name="appPort">The port the app server listens on.</param>
/// <param name="uiToken">The token of this start, which the session cookie carries.</param>
public sealed class AppAccessPolicy(int appPort, string uiToken)
{
    /// <summary>
    /// The name of the session cookie. The Rust runtime sets the cookie under the same name.
    /// </summary>
    public const string SESSION_COOKIE_NAME = "ai_studio_session";

    /// <summary>
    /// The environment variable which carries the token from the Rust runtime to this server.
    /// </summary>
    public const string UI_TOKEN_ENVIRONMENT_VARIABLE = "AI_STUDIO_UI_TOKEN";

    /// <summary>
    /// The host names under which the app server answers. A request for any other name, such as one
    /// which a website points at the loopback address, gets refused.
    /// </summary>
    private static readonly string[] ALLOWED_HOSTS = ["127.0.0.1", "localhost", "[::1]"];

    private readonly HashSet<string> allowedOrigins = ALLOWED_HOSTS.Select(host => $"http://{host}:{appPort}").ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly byte[] uiTokenBytes = Encoding.UTF8.GetBytes(uiToken);

    /// <summary>
    /// Lets the host filtering of ASP.NET Core accept only the host names of the app server.
    /// </summary>
    /// <param name="options">The options of the host filtering.</param>
    public static void ConfigureHostFiltering(HostFilteringOptions options)
    {
        options.AllowedHosts = [..ALLOWED_HOSTS];
        options.AllowEmptyHosts = false;
        options.IncludeFailureMessage = false;
    }

    /// <summary>
    /// Checks whether the given origin is the app itself.
    /// </summary>
    /// <param name="origin">The value of the Origin header of a request.</param>
    /// <returns>True when a page of the app made the request.</returns>
    public bool IsAppOrigin(string origin) => this.allowedOrigins.Contains(origin);

    /// <summary>
    /// Checks whether the given request carries the session cookie of this start.
    /// </summary>
    /// <remarks>
    /// The comparison takes the same time no matter where a wrong value differs, so the time of a
    /// refusal tells nothing about how much of a guess was right.
    /// </remarks>
    /// <param name="request">The request.</param>
    /// <returns>True when the request comes from the app window.</returns>
    public bool HasSessionCookie(HttpRequest request) => request.Cookies.TryGetValue(SESSION_COOKIE_NAME, out var value) && this.IsUIToken(value);

    /// <summary>
    /// Checks whether the given value is the token of this start, in a time which does not depend
    /// on where a wrong value differs.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True when the value is the token.</returns>
    public bool IsUIToken(string? value) => !string.IsNullOrEmpty(value) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), this.uiTokenBytes);
}