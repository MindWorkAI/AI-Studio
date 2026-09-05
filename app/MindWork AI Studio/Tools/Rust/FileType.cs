namespace AIStudio.Tools.Rust;

/// <summary>
/// Represents a file type that can optionally contain child file types.
/// Use the static helpers <see cref="Leaf"/>,  <see cref="Parent"/> and <see cref="Composite"/> to build readable trees.
/// </summary>
/// <param name="FilterName">Display name of the type (e.g., "Document").</param>
/// <param name="FilterExtensions">File extensions belonging to this type (without dot).</param>
/// <param name="Children">Nested file types that are included when this type is selected.</param>
public sealed record FileType(string FilterName, string[] FilterExtensions, IReadOnlyList<FileType> Children)
{
    /// <summary>
    /// Factory for a leaf node.
    /// Example: <c>FileType.Leaf(".NET", "cs", "razor")</c>
    /// </summary>
    public static FileType Leaf(string name, params string[] extensions) =>
        new(name, extensions, []);

    /// <summary>
    /// Factory for a parent node that only has children.
    /// Example: <c>FileType.Parent("Source Code", dotnet, java)</c>
    /// </summary>
    public static FileType Parent(string name, params FileType[]? children) =>
        new(name, [], children ?? []);

    /// <summary>
    /// Factory for a composite node that has its own extensions in addition to children.
    /// </summary>
    public static FileType Composite(string name, string[] extensions, params FileType[] children) =>
        new(name, extensions, children);

    /// <summary>
    /// Collects all extensions for this type, including children.
    /// </summary>
    public IEnumerable<string> FlattenExtensions()
    {
        return this.FilterExtensions
            .Concat(this.Children.SelectMany(child => child.FlattenExtensions()))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}