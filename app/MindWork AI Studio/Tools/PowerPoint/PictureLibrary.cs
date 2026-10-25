namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Reads a folder of pictures and icons, which the LLM may choose from by their names.
/// </summary>
/// <remarks>
/// The LLM reads only the names, never the pictures: a library holds hundreds or thousands of them.
/// Only PNG and JPEG pictures count, since the writer can place nothing else; many icons come as SVG,
/// which is why the other files are counted, so that the user learns about them. Hidden files and
/// folders are left out, as everywhere in AI Studio.
/// </remarks>
internal static class PictureLibrary
{
    /// <summary>
    /// The most pictures of a library the LLM reads about in one request.
    /// </summary>
    /// <remarks>
    /// Every name costs tokens in every request, and 5,000 names come to about 50,000 tokens. A
    /// larger library is sampled at random, anew for every request, so that every picture has the
    /// same chance rather than those which the file system happens to list first.
    /// </remarks>
    public const int MAX_LIBRARY_PICTURES = 5_000;

    /// <summary>
    /// What the name of an icon, or of one of the folders it is in, starts with, in any case.
    /// </summary>
    public const string ICON_PREFIX = "icon";

    private static readonly HashSet<string> PICTURE_EXTENSIONS = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg" };

    /// <summary>
    /// Reads a library on a thread of its own, since a folder on a network drive may take a while.
    /// </summary>
    /// <param name="folder">The library folder.</param>
    /// <param name="random">Where the sample of a large library comes from.</param>
    /// <param name="reportInaccessibleFolder">Told about every folder which could not be read.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What the library offers.</returns>
    public static Task<PictureLibraryContent> ReadAsync(string folder, Random random, Action<string, Exception>? reportInaccessibleFolder, CancellationToken token) => Task.Run(() => Read(folder, random, MAX_LIBRARY_PICTURES, reportInaccessibleFolder, token), token);

    /// <summary>
    /// Reads a library.
    /// </summary>
    /// <remarks>
    /// The sample is drawn while the folder is read, by reservoir sampling: the first pictures fill
    /// the sample, and each later one replaces a random member of it with the chance that keeps every
    /// picture equally likely. So the list of all pictures is never held in memory.
    /// </remarks>
    /// <param name="folder">The library folder.</param>
    /// <param name="random">Where the sample of a large library comes from.</param>
    /// <param name="maxPictures">The most pictures the sample holds.</param>
    /// <param name="reportInaccessibleFolder">Told about every folder which could not be read.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What the library offers.</returns>
    public static PictureLibraryContent Read(string folder, Random random, int maxPictures, Action<string, Exception>? reportInaccessibleFolder, CancellationToken token)
    {
        var sample = new List<LibraryPicture>();
        var pictureCount = 0;
        var iconCount = 0;
        var skippedFileCount = 0;
        foreach (var path in DirectoryFileTraversal.EnumerateFiles(folder, true, false, reportInaccessibleDirectory: reportInaccessibleFolder))
        {
            token.ThrowIfCancellationRequested();
            if (!PICTURE_EXTENSIONS.Contains(Path.GetExtension(path)))
            {
                skippedFileCount++;
                continue;
            }

            var name = Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');
            var picture = new LibraryPicture(path, name, IsIcon(name));
            pictureCount++;
            if (picture.IsIcon)
                iconCount++;

            if (sample.Count < maxPictures)
                sample.Add(picture);
            else if (random.NextInt64(pictureCount) is var slot && slot < maxPictures)
                sample[(int)slot] = picture;
        }

        sample.Sort((first, second) => StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name));
        return new PictureLibraryContent(sample, pictureCount, iconCount, skippedFileCount);
    }

    /// <summary>
    /// Whether a picture is an icon: its name or the name of one of its folders below the library starts with "icon".
    /// </summary>
    /// <param name="name">The path of the picture below the library folder, with forward slashes.</param>
    private static bool IsIcon(string name) => name.Split('/').Any(part => part.StartsWith(ICON_PREFIX, StringComparison.OrdinalIgnoreCase));
}
