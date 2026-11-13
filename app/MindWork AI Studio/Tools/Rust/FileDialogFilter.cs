namespace AIStudio.Tools.Rust;

/// <summary>
/// The file type filter of a native file dialog, as the runtime takes it.
/// </summary>
/// <remarks>
/// This mirrors the struct FileTypeFilter in runtime/src/file_actions.rs, which knows one name and
/// one flat list of extensions. A tree of file types never reaches the runtime: FileTypes.ToFileDialogFilter
/// flattens it first, because the runtime would drop the children.
/// </remarks>
/// <param name="FilterName">The name of the filter, as some file dialogs show it.</param>
/// <param name="FilterExtensions">The file extensions to show, without dot.</param>
public sealed record FileDialogFilter(string FilterName, string[] FilterExtensions);