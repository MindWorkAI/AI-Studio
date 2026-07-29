using System.Diagnostics;
using AIStudio.Chat;
using AIStudio.Dialogs;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;
using DialogOptions = MudBlazor.DialogOptions;

namespace AIStudio.Tools;

public static class PlainFileExport
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(PlainFileExport)); 
    
    private sealed record ExportTarget(string DisplayName, FileTypeFilter FileType);
    
    private static readonly ExportTarget MARKDOWN = new("Markdown (.md)", FileTypes.MARKDOWN);
    
    private static string TB(string fallbackEn) => I18N.I.T(fallbackEn, typeof(PlainFileExport).Namespace, nameof(PlainFileExport));
    
    public static async Task<bool> ToFile(RustService rustService, FileExportFormat format, IContent markdownContent)
    {
        var exportTarget = format switch
        {
            FileExportFormat.MARKDOWN => MARKDOWN,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
        
        var response = await rustService.SaveFile(TB("Export chat"), [exportTarget.FileType]);
        if (response.UserCancelled)
        {
            LOGGER.LogInformation("User cancelled the save dialog.");
            return false;
        }

        LOGGER.LogInformation($"The user chose the path '{response.SaveFilePath}' for the {exportTarget.DisplayName} export.");

        try
        {
            var markdownText = markdownContent switch
            {
                ContentText text => text.Text,
                ContentImage _ => "Image export is not yet possible.",
                _ => "Unknown content type. Cannot export document."
            };

            await File.WriteAllTextAsync(response.SaveFilePath, markdownText);
            await MessageBus.INSTANCE.SendSuccess(new(Icons.Material.Filled.CheckCircle, TB("Document export successful")));
            
            return true;
        }
        catch (Exception ex)
        {
            LOGGER.LogError(ex, $"Error during {exportTarget.DisplayName} export.");
            await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.Cancel, TB("Error during document export")));
            return false;
        }
    }
}
