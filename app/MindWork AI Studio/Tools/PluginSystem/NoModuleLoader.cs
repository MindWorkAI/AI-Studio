using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// This Lua module loader does not load any modules.
/// </summary>
public sealed class NoModuleLoader : ILuaModuleLoader
{
    #region Implementation of ILuaModuleLoader

    public bool Exists(string moduleName) => false;

    public ValueTask<LuaModule> LoadAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new LuaModule());
    }

    #endregion
}