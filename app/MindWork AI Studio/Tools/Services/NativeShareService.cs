namespace AIStudio.Tools.Services;

public sealed class NativeShareService(RustService rustService)
{
    public Task<bool> Share(string filePath) => rustService.ShareFile(filePath);
}
