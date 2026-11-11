namespace AIStudio.Tools.Rust;

/// <summary>
/// What the runtime tells about itself once the app starts.
/// </summary>
/// <param name="WorkingDirectory">The working directory of the runtime.</param>
/// <param name="ExecutablePath">The path of the runtime's executable.</param>
/// <param name="LinuxPackageType">How the Linux build was packaged.</param>
/// <param name="InstallationKind">Whether and how the app may update itself.</param>
/// <param name="RustVersion">The Rust compiler which built the runtime, e.g., "1.99.1 (commit 58cf8f9c3)". A runtime older than the app, as a development build may be, sends none.</param>
public readonly record struct RuntimeInfoResponse(string WorkingDirectory, string ExecutablePath, LinuxPackageType LinuxPackageType, InstallationKind InstallationKind, string? RustVersion);