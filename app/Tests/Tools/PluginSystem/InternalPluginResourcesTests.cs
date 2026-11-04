using System.Reflection;

using AIStudio.Tools.PluginSystem;

using Microsoft.Extensions.FileProviders;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that the release finds the plugins which ship with AI Studio, such as the language plugins.
/// </summary>
/// <remarks>
/// A debug build reads these plugins from the source tree, while the release copies them out of the
/// files embedded into the app. A plugin which is missing there would leave the release without its
/// language, and only an error in the log would tell.
/// </remarks>
[TestFixture]
public sealed class InternalPluginResourcesTests
{
    [Test]
    public void EveryInternalPluginIsEmbedded([Values] InternalPlugin plugin)
    {
        var embeddedPlugins = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!, "Plugins");
        var mainFile = embeddedPlugins.GetFileInfo($"{plugin.MetaData().ResourcePath}/plugin.lua");

        Assert.That(mainFile.Exists, Is.True, $"The release would not find the plugin {plugin}, since it is not among the embedded files.");
        Assert.That(mainFile.Length, Is.GreaterThan(0), $"The release would find the plugin {plugin} empty.");
    }
}