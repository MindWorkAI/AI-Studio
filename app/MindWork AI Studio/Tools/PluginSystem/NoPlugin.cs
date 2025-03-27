using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a plugin that could not be loaded.
/// </summary>
/// <param name="parsingError">The error message that occurred while parsing the plugin.</param>
public sealed class NoPlugin(string parsingError) : PluginBase(false, LuaState.Create(), PluginType.NONE, parsingError);