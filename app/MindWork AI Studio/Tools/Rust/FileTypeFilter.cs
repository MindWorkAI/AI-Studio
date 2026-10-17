namespace AIStudio.Tools.Rust;

/// <summary>
/// Represents a file type that can optionally contain child file types.
/// Use the static helpers Leaf, Parent, Composite, ByFileName, and ByFileNamePrefix to build readable trees.
/// </summary>
/// <remarks>
/// File names are kept apart from the extensions: a native file picker filters by extension only, and
/// a file name such as Dockerfile is no extension of any file.
/// </remarks>
/// <param name="FilterName">Display name of the type (e.g., "Document").</param>
/// <param name="FilterExtensions">File extensions belonging to this type (without dot).</param>
/// <param name="Children">Nested file types that are included when this type is selected.</param>
/// <param name="FileNames">Complete file names belonging to this type (e.g., "Makefile").</param>
/// <param name="FileNamePrefixes">Beginnings of file names belonging to this type (e.g., "Dockerfile" for "Dockerfile.development").</param>
public sealed record FileTypeFilter(string FilterName, string[] FilterExtensions, IReadOnlyList<FileTypeFilter> Children, string[] FileNames, string[] FileNamePrefixes)
{
    /// <summary>
    /// Factory for a leaf node.
    /// Example: <c>FileTypeFilter.Leaf(".NET", "cs", "razor")</c>
    /// </summary>
    public static FileTypeFilter Leaf(string name, params string[] extensions) =>
        new(name, extensions, [], [], []);

    /// <summary>
    /// Factory for a parent node that only has children.
    /// Example: <c>FileTypeFilter.Parent("Source Code", dotnet, java)</c>
    /// </summary>
    public static FileTypeFilter Parent(string name, params FileTypeFilter[]? children) =>
        new(name, [], children ?? [], [], []);

    /// <summary>
    /// Factory for a composite node that has its own extensions in addition to children.
    /// </summary>
    public static FileTypeFilter Composite(string name, string[] extensions, params FileTypeFilter[] children) =>
        new(name, extensions, children, [], []);

    /// <summary>
    /// Factory for a leaf node that matches complete file names instead of extensions.
    /// Example: <c>FileTypeFilter.ByFileName("Build files", "Makefile", "BUILD")</c>
    /// </summary>
    public static FileTypeFilter ByFileName(string name, params string[] fileNames) =>
        new(name, [], [], fileNames, []);

    /// <summary>
    /// Factory for a leaf node that matches the beginning of file names instead of extensions.
    /// Example: <c>FileTypeFilter.ByFileNamePrefix("Container files", "Dockerfile", "Containerfile")</c>
    /// </summary>
    public static FileTypeFilter ByFileNamePrefix(string name, params string[] prefixes) =>
        new(name, [], [], [], prefixes);

    /// <summary>
    /// Collects all extensions for this type, including children.
    /// </summary>
    public IEnumerable<string> FlattenExtensions() => this.Flatten(type => type.FilterExtensions);

    /// <summary>
    /// Collects all complete file names for this type, including children.
    /// </summary>
    public IEnumerable<string> FlattenFileNames() => this.Flatten(type => type.FileNames);

    /// <summary>
    /// Collects all file name prefixes for this type, including children.
    /// </summary>
    public IEnumerable<string> FlattenFileNamePrefixes() => this.Flatten(type => type.FileNamePrefixes);

    private IEnumerable<string> Flatten(Func<FileTypeFilter, string[]> select)
    {
        return select(this)
            .Concat(this.Children.SelectMany(child => child.Flatten(select)))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public bool ContainsType(FileTypeFilter target)
    {
        if (this == target)
            return true;

        return this.Children.Any(child => child.ContainsType(target));
    }
}