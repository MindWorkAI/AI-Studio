using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<QdrantInfo> GetQdrantInfo()
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var response = await this.http.GetFromJsonAsync<QdrantInfo>("/system/qdrant/info", this.jsonRustSerializerOptions, cts.Token);
            return response;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return default;
        }
    }
}