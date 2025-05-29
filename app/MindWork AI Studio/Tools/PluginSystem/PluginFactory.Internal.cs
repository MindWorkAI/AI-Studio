using Microsoft.Extensions.FileProviders;

#if RELEASE
using System.Reflection;
#endif

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    public static async Task EnsureInternalPlugins()
    {
        if (!IS_INITIALIZED)
        {
            LOG.LogError("PluginFactory is not initialized. Please call Setup() before using it.");
            return;
        }
        
        // A plugin update might remove some resources. Even worse, a plugin
        // might have changed its name, etc. Thus, we delete the internal
        // plugin directories before copying the new resources:
        LOG.LogInformation("Try to delete the internal plugins directory for maintenance.");
        if (Directory.Exists(INTERNAL_PLUGINS_ROOT))
        {
            try
            {
                Directory.Delete(INTERNAL_PLUGINS_ROOT, true);
                LOG.LogInformation("Successfully deleted the internal plugins directory for maintenance.");
            }
            catch (Exception e)
            {
                LOG.LogError($"Could not delete the internal plugins directory for maintenance: {INTERNAL_PLUGINS_ROOT}. Error: {e}");
            }
        }
        
        LOG.LogInformation("Start ensuring internal plugins.");
        if(!Directory.Exists(INTERNAL_PLUGINS_ROOT))
            Directory.CreateDirectory(INTERNAL_PLUGINS_ROOT);
        
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
            foreach (var contentFilePath in resourceFileProvider.GetDirectoryContents(metaData.ResourcePath))
            {
                if(contentFilePath.IsDirectory)
                {
                    LOG.LogError("The plugin contains a directory. This is not allowed.");
                    continue;
                }
                
                await CopyInternalPluginFile(contentFilePath, metaData);
            }
        }
        catch
        {
            LOG.LogError($"Was not able to ensure the plugin: {plugin}");
        }
    }

    private static async Task CopyInternalPluginFile(IFileInfo resourceFilePath, InternalPluginData metaData)
    {
        await using var inputStream = resourceFilePath.CreateReadStream();
        
        var pluginTypeBasePath = Path.Join(INTERNAL_PLUGINS_ROOT, metaData.Type.GetDirectory());
        
        if (!Directory.Exists(INTERNAL_PLUGINS_ROOT))
            Directory.CreateDirectory(INTERNAL_PLUGINS_ROOT);
        
        if (!Directory.Exists(pluginTypeBasePath))
            Directory.CreateDirectory(pluginTypeBasePath);
        
        var pluginPath = Path.Join(pluginTypeBasePath, metaData.ResourceName);
        if (!Directory.Exists(pluginPath))
            Directory.CreateDirectory(pluginPath);
        
        var pluginFilePath = Path.Join(pluginPath, resourceFilePath.Name);
            
        await using var outputStream = File.Create(pluginFilePath);
        await inputStream.CopyToAsync(outputStream);
    }
}