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
}