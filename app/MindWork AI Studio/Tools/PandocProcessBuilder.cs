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
    private static readonly RID CPU_ARCHITECTURE = META_DATA_ARCH.Architecture.ToRID();
    
    private string? providedInputFile;
    private string? providedOutputFile;
    private string? providedInputFormat;
    private string? providedOutputFormat;
    
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
    
    public async Task<PandocPreparedProcess> BuildAsync(RustService rustService)
    {
        var sbArguments = new StringBuilder();
        
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
        // First, we try to find the pandoc executable in the data directory.
        // Any local installation should be preferred over the system-wide installation.
        //
        var localInstallationRootDirectory = await Pandoc.GetPandocDataFolder(rustService);
        try
        {
            var executableName = PandocExecutableName;
            var subdirectories = Directory.GetDirectories(localInstallationRootDirectory, "*", SearchOption.AllDirectories);
            foreach (var subdirectory in subdirectories)
            {
                var pandocPath = Path.Combine(subdirectory, executableName);
                if (File.Exists(pandocPath))
                    return new(pandocPath, true);
            }
        }
        catch
        {
            // ignored
        }
        
        //
        // When no local installation was found, we assume that the pandoc executable is in the system PATH.
        //
        return new(PandocExecutableName, false);
    }
    
    /// <summary>
    /// Reads the os platform to determine the used executable name.
    /// </summary>
    public static string PandocExecutableName => CPU_ARCHITECTURE is RID.WIN_ARM64 or RID.WIN_X64 ? "pandoc.exe" : "pandoc";
}