using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.Services;

/// <summary>
/// A provider or data source a configuration plugin brings, and where it sends data to.
/// </summary>
/// <param name="Type">The kind of configuration object.</param>
/// <param name="Name">The name the configuration gives it.</param>
/// <param name="Endpoint">The host of a self-hosted destination, or the name of the cloud provider.</param>
public sealed record ConfigurationPluginDestination(PluginConfigurationObjectType Type, string Name, string Endpoint);