using System.Reflection;

using Microsoft.Extensions.FileProviders;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    public static async Task EnsureInternalPlugins()
    {
        LOG.LogInformation("Start ensuring internal plugins.");
        foreach (var plugin in Enum.GetValues<InternalPlugin>())
        {
            LOG.LogInformation($"Ensure plugin: {plugin}");
            await EnsurePlugin(plugin);
        }
    }
    
    private static async Task EnsurePlugin(InternalPlugin plugin)
    {
        try
        {
            #if DEBUG
            var basePath = Path.Join(Environment.CurrentDirectory, "Plugins");
            var resourceFileProvider = new PhysicalFileProvider(basePath);
            #else
            var resourceFileProvider = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!, "Plugins");
            #endif
            
            var metaData = plugin.MetaData();
            var mainResourcePath = $"{metaData.ResourcePath}/plugin.lua";
            var resourceInfo = resourceFileProvider.GetFileInfo(mainResourcePath);
            
            if(!resourceInfo.Exists)
            {
                LOG.LogError($"The plugin {plugin} does not exist. This should not happen, since the plugin is an integral part of AI Studio.");
                return;
            }
            
            // Ensure that the additional resources exist:
            foreach (var content in resourceFileProvider.GetDirectoryContents(metaData.ResourcePath))
            {
                if(content.IsDirectory)
                {
                    LOG.LogError("The plugin contains a directory. This is not allowed.");
                    continue;
                }
                
                await CopyPluginFile(content, metaData);
            }
        }
        catch
        {
            LOG.LogError($"Was not able to ensure the plugin: {plugin}");
        }
    }

    private static async Task CopyPluginFile(IFileInfo resourceInfo, InternalPluginData metaData)
    {
        await using var inputStream = resourceInfo.CreateReadStream();
        
        var pluginTypeBasePath = Path.Join(PLUGINS_ROOT, metaData.Type.GetDirectory());
        
        if (!Directory.Exists(PLUGINS_ROOT))
            Directory.CreateDirectory(PLUGINS_ROOT);
        
        if (!Directory.Exists(pluginTypeBasePath))
            Directory.CreateDirectory(pluginTypeBasePath);
        
        var pluginPath = Path.Join(pluginTypeBasePath, metaData.ResourceName);
        if (!Directory.Exists(pluginPath))
            Directory.CreateDirectory(pluginPath);
        
        var pluginFilePath = Path.Join(pluginPath, resourceInfo.Name);
            
        await using var outputStream = File.Create(pluginFilePath);
        await inputStream.CopyToAsync(outputStream);
    }
}