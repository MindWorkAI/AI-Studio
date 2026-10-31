using Microsoft.AspNetCore.HostFiltering;

namespace AIStudio.Tools.Security;

/// <summary>
/// Decides which requests may reach the app server.
/// </summary>
/// <param name="appPort">The port the app server listens on.</param>
public sealed class AppAccessPolicy(int appPort)
{
    /// <summary>
    /// The host names under which the app server answers. A request for any other name, such as one
    /// which a website points at the loopback address, gets refused.
    /// </summary>
    private static readonly string[] ALLOWED_HOSTS = ["127.0.0.1", "localhost", "[::1]"];

    private readonly HashSet<string> allowedOrigins = ALLOWED_HOSTS.Select(host => $"http://{host}:{appPort}").ToHashSet(StringComparer.OrdinalIgnoreCase);

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
}