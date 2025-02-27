using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    /// <summary>
    /// Get the paths of the log files.
    /// </summary>
    /// <returns>The paths of the log files.</returns>
    public async Task<GetLogPathsResponse> GetLogPaths()
    {
        return await this.http.GetFromJsonAsync<GetLogPathsResponse>("/log/paths", this.jsonRustSerializerOptions);
    }
}