using System.Text;

using AIStudio.Settings.DataModel;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// Reads the files which replace the prompts of the BeyondFacts Lab, so that changed prompts can be tested without a
/// new version of AI Studio.
/// </summary>
/// <remarks>
/// The folder mirrors the files of the lab below Assistants/BeyondFactsLab, e.g., Prompts/steps/knowledge.md, so
/// that every name stays unique. Only the files of BflPromptLibrary.AllPaths count; the lab leaves any other file in
/// the folder alone, and an empty file replaces nothing. A Markdown file which matches no prompt, e.g., the copy of a
/// prompt which has a new name in this version, the lab names in a notice, see FindUnknown. The files are
/// instructions of the person, who chose to use them, so they bypass the prompt injection filter like the prompts
/// which come with the app.
/// </remarks>
public static class BflPromptOverrides
{
    /// <summary>
    /// The name of the folder below the folder of the lab.
    /// </summary>
    public const string DIRECTORY_NAME = "promptOverrides";

    /// <summary>
    /// Compares the names of two files as the file system does where the lab looks for a prompt: without case on
    /// Windows and macOS, whose file systems ignore it by default, and with case elsewhere.
    /// </summary>
    private static readonly StringComparer FILE_NAME_COMPARER = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Gets the folder whose files replace the prompts of the lab, or null while the setting does not allow them.
    /// </summary>
    /// <param name="settings">The settings of the lab, which an organization may lock.</param>
    /// <param name="directory">The folder for prompt overrides.</param>
    public static string? ActiveDirectory(DataBeyondFactsLab settings, string directory) => settings.AllowPromptOverrides ? directory : null;

    /// <summary>
    /// Reads the file which replaces a prompt of the lab, and checks its placeholders.
    /// </summary>
    /// <param name="directory">The folder for prompt overrides.</param>
    /// <param name="path">The prompt, one of BflPromptLibrary.AllPaths.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text of the file, or null when no file replaces the prompt.</returns>
    /// <exception cref="BflPromptOverrideException">The file contains a placeholder which the lab does not fill in it.</exception>
    public static async Task<string?> TryReadAsync(string directory, string path, CancellationToken token = default)
    {
        var text = await TryReadFileAsync(directory, path, token);
        if (text is null)
            return null;

        var allowed = BflPromptLibrary.PlaceholdersOf(path);
        foreach (var placeholder in BflPromptTemplate.NamesIn(text))
        {
            if (!allowed.Contains(placeholder))
                throw new BflPromptOverrideException(BflPromptLibrary.RelativePath(path), placeholder);
        }

        return text;
    }

    /// <summary>
    /// Finds the prompts of the lab which a file in the folder replaces with another text.
    /// </summary>
    /// <remarks>
    /// A copy of a prompt which matches the prompt of this version, apart from line breaks and the space around the
    /// text, changes nothing and does not count. A copy of an older version does.
    /// </remarks>
    /// <param name="directory">The folder for prompt overrides.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The prompts, as paths of BflPromptLibrary.AllPaths.</returns>
    public static async Task<IReadOnlyList<string>> FindAsync(string directory, CancellationToken token = default)
    {
        var replaced = new List<string>();
        foreach (var path in BflPromptLibrary.AllPaths)
        {
            string? text;
            try
            {
                text = await TryReadFileAsync(directory, path, token);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The lab will try to read the file, too, so it replaces the prompt as far as the person can tell:
                replaced.Add(path);
                continue;
            }

            if (text is not null && Normalize(text) != Normalize(await BflPromptLibrary.ReadBuiltInAsync(path, token)))
                replaced.Add(path);
        }

        return replaced;
    }

    /// <summary>
    /// Finds the Markdown files in the folder which replace no prompt of the lab, e.g., the copy of a prompt which has
    /// a new name in this version, so that the person learns why such a file changes nothing.
    /// </summary>
    /// <remarks>
    /// Only Markdown files count, since every prompt is one; files in other formats, such as notes, belong to the
    /// person. Hidden files and folders stay out, as do those whose names start with a dot on every system, such as
    /// the folder of a version control.
    /// </remarks>
    /// <param name="directory">The folder for prompt overrides.</param>
    /// <returns>The files relative to the folder, with forward slashes, e.g., "Prompts/steps/02-knowledge.md", in their order by name; none while the folder is missing.</returns>
    /// <exception cref="IOException">The folder cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be read.</exception>
    public static IReadOnlyList<string> FindUnknown(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        var known = new HashSet<string>(BflPromptLibrary.AllPaths.Select(BflPromptLibrary.RelativePath), FILE_NAME_COMPARER);
        var options = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
        return [.. Directory.EnumerateFiles(directory, "*.md", options)
            .Select(file => Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(file => !file.Split('/').Any(name => name.StartsWith('.')) && !known.Contains(file))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Copies the prompts of this version into the folder, so that changes start from them. A file which exists stays
    /// as it is.
    /// </summary>
    /// <param name="directory">The folder for prompt overrides; it is created when it is missing.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>How many files the folder got.</returns>
    public static async Task<int> CopyBuiltInAsync(string directory, CancellationToken token = default)
    {
        var copied = 0;
        foreach (var path in BflPromptLibrary.AllPaths)
        {
            var target = FilePath(directory, path);
            if (File.Exists(target))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var prompt = await BflPromptLibrary.ReadBuiltInAsync(path, token);
            try
            {
                // Writing fails when the file appeared meanwhile, so the person's file is never overwritten:
                await AtomicFile.WriteTextAsync(target, prompt, new UTF8Encoding(false), token, overwrite: false, flushToDisk: true);
                copied++;
            }
            catch (IOException) when (File.Exists(target))
            {
                // The person's file stays, and AtomicFile removed its temporary file already.
            }
        }

        return copied;
    }

    /// <summary>
    /// Gets the file which replaces a prompt of the lab.
    /// </summary>
    /// <param name="directory">The folder for prompt overrides.</param>
    /// <param name="path">The prompt, one of BflPromptLibrary.AllPaths.</param>
    public static string FilePath(string directory, string path) => Path.Join([directory, ..BflPromptLibrary.RelativePath(path).Split('/')]);

    /// <summary>
    /// Reads the file which replaces a prompt of the lab, without checking it.
    /// </summary>
    /// <param name="directory">The folder for prompt overrides.</param>
    /// <param name="path">The prompt, one of BflPromptLibrary.AllPaths.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The text of the file, or null when there is no such file or it is empty.</returns>
    private static async Task<string?> TryReadFileAsync(string directory, string path, CancellationToken token)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(FilePath(directory, path), Encoding.UTF8, token);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Brings a prompt into the form in which two copies compare, whatever system wrote them.
    /// </summary>
    /// <param name="text">The prompt.</param>
    private static string Normalize(string text) => text.ReplaceLineEndings("\n").Trim();
}