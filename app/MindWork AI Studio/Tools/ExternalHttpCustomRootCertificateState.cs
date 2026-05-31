namespace AIStudio.Tools;

public sealed record ExternalHttpCustomRootCertificateState(
    bool IsEnabled,
    string Source,
    string BundlePath,
    IReadOnlyList<string> AllowedHostPatterns,
    bool IsUsable,
    int CertificateCount,
    IReadOnlyList<string> CertificateFingerprints,
    string Issue);