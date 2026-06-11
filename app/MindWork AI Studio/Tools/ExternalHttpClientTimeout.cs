using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using AIStudio.Settings;

namespace AIStudio.Tools;

/// <summary>
/// Provides utility methods to standardize the management of HTTP client timeouts
/// across various components in the application.
/// </summary>
public static class ExternalHttpClientTimeout
{
    public const int MIN_HTTP_CLIENT_TIMEOUT_SECONDS = 120;
    public const int MAX_HTTP_CLIENT_TIMEOUT_SECONDS = 3600;
    public const int DEFAULT_HTTP_CLIENT_TIMEOUT_SECONDS = 3600;

    private const string ENV_CUSTOM_ROOT_CERTIFICATES_ENABLED = "MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATES_ENABLED";
    private const string ENV_CUSTOM_ROOT_CERTIFICATE_BUNDLE_PATH = "MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_BUNDLE_PATH";
    private const string ENV_CUSTOM_ROOT_CERTIFICATE_ALLOWED_HOSTS = "MINDWORK_AI_STUDIO_EXTERNAL_HTTP_CUSTOM_ROOT_CERTIFICATE_ALLOWED_HOSTS";

    // id-kp-serverAuth: Extended Key Usage for TLS server authentication.
    // See RFC 5280, section 4.2.1.12: https://www.rfc-editor.org/rfc/rfc5280#section-4.2.1.12
    private const string TLS_SERVER_AUTHENTICATION_EKU_OID = "1.3.6.1.5.5.7.3.1";

    private static string TB(string fallbackEN) => PluginSystem.I18N.I.T(fallbackEN, typeof(ExternalHttpClientTimeout).Namespace, nameof(ExternalHttpClientTimeout));
    private static readonly Lazy<ILogger> LOGGER = new(() => Program.LOGGER_FACTORY.CreateLogger(nameof(ExternalHttpClientTimeout)));
    private static readonly Lazy<SettingsManager> SETTINGS_MANAGER = new(() => Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>());
    private static readonly Lock CUSTOM_ROOT_CERTIFICATE_LOCK = new();
    private static CustomRootCertificateCache? CUSTOM_ROOT_CERTIFICATE_CACHE;

    public static HttpClient CreateHttpClient(ExternalHttpTrustPolicy trustPolicy) => CreateHttpClient(null, trustPolicy);
    
    public static HttpClient CreateHttpClient(Uri? baseAddress, ExternalHttpTrustPolicy trustPolicy)
    {
        var customRootCertificateCache = GetCustomRootCertificateCache();
        var httpClient = customRootCertificateCache.State.IsUsable
            ? new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (request, certificate, chain, sslPolicyErrors) => 
                    ValidateServerCertificateWithCustomRootCertificates(request, certificate, chain, sslPolicyErrors, customRootCertificateCache, trustPolicy)
            })
            : new HttpClient();
        Configure(httpClient, baseAddress);
        return httpClient;
    }

    public static void ConfigureSocketsHttpHandler(SocketsHttpHandler handler, string host, ExternalHttpTrustPolicy trustPolicy)
    {
        var customRootCertificateCache = GetCustomRootCertificateCache();
        if (!customRootCertificateCache.State.IsUsable)
            return;

        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, chain, sslPolicyErrors) =>
            ValidateServerCertificateWithCustomRootCertificates(host, certificate, chain, sslPolicyErrors, customRootCertificateCache, trustPolicy);
    }

    public static ExternalHttpCustomRootCertificateState CustomRootCertificateState => GetCustomRootCertificateCache().State;

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
        var seconds = SETTINGS_MANAGER.Value.ConfigurationData.App.HttpClientTimeoutSeconds;
        if (seconds <= 0)
            seconds = DEFAULT_HTTP_CLIENT_TIMEOUT_SECONDS;

        seconds = Math.Clamp(seconds, MIN_HTTP_CLIENT_TIMEOUT_SECONDS, MAX_HTTP_CLIENT_TIMEOUT_SECONDS);
        return TimeSpan.FromSeconds(seconds);
    }
    
    private static void Configure(HttpClient httpClient, Uri? baseAddress = null)
    {
        httpClient.Timeout = GetTimeout();
        if (baseAddress is not null)
            httpClient.BaseAddress = baseAddress;
    }

    private static CustomRootCertificateCache GetCustomRootCertificateCache()
    {
        var configuration = ReadCustomRootCertificateConfiguration();
        var cacheKey = $"{configuration.Enabled}|{configuration.BundlePath}|{string.Join(";", configuration.AllowedHostPatterns)}|{ReadCertificateBundleFileSignature(configuration.BundlePath)}";
        lock (CUSTOM_ROOT_CERTIFICATE_LOCK)
        {
            if (CUSTOM_ROOT_CERTIFICATE_CACHE is not null && CUSTOM_ROOT_CERTIFICATE_CACHE.CacheKey == cacheKey)
                return CUSTOM_ROOT_CERTIFICATE_CACHE;

            CUSTOM_ROOT_CERTIFICATE_CACHE = LoadCustomRootCertificateCache(cacheKey, configuration);
            LogCustomRootCertificateState(CUSTOM_ROOT_CERTIFICATE_CACHE.State);
            return CUSTOM_ROOT_CERTIFICATE_CACHE;
        }
    }

    private static CustomRootCertificateConfiguration ReadCustomRootCertificateConfiguration()
    {
        var envEnabled = Environment.GetEnvironmentVariable(ENV_CUSTOM_ROOT_CERTIFICATES_ENABLED);
        var envBundlePath = Environment.GetEnvironmentVariable(ENV_CUSTOM_ROOT_CERTIFICATE_BUNDLE_PATH);
        var envAllowedHosts = Environment.GetEnvironmentVariable(ENV_CUSTOM_ROOT_CERTIFICATE_ALLOWED_HOSTS);

        var enabled = TryParseBooleanEnvironmentValue(envEnabled, out var parsedEnvEnabled)
            ? parsedEnvEnabled
            : SETTINGS_MANAGER.Value.ConfigurationData.App.ExternalHttpCustomRootCertificatesEnabled;

        var bundlePath = !string.IsNullOrWhiteSpace(envBundlePath)
            ? envBundlePath.Trim()
            : SETTINGS_MANAGER.Value.ConfigurationData.App.ExternalHttpCustomRootCertificateBundlePath.Trim();

        var allowedHostPatterns = ReadAllowedHostPatterns(envAllowedHosts);
        var source = ReadCustomRootCertificateConfigurationSource(envEnabled, envBundlePath, envAllowedHosts);

        return new(enabled, bundlePath, allowedHostPatterns, source);
    }

    private static string ReadCustomRootCertificateConfigurationSource(string? envEnabled, string? envBundlePath, string? envAllowedHosts)
    {
        if (!string.IsNullOrWhiteSpace(envEnabled) || !string.IsNullOrWhiteSpace(envBundlePath) || !string.IsNullOrWhiteSpace(envAllowedHosts))
            return TB("environment variables");

        var enabledIsManaged = ManagedConfiguration.TryGet(x => x.App, x => x.ExternalHttpCustomRootCertificatesEnabled, out var enabledMeta) && enabledMeta.IsLocked;
        var bundlePathIsManaged = ManagedConfiguration.TryGet(x => x.App, x => x.ExternalHttpCustomRootCertificateBundlePath, out var bundlePathMeta) && bundlePathMeta.IsLocked;
        var allowedHostsIsManaged = ManagedConfiguration.TryGet(x => x.App, x => x.ExternalHttpCustomRootCertificateAllowedHosts, out var allowedHostsMeta) && allowedHostsMeta.IsLocked;
        return enabledIsManaged || bundlePathIsManaged || allowedHostsIsManaged
            ? TB("configuration plugin")
            : TB("app settings");
    }

    private static IReadOnlyList<string> ReadAllowedHostPatterns(string? envAllowedHosts)
    {
        IEnumerable<string> rawPatterns = !string.IsNullOrWhiteSpace(envAllowedHosts)
            ? envAllowedHosts.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : SETTINGS_MANAGER.Value.ConfigurationData.App.ExternalHttpCustomRootCertificateAllowedHosts;

        var patterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawPattern in rawPatterns)
        {
            if (TryNormalizeAllowedHostPattern(rawPattern, out var pattern))
                patterns.Add(pattern);
            else
                LOGGER.Value.LogWarning($"Ignoring invalid external HTTP custom root certificate host pattern: '{rawPattern}'.");
        }

        return patterns.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool TryNormalizeAllowedHostPattern(string? rawPattern, out string pattern)
    {
        pattern = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPattern))
            return false;

        var normalized = rawPattern.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalized.Contains("://", StringComparison.Ordinal) || normalized.Contains('/', StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal))
            return false;

        if (normalized.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = normalized[2..];
            if (!IsValidDnsHost(suffix))
                return false;

            pattern = $"*.{suffix}";
            return true;
        }

        if (normalized.Contains('*', StringComparison.Ordinal))
            return false;

        if (!IsValidDnsHost(normalized))
            return false;

        pattern = normalized;
        return true;
    }

    private static bool IsValidDnsHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        if (Uri.CheckHostName(host) is not UriHostNameType.Dns)
            return false;

        return host.Split('.').All(label => !string.IsNullOrWhiteSpace(label) && !label.StartsWith('-') && !label.EndsWith('-'));
    }

    private static string ReadCertificateBundleFileSignature(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
            return string.Empty;

        try
        {
            var fileInfo = new FileInfo(bundlePath);
            return fileInfo.Exists
                ? $"{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}"
                : "missing";
        }
        catch
        {
            return "unavailable";
        }
    }

    private static bool TryParseBooleanEnvironmentValue(string? value, out bool parsedValue)
    {
        parsedValue = false;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (bool.TryParse(normalized, out parsedValue))
            return true;

        if (normalized is "1" || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) || normalized.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            parsedValue = true;
            return true;
        }

        if (normalized is "0" || normalized.Equals("no", StringComparison.OrdinalIgnoreCase) || normalized.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            parsedValue = false;
            return true;
        }

        return false;
    }

    private static CustomRootCertificateCache LoadCustomRootCertificateCache(string cacheKey, CustomRootCertificateConfiguration configuration)
    {
        var certificates = new X509Certificate2Collection();
        if (!configuration.Enabled)
        {
            return new(
                cacheKey,
                certificates,
                new ExternalHttpCustomRootCertificateState(false, configuration.Source, configuration.BundlePath, configuration.AllowedHostPatterns, false, 0, [], string.Empty));
        }

        if (string.IsNullOrWhiteSpace(configuration.BundlePath))
        {
            return new(
                cacheKey,
                certificates,
                new ExternalHttpCustomRootCertificateState(true, configuration.Source, configuration.BundlePath, configuration.AllowedHostPatterns, false, 0, [], TB("No certificate bundle path is configured.")));
        }

        if (!File.Exists(configuration.BundlePath))
        {
            return new(
                cacheKey,
                certificates,
                new ExternalHttpCustomRootCertificateState(true, configuration.Source, configuration.BundlePath, configuration.AllowedHostPatterns, false, 0, [], TB("The configured certificate bundle file does not exist.")));
        }

        try
        {
            var importedCertificates = new X509Certificate2Collection();
            importedCertificates.ImportFromPemFile(configuration.BundlePath);

            foreach (var certificate in importedCertificates)
            {
                if (!IsRootCertificateAuthority(certificate))
                    continue;

                certificates.Add(certificate);
            }

            var fingerprints = certificates
                .Select(certificate => certificate.GetCertHashString(HashAlgorithmName.SHA256))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var issue = certificates.Count == 0
                ? TB("The configured certificate bundle does not contain usable root CA certificates.")
                : string.Empty;

            return new(
                cacheKey,
                certificates,
                new ExternalHttpCustomRootCertificateState(true, configuration.Source, configuration.BundlePath, configuration.AllowedHostPatterns, certificates.Count > 0, certificates.Count, fingerprints, issue));
        }
        catch (Exception e)
        {
            return new(
                cacheKey,
                certificates,
                new ExternalHttpCustomRootCertificateState(true, configuration.Source, configuration.BundlePath, configuration.AllowedHostPatterns, false, 0, [], e.Message));
        }
    }

    private static bool IsRootCertificateAuthority(X509Certificate2 certificate)
    {
        if (!certificate.SubjectName.RawData.SequenceEqual(certificate.IssuerName.RawData))
            return false;

        return certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .Any(extension => extension.CertificateAuthority);
    }

    private static bool ValidateServerCertificateWithCustomRootCertificates(
        HttpRequestMessage request,
        X509Certificate? certificate,
        X509Chain? originalChain,
        SslPolicyErrors sslPolicyErrors,
        CustomRootCertificateCache customRootCertificateCache,
        ExternalHttpTrustPolicy trustPolicy)
    {
        return ValidateServerCertificateWithCustomRootCertificates(
            ReadRequestHost(request),
            certificate,
            originalChain,
            sslPolicyErrors,
            customRootCertificateCache,
            trustPolicy);
    }

    private static bool ValidateServerCertificateWithCustomRootCertificates(
        string host,
        X509Certificate? certificate,
        X509Chain? originalChain,
        SslPolicyErrors sslPolicyErrors,
        CustomRootCertificateCache customRootCertificateCache,
        ExternalHttpTrustPolicy trustPolicy)
    {
        if (sslPolicyErrors is SslPolicyErrors.None)
            return true;

        if (sslPolicyErrors is not SslPolicyErrors.RemoteCertificateChainErrors || certificate is null)
            return false;

        if (trustPolicy is ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY)
        {
            LOGGER.Value.LogError($"Rejected external HTTPS certificate for '{HostForLog(host)}' because this request requires system trust only. Configured custom root certificates are not allowed for this request.");
            return false;
        }

        if (!IsAllowedCustomRootCertificateHost(host, customRootCertificateCache.State.AllowedHostPatterns))
        {
            LOGGER.Value.LogError($"Rejected external HTTPS certificate for '{HostForLog(host)}' because the host is not allowed to use configured custom root certificates.");
            return false;
        }

        var ownsServerCertificate = certificate is not X509Certificate2;
        var serverCertificate = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        try
        {
            using var customChain = new X509Chain();
            customChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            customChain.ChainPolicy.CustomTrustStore.AddRange(customRootCertificateCache.Certificates);
            customChain.ChainPolicy.ApplicationPolicy.Add(new Oid(TLS_SERVER_AUTHENTICATION_EKU_OID));

            if (originalChain is not null)
            {
                foreach (var element in originalChain.ChainElements)
                {
                    if (element.Certificate.Thumbprint == serverCertificate.Thumbprint)
                        continue;

                    customChain.ChainPolicy.ExtraStore.Add(element.Certificate);
                }
            }

            var isValid = customChain.Build(serverCertificate);
            if (isValid)
                LogCustomRootCertificateAccepted(host);

            return isValid;
        }
        finally
        {
            if (ownsServerCertificate)
                serverCertificate.Dispose();
        }
    }

    private static bool IsAllowedCustomRootCertificateHost(string host, IReadOnlyList<string> allowedHostPatterns)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var normalizedHost = host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var pattern in allowedHostPatterns)
        {
            if (!pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                if (normalizedHost.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;

                continue;
            }

            var suffix = pattern[2..];
            if (!normalizedHost.EndsWith($".{suffix}", StringComparison.OrdinalIgnoreCase))
                continue;

            var prefix = normalizedHost[..^(suffix.Length + 1)];
            if (!prefix.Contains('.', StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void LogCustomRootCertificateState(ExternalHttpCustomRootCertificateState state)
    {
        if (!state.IsEnabled)
        {
            LOGGER.Value.LogInformation("External HTTP custom root certificates are disabled.");
            return;
        }

        if (state.IsUsable)
        {
            LOGGER.Value.LogWarning($"External HTTP custom root certificates are enabled from {state.Source}. Loaded {state.CertificateCount} root certificate(s) from '{state.BundlePath}'. Allowed hosts: {FormatAllowedHostPatternsForLog(state.AllowedHostPatterns)}. Fingerprints: {string.Join(", ", state.CertificateFingerprints)}");
            return;
        }

        LOGGER.Value.LogWarning($"External HTTP custom root certificates are enabled from {state.Source}, but no additional root certificates are usable. Bundle path: '{state.BundlePath}'. Issue: {state.Issue}");
    }

    private static void LogCustomRootCertificateAccepted(HttpRequestMessage request)
    {
        LogCustomRootCertificateAccepted(ReadRequestHost(request));
    }

    private static void LogCustomRootCertificateAccepted(string host) => LOGGER.Value.LogWarning($"Accepted an external HTTPS certificate for '{host}' using configured custom root certificates.");

    private static string ReadRequestHost(HttpRequestMessage request)
    {
        var host = request.RequestUri?.IdnHost;
        if (string.IsNullOrWhiteSpace(host))
            host = request.RequestUri?.Host;

        return host ?? string.Empty;
    }

    private static string HostForLog(string host) => string.IsNullOrWhiteSpace(host) ? "unknown host" : host;

    private static string FormatAllowedHostPatternsForLog(IReadOnlyList<string> allowedHostPatterns)
    {
        if (allowedHostPatterns.Count == 0)
            return "none";

        return string.Join(", ", allowedHostPatterns);
    }

    private readonly record struct CustomRootCertificateConfiguration(bool Enabled, string BundlePath, IReadOnlyList<string> AllowedHostPatterns, string Source);

    private sealed record CustomRootCertificateCache(
        string CacheKey,
        X509Certificate2Collection Certificates,
        ExternalHttpCustomRootCertificateState State);
}
