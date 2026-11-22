namespace Build.Commands;

public static class FlatpakArchitectureExtensions
{
    /// <param name="architecture">The CPU architecture of the Flatpak.</param>
    extension(FlatpakArchitecture architecture)
    {
        /// <summary>
        /// The name of the architecture in Flatpak and on Linux, as uname -m reports it.
        /// </summary>
        public string FlatpakName => architecture switch
        {
            FlatpakArchitecture.AARCH64 => "aarch64",
            FlatpakArchitecture.X86_64 => "x86_64",

            _ => string.Empty,
        };

        /// <summary>
        /// The platform of a Docker image which runs on this architecture.
        /// </summary>
        public string DockerPlatform => architecture switch
        {
            FlatpakArchitecture.AARCH64 => "linux/arm64",
            FlatpakArchitecture.X86_64 => "linux/amd64",

            _ => string.Empty,
        };

        /// <summary>
        /// The file name of the bundle of AI Studio, the same as the CI of the Flatpak repository gives it.
        /// </summary>
        public string AppBundleName => $"MindWork AI Studio_{architecture.FlatpakName}.flatpak";

        /// <summary>
        /// The file name of the bundle of the Pandoc plugin, the same as the CI of the Flatpak repository gives it.
        /// </summary>
        public string PandocBundleName => $"MindWork AI Studio Plugin Pandoc_{architecture.FlatpakName}.flatpak";

        /// <summary>
        /// Finds the architecture for a name, as Flatpak, Linux, or Docker write it.
        /// </summary>
        /// <param name="name">The name, e.g., aarch64, arm64, x86_64, or amd64.</param>
        /// <returns>The architecture, or NONE for an unknown name.</returns>
        public static FlatpakArchitecture FromName(string name) => name.Trim().ToLowerInvariant() switch
        {
            "aarch64" or "arm64" => FlatpakArchitecture.AARCH64,
            "x86_64" or "amd64" => FlatpakArchitecture.X86_64,

            _ => FlatpakArchitecture.NONE,
        };
    }
}