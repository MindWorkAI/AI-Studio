namespace AIStudio.Tools.Web;

internal static class WebHostHelper
{
    public static string Normalize(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();
}
