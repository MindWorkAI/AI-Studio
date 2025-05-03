using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

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
    
    public async Task<FileSelectionResponse> SelectFile(string title, FileTypeFilter? filter = null, string? initialFile = null)
    {
        var payload = new SelectFileOptions
        {
            Title = title,
            PreviousFile = initialFile is null ? null : new (initialFile),
            Filter = filter
        };
        
        var result = await this.http.PostAsJsonAsync("/select/file", payload, this.jsonRustSerializerOptions);
        if (!result.IsSuccessStatusCode)
        {
            this.logger!.LogError($"Failed to select a file: '{result.StatusCode}'");
            return new FileSelectionResponse(true, string.Empty);
        }
        
        return await result.Content.ReadFromJsonAsync<FileSelectionResponse>(this.jsonRustSerializerOptions);
    }
}