using System.IO.Compression;
using System.Net.Http.Headers;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    public static async Task<EntityTagHeaderValue?> DetermineConfigPluginETagAsync(Guid configPlugId, string configServerUrl, CancellationToken cancellationToken = default)
    {
        if(configPlugId == Guid.Empty || string.IsNullOrWhiteSpace(configServerUrl))
            return null;
        
        try
        {
            var serverUrl = configServerUrl.EndsWith('/') ? configServerUrl[..^1] : configServerUrl;
            var downloadUrl = $"{serverUrl}/{configPlugId}.zip";
            
            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.Headers.ETag;
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while determining the ETag for the configuration plugin.");
            return null;
        }
    }
    
    public static async Task<bool> TryDownloadingConfigPluginAsync(Guid configPlugId, string configServerUrl, CancellationToken cancellationToken = default)
    {
        if(!IS_INITIALIZED)
        {
            LOG.LogWarning("Plugin factory is not yet initialized. Cannot download configuration plugin.");
            return false;
        }

        var serverUrl = configServerUrl.EndsWith('/') ? configServerUrl[..^1] : configServerUrl;
        var downloadUrl = $"{serverUrl}/{configPlugId}.zip";
        
        LOG.LogInformation($"Try to download configuration plugin with ID='{configPlugId}' from server='{configServerUrl}' (GET {downloadUrl})");
        var tempDownloadFile = Path.GetTempFileName();
        try
        {
            await LockHotReloadAsync();
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                await using(var tempFileStream = File.Create(tempDownloadFile))
                {
                    await response.Content.CopyToAsync(tempFileStream, cancellationToken);
                }
                
                var configDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, configPlugId.ToString());
                if(Directory.Exists(configDirectory))
                    Directory.Delete(configDirectory, true);
                
                Directory.CreateDirectory(configDirectory);
                ZipFile.ExtractToDirectory(tempDownloadFile, configDirectory);
                
                LOG.LogInformation($"Configuration plugin with ID='{configPlugId}' downloaded and extracted successfully to '{configDirectory}'.");
            }
            else
                LOG.LogError($"Failed to download the enterprise configuration plugin. HTTP Status: {response.StatusCode}");
        }
        catch (Exception e)
        {
            LOG.LogError(e, "An error occurred while downloading or extracting the enterprise configuration plugin.");
        }
        finally
        {
            if (File.Exists(tempDownloadFile))
            {
                try
                {
                    File.Delete(tempDownloadFile);
                }
                catch (Exception e)
                {
                    LOG.LogError(e, "Failed to delete the temporary download file.");
                }
            }
            
            UnlockHotReload();
        }
        
        return true;
    }
}