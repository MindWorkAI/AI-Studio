using AIStudio.Tools.Databases;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

public sealed partial class RustService
{
    public async Task<QdrantInfo> GetQdrantInfo(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));
            
            return await this.http.GetFromJsonAsync<QdrantInfo>("/system/qdrant/info", this.jsonRustSerializerOptions, cts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if(this.logger is not null)
                this.logger.LogWarning("Fetching Qdrant info from Rust service was cancelled by caller.");
            else
                Console.WriteLine("Fetching Qdrant info from Rust service was cancelled by caller.");
            
            return new QdrantInfo
            {
                Status = QdrantStatus.UNAVAILABLE,
                UnavailableReason = "Operation cancelled by caller."
            };
        }
        catch (Exception e)
        {
            if(this.logger is not null)
                this.logger.LogError(e, "Error while fetching Qdrant info from Rust service.");
            else
                Console.WriteLine($"Error while fetching Qdrant info from Rust service: '{e}'.");
            
            return new QdrantInfo
            {
                Status = QdrantStatus.UNAVAILABLE,
                UnavailableReason = e.Message
            };
        }
    }
}