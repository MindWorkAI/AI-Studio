using Lua.IO;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// The file system of the Lua runtime of plugins, in which no file exists.
/// </summary>
/// <remarks>
/// The Lua runtime reaches files through its file system: dofile and loadfile do, and so does require
/// for every module the module loader does not know, which it then looks for along package.path,
/// relative to the working directory of the app. Plugins are shared between users, and the assistant
/// builder runs code a model wrote, so none of them may reach a file this way.<br/><br/>
/// A plugin still loads the files of its own directory with require: the module loader, see
/// PluginLoader, reads them without this file system.
/// </remarks>
public sealed class NoLuaFileSystem : ILuaFileSystem
{
    private const string NO_FILES = "Plugins cannot reach files through the Lua runtime.";

    #region Implementation of ILuaFileSystem

    public bool IsReadable(string path) => false;

    public ValueTask<ILuaStream> Open(string path, LuaFileOpenMode mode, CancellationToken cancellationToken) => throw new NotSupportedException(NO_FILES);

    public ValueTask Rename(string oldName, string newName, CancellationToken cancellationToken) => throw new NotSupportedException(NO_FILES);

    public ValueTask Remove(string path, CancellationToken cancellationToken) => throw new NotSupportedException(NO_FILES);

    public string GetTempFileName() => throw new NotSupportedException(NO_FILES);

    public ValueTask<ILuaStream> OpenTempFileStream(CancellationToken cancellationToken) => throw new NotSupportedException(NO_FILES);

    #endregion
}