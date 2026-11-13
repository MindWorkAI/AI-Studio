using Lua;

namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Reads one configuration object from the Lua table in which a configuration plugin defines it.
/// </summary>
/// <param name="index">The position of the entry within its Lua table, which the log names.</param>
/// <param name="table">The entry to read.</param>
/// <param name="configPluginId">The configuration plugin which defines the object.</param>
/// <param name="pluginPath">The directory of the plugin, which paths in the entry are relative to.</param>
/// <param name="dryRun">Whether the plugin is only checked, e.g., before an import. Nothing must be stored then.</param>
/// <param name="configObject">The configuration object read from the entry.</param>
/// <returns>True when the entry defines a valid configuration object.</returns>
public delegate bool ConfigurationObjectParser(int index, LuaTable table, Guid configPluginId, string pluginPath, bool dryRun, out ConfigurationBaseObject configObject);