using System.Reflection;

using Microsoft.Extensions.FileProviders;

namespace AIStudio.Tools;

/// <summary>
/// Reads the files below wwwroot, which every build embeds into the app.
/// </summary>
/// <remarks>
/// The app reads files such as its changelogs directly instead of requesting them over HTTP from
/// its own server, which answers the app window only. The release build serves its static files
/// from the same provider.
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

    /// <summary>
    /// Copies a file below wwwroot to the given place, such as one the user chose to save it to.
    /// </summary>
    /// <param name="path">The path below wwwroot, such as <c>specs/eri/v1.json</c>.</param>
    /// <param name="destinationPath">Where to write the copy. An existing file there gets replaced.</param>
    /// <returns>True when the copy was written, false when there is no such file below wwwroot.</returns>
    public async Task<bool> CopyToAsync(string path, string destinationPath)
    {
        var file = this.FileProvider.GetFileInfo(path);
        if (!file.Exists)
            return false;

        await using var source = file.CreateReadStream();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);
        return true;
    }
}