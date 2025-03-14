using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Represents a plugin that could not be loaded.
/// </summary>
/// <param name="state">The Lua state that the plugin was loaded into.</param>
/// <param name="parsingError">The error message that occurred while parsing the plugin.</param>
public sealed class NoPlugin(LuaState state, string parsingError) : PluginBase(state, PluginType.NONE, parsingError);