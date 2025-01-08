using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

public sealed partial class RustService
{
    public async Task<DirectorySelectionResponse> SelectDirectory(string title, string? initialDirectory = null)
    {
        PreviousDirectory? previousDirectory = initialDirectory is null ? null : new (initialDirectory);
        var result = await this.http.PostAsJsonAsync($"/select/directory?title={title}", previousDirectory, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a directory: '{result.StatusCode}'");
            return new DirectorySelectionResponse(true, string.Empty);
        }
        
        return await result.Content.ReadFromJsonAsync<DirectorySelectionResponse>(this.jsonRustSerializerOptions);
    }
    
    public async Task<FileSelectionResponse> SelectFile(string title, string? initialFile = null)
    {
        PreviousFile? previousFile = initialFile is null ? null : new (initialFile);
        var result = await this.http.PostAsJsonAsync($"/select/file?title={title}", previousFile, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a file: '{result.StatusCode}'");
            return new FileSelectionResponse(true, string.Empty);
        }
        
        return await result.Content.ReadFromJsonAsync<FileSelectionResponse>(this.jsonRustSerializerOptions);
    }
}