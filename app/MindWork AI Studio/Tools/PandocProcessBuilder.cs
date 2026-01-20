using System.Diagnostics;
using System.Reflection;
using System.Text;

using AIStudio.Tools.Metadata;
using AIStudio.Tools.Services;

using SharedTools;

namespace AIStudio.Tools;

public sealed class PandocProcessBuilder
{
    private static readonly Assembly ASSEMBLY = Assembly.GetExecutingAssembly();
    private static readonly MetaDataArchitectureAttribute META_DATA_ARCH = ASSEMBLY.GetCustomAttribute<MetaDataArchitectureAttribute>()!;

    // Use runtime detection instead of metadata to ensure correct RID on dev machines:
    private static readonly RID CPU_ARCHITECTURE = RIDExtensions.GetCurrentRID();
    private static readonly RID METADATA_ARCHITECTURE = META_DATA_ARCH.Architecture.ToRID();
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PandocProcessBuilder));

    // Tracks whether the first log has been written to avoid log spam on repeated calls:
    private static bool HAS_LOGGED_ONCE;

    private string? providedInputFile;
    private string? providedOutputFile;
    private string? providedInputFormat;
    private string? providedOutputFormat;
    private bool useStandaloneMode;
    
    private readonly List<string> additionalArguments = new();
    
    private PandocProcessBuilder()
    {
    }
    
    public static PandocProcessBuilder Create() => new();
    
    public PandocProcessBuilder WithInputFile(string inputFile)
    {
        this.providedInputFile = inputFile;
        return this;
    }
    
    public PandocProcessBuilder WithOutputFile(string outputFile)
    {
        this.providedOutputFile = outputFile;
        return this;
    }
    
    public PandocProcessBuilder WithInputFormat(string inputFormat)
    {
        this.providedInputFormat = inputFormat;
        return this;
    }
    
    public PandocProcessBuilder WithOutputFormat(string outputFormat)
    {
        this.providedOutputFormat = outputFormat;
        return this;
    }
    
    public PandocProcessBuilder AddArgument(string argument)
    {
        this.additionalArguments.Add(argument);
        return this;
    }

    public PandocProcessBuilder UseStandaloneMode()
    {
        this.useStandaloneMode = true;
        return this;
    }
    
    public async Task<PandocPreparedProcess> BuildAsync(RustService rustService)
    {
        var sbArguments = new StringBuilder();

        if (this.useStandaloneMode)
            sbArguments.Append(" --standalone ");
        
        if(!string.IsNullOrWhiteSpace(this.providedInputFile))
            sbArguments.Append(this.providedInputFile);
        
        if(!string.IsNullOrWhiteSpace(this.providedInputFormat))
            sbArguments.Append($" -f {this.providedInputFormat}");
        
        if(!string.IsNullOrWhiteSpace(this.providedOutputFormat))
            sbArguments.Append($" -t {this.providedOutputFormat}");
        
        foreach (var additionalArgument in this.additionalArguments)
            sbArguments.Append($" {additionalArgument}");
        
        if(!string.IsNullOrWhiteSpace(this.providedOutputFile))
            sbArguments.Append($" -o {this.providedOutputFile}");
        
        var pandocExecutable = await PandocExecutablePath(rustService);
        return new (new ProcessStartInfo
        {
            FileName = pandocExecutable.Executable,
            Arguments = sbArguments.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }, pandocExecutable.IsLocalInstallation);
    }
    
    /// <summary>
    /// Returns the path to the pandoc executable.
    /// </summary>
    /// <remarks>
    /// Any local installation of pandoc will be preferred over the system-wide installation.
    /// When a local installation is found, its absolute path will be returned. In case no local
    /// installation is found, the name of the pandoc executable will be returned.
    /// </remarks>
    /// <param name="rustService">Global rust service to access file system and data dir.</param>
    /// <returns>Path to the pandoc executable.</returns>
    private static async Task<PandocExecutable> PandocExecutablePath(RustService rustService)
    {
        //
        // Determine if we should log (only on the first call):
        //
        var shouldLog = !HAS_LOGGED_ONCE;

        try
        {
            //
            // Log a warning if the runtime-detected RID differs from the metadata RID.
            // This can happen on dev machines where the metadata.txt contains stale values.
            // We always use the runtime-detected RID for correct behavior.
            //
            if (shouldLog && CPU_ARCHITECTURE != METADATA_ARCHITECTURE)
            {
                LOGGER.LogWarning(
                    "Runtime-detected RID '{RuntimeRID}' differs from metadata RID '{MetadataRID}'. Using runtime-detected RID. This is expected on dev machines where metadata.txt may be outdated.",
                    CPU_ARCHITECTURE.ToUserFriendlyName(),
                    METADATA_ARCHITECTURE.ToUserFriendlyName());
            }

            //
            // First, we try to find the pandoc executable in the data directory.
            // Any local installation should be preferred over the system-wide installation.
            //
            var localInstallationRootDirectory = await Pandoc.GetPandocDataFolder(rustService);

            //
            // Check if the data directory path is valid:
            //
            if (string.IsNullOrWhiteSpace(localInstallationRootDirectory))
            {
                if (shouldLog)
                    LOGGER.LogWarning("The local data directory path is empty or null. Cannot search for local Pandoc installation.");
            }
            else if (!Directory.Exists(localInstallationRootDirectory))
            {
                if (shouldLog)
                    LOGGER.LogWarning("The local Pandoc installation directory does not exist: '{LocalInstallationRootDirectory}'.", localInstallationRootDirectory);
            }
            else
            {
                //
                // The directory exists, search for the pandoc executable:
                //
                var executableName = PandocExecutableName;
                if (shouldLog)
                    LOGGER.LogInformation("Searching for Pandoc executable '{ExecutableName}' in: '{LocalInstallationRootDirectory}'.", executableName, localInstallationRootDirectory);

                try
                {
                    //
                    // First, check the root directory itself:
                    //
                    var rootExecutablePath = Path.Combine(localInstallationRootDirectory, executableName);
                    if (File.Exists(rootExecutablePath))
                    {
                        if (shouldLog)
                            LOGGER.LogInformation("Found local Pandoc installation at the root path: '{Path}'.", rootExecutablePath);

                        HAS_LOGGED_ONCE = true;
                        return new(rootExecutablePath, true);
                    }

                    //
                    // Then, search all subdirectories:
                    //
                    var subdirectories = Directory.GetDirectories(localInstallationRootDirectory, "*", SearchOption.AllDirectories);
                    foreach (var subdirectory in subdirectories)
                    {
                        var pandocPath = Path.Combine(subdirectory, executableName);
                        if (File.Exists(pandocPath))
                        {
                            if (shouldLog)
                                LOGGER.LogInformation("Found local Pandoc installation at: '{Path}'.", pandocPath);

                            HAS_LOGGED_ONCE = true;
                            return new(pandocPath, true);
                        }
                    }

                    if (shouldLog)
                        LOGGER.LogWarning("No Pandoc executable found in local installation directory or its subdirectories.");
                }
                catch (Exception ex)
                {
                    if (shouldLog)
                        LOGGER.LogWarning(ex, "Error while searching for a local Pandoc installation in: '{LocalInstallationRootDirectory}'.", localInstallationRootDirectory);
                }
            }

            //
            // When no local installation was found, we assume that the pandoc executable is in the system PATH:
            //
            if (shouldLog)
                LOGGER.LogWarning("Falling back to system PATH for the Pandoc executable: '{ExecutableName}'.", PandocExecutableName);
            
            return new(PandocExecutableName, false);
        }
        finally
        {
            HAS_LOGGED_ONCE = true;
        }
    }
    
    /// <summary>
    /// Reads the os platform to determine the used executable name.
    /// </summary>
    public static string PandocExecutableName => CPU_ARCHITECTURE is RID.WIN_ARM64 or RID.WIN_X64 ? "pandoc.exe" : "pandoc";
}