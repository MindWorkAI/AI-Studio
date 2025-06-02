using System.IO.Compression;

namespace AIStudio.Tools.PluginSystem;

public static partial class PluginFactory
{
    public static async Task<bool> TryDownloadingConfigPluginAsync(Guid configPlugId, string configServerUrl, CancellationToken cancellationToken = default)
    {
        LOG.LogInformation($"Downloading configuration plugin with ID: {configPlugId} from server: {configServerUrl}");
        var tempDownloadFile = Path.GetTempFileName();
        try
        {
            using var httpClient = new HttpClient();
            var serverUrl = configServerUrl.EndsWith('/') ? configServerUrl[..^1] : configServerUrl;
            var response = await httpClient.GetAsync($"{serverUrl}/{configPlugId}.zip", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                await using var tempFileStream = File.Create(tempDownloadFile);
                await response.Content.CopyToAsync(tempFileStream, cancellationToken);
                
                var pluginDirectory = Path.Join(CONFIGURATION_PLUGINS_ROOT, configPlugId.ToString());
                if(Directory.Exists(pluginDirectory))
                    Directory.Delete(pluginDirectory, true);
                
                Directory.CreateDirectory(pluginDirectory);
                ZipFile.ExtractToDirectory(tempDownloadFile, pluginDirectory);
                
                LOG.LogInformation($"Configuration plugin with ID='{configPlugId}' downloaded and extracted successfully to '{pluginDirectory}'.");
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
        }
        
        return true;
    }
}