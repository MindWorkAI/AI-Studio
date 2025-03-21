using System.Reflection;

using AIStudio.Settings;

using Lua;

using Microsoft.Extensions.FileProviders;

namespace AIStudio.Tools.PluginSystem;

public static class PluginFactory
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger("PluginFactory");
    private static readonly string DATA_DIR = SettingsManager.DataDirectory!;
    
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
        
        var pluginsRoot = Path.Join(DATA_DIR, "plugins");
        var pluginTypeBasePath = Path.Join(pluginsRoot, metaData.Type.GetDirectory());
        
        if (!Directory.Exists(pluginsRoot))
            Directory.CreateDirectory(pluginsRoot);
        
        if (!Directory.Exists(pluginTypeBasePath))
            Directory.CreateDirectory(pluginTypeBasePath);
        
        var pluginPath = Path.Join(pluginTypeBasePath, metaData.ResourceName);
        if (!Directory.Exists(pluginPath))
            Directory.CreateDirectory(pluginPath);
        
        var pluginFilePath = Path.Join(pluginPath, resourceInfo.Name);
            
        await using var outputStream = File.Create(pluginFilePath);
        await inputStream.CopyToAsync(outputStream);
    }

    public static async Task LoadAll()
    {
        
    }
    
    public static async Task<PluginBase> Load(string path, string code, CancellationToken cancellationToken = default)
    {
        var state = LuaState.Create();

        try
        {
            await state.DoStringAsync(code, cancellationToken: cancellationToken);
        }
        catch (LuaParseException e)
        {
            return new NoPlugin($"Was not able to parse the plugin: {e.Message}");
        }
        
        if (!state.Environment["TYPE"].TryRead<string>(out var typeText))
            return new NoPlugin("TYPE does not exist or is not a valid string.");
        
        if (!Enum.TryParse<PluginType>(typeText, out var type))
            return new NoPlugin($"TYPE is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues<PluginType>()}");
        
        if(type is PluginType.NONE)
            return new NoPlugin($"TYPE is not a valid plugin type. Valid types are: {CommonTools.GetAllEnumValues<PluginType>()}");
        
        return type switch
        {
            PluginType.LANGUAGE => new PluginLanguage(path, state, type),
            
            _ => new NoPlugin("This plugin type is not supported yet. Please try again with a future version of AI Studio.")
        };
    }
}