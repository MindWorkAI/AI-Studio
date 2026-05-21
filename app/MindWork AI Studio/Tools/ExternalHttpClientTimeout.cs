using AIStudio.Settings;

namespace AIStudio.Tools;

/// <summary>
/// Provides utility methods to standardize the management of HTTP client timeouts
/// across various components in the application.
/// </summary>
public static class ExternalHttpClientTimeout
{
    public const int DEFAULT_HTTP_CLIENT_TIMEOUT_SECONDS = 3600;

    public static HttpClient CreateHttpClient(Uri? baseAddress = null)
    {
        var httpClient = new HttpClient();
        Configure(httpClient, baseAddress);
        return httpClient;
    }

    public static string GetTimeoutDescription()
    {
        var timeout = GetTimeout();

        if (timeout.TotalHours >= 1 && timeout.TotalMinutes % 60 == 0)
        {
            var hours = (int)timeout.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        if (timeout.TotalMinutes >= 1 && timeout.TotalSeconds % 60 == 0)
        {
            var minutes = (int)timeout.TotalMinutes;
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        var seconds = (int)timeout.TotalSeconds;
        return seconds == 1 ? "1 second" : $"{seconds} seconds";
    }

    public static CancellationTokenSource CreateTimeoutTokenSource(CancellationToken cancellationToken)
    {
        var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(GetTimeout());
        return timeoutTokenSource;
    }

    public static bool IsTimeoutException(Exception exception, CancellationToken userCancellationToken = default)
    {
        if (userCancellationToken.IsCancellationRequested)
            return false;

        if (exception is TimeoutException)
            return true;

        if (exception is OperationCanceledException)
            return true;

        return exception.InnerException is not null && IsTimeoutException(exception.InnerException, userCancellationToken);
    }
    
    private static TimeSpan GetTimeout()
    {
        var settingsManager = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
        var seconds = settingsManager.ConfigurationData.App.HttpClientTimeoutSeconds;
        if (seconds <= 0)
            seconds = DEFAULT_HTTP_CLIENT_TIMEOUT_SECONDS;

        return TimeSpan.FromSeconds(seconds);
    }
    
    private static void Configure(HttpClient httpClient, Uri? baseAddress = null)
    {
        httpClient.Timeout = GetTimeout();
        if (baseAddress is not null)
            httpClient.BaseAddress = baseAddress;
    }
}
