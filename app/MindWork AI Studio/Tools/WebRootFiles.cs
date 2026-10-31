using System.Reflection;

using Microsoft.Extensions.FileProviders;

namespace AIStudio.Tools;

/// <summary>
/// Reads the files below wwwroot, which every build embeds into the app.
/// </summary>
/// <remarks>
/// The app reads files such as its changelogs directly instead of requesting them over HTTP from
/// its own server. The release build serves its static files from the same provider.
/// </remarks>
public sealed class WebRootFiles
{
    /// <summary>
    /// Gets the files below wwwroot.
    /// </summary>
    public IFileProvider FileProvider { get; } = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!, "wwwroot");

    /// <summary>
    /// Reads a text file below wwwroot.
    /// </summary>
    /// <param name="path">The path below wwwroot, such as <c>changelog/v26.10.1.md</c>.</param>
    /// <returns>The text of the file, or an empty string when there is no such file.</returns>
    public async Task<string> ReadTextAsync(string path)
    {
        var file = this.FileProvider.GetFileInfo(path);
        if (!file.Exists)
            return string.Empty;

        await using var fileStream = file.CreateReadStream();
        using var reader = new StreamReader(fileStream);
        return await reader.ReadToEndAsync();
    }
}