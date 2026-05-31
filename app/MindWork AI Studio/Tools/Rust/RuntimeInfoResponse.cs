namespace AIStudio.Tools.Rust;

public readonly record struct RuntimeInfoResponse(string WorkingDirectory, string ExecutablePath, string LinuxPackageType);