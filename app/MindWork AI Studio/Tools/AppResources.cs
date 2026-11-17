using System.Text;

using Microsoft.Extensions.FileProviders;

#if RELEASE
using System.Reflection;
#endif

namespace AIStudio.Tools;

/// <summary>
/// Reads files which come with AI Studio, such as prompts, guides, or the built-in plugins.
/// </summary>
/// <remarks>
/// The files lie below the project folder and are listed as EmbeddedResource in the project file. A development
/// build reads them from the disk, so that a change shows without building again; a release reads them from the
/// resources embedded into the app. Paths are relative to the project folder and use forward slashes.
/// </remarks>
public static class AppResources
{
    /// <summary>
    /// Gets a file provider for a folder of the app.
    /// </summary>
    /// <param name="relativeFolder">The folder, relative to the project folder, e.g., "Plugins".</param>
    /// <returns>The file provider, whose paths are relative to that folder.</returns>
    public static IFileProvider FolderProvider(string relativeFolder)
    {
        var folder = Normalize(relativeFolder);
#if DEBUG
        return new PhysicalFileProvider(Path.Join(Environment.CurrentDirectory, folder));
#else
        return new ManifestEmbeddedFileProvider(Assembly.GetAssembly(typeof(Program))!, folder);
#endif
    }

    /// <summary>
    /// Reads a text file of the app.
    /// </summary>
    /// <param name="relativePath">The file, relative to the project folder, e.g., "Assistants/PromptOptimizer/prompting_guideline.md".</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text, or an empty string when the app has no such file.</returns>
    public static async Task<string> ReadTextAsync(string relativePath, CancellationToken token = default)
    {
        var path = Normalize(relativePath);
#if DEBUG
        var filePath = Path.Join(Environment.CurrentDirectory, path);
        return File.Exists(filePath) ? await File.ReadAllTextAsync(filePath, Encoding.UTF8, token) : string.Empty;
#else
        var file = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(typeof(Program))!).GetFileInfo(path);
        if (!file.Exists || file.IsDirectory)
            return string.Empty;

        await using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(token);
#endif
    }

    /// <summary>
    /// Turns a path into the form both kinds of builds understand: forward slashes, without a slash at either end.
    /// </summary>
    /// <param name="relativePath">The path.</param>
    private static string Normalize(string relativePath) => relativePath.Replace('\\', '/').Trim('/');
}