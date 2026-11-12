using System.Text.Json.Serialization;

using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

/// <summary>
/// The contracts with which System.Text.Json reads and writes nearly every call to the runtime, built at
/// compile time.
/// </summary>
/// <remarks>
/// The calls to the vector store are generic: every query of its client needs the answer type with the type
/// of its result, e.g., DatabaseQueryResponse&lt;bool?&gt;. A test fails for a request or an answer which is
/// missing here.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]

// The requests:
[JsonSerializable(typeof(SelectSecretRequest))]
[JsonSerializable(typeof(StoreSecretRequest))]
[JsonSerializable(typeof(RustService.ShareFileRequest))]
[JsonSerializable(typeof(PreviousDirectory))]
[JsonSerializable(typeof(SelectFileOptions))]
[JsonSerializable(typeof(SaveFileOptions))]
[JsonSerializable(typeof(OpenPathRequest))]
[JsonSerializable(typeof(OpenDocumentRequest))]
[JsonSerializable(typeof(RegisterShortcutRequest))]
[JsonSerializable(typeof(ValidateShortcutRequest))]
[JsonSerializable(typeof(SetInterfaceSizeRequest))]
[JsonSerializable(typeof(CreateMediaJobRequest))]
[JsonSerializable(typeof(LogEventRequest))]
[JsonSerializable(typeof(ValidateTokenizerRequest))]
[JsonSerializable(typeof(StoreTokenizerRequest))]
[JsonSerializable(typeof(DeleteTokenizerRequest))]
[JsonSerializable(typeof(CountTokensRequest))]
[JsonSerializable(typeof(PrepareImageRequest))]
[JsonSerializable(typeof(PrepareImageForModelRequest))]

// The answers:
[JsonSerializable(typeof(RequestedSecret))]
[JsonSerializable(typeof(StoreSecretResponse))]
[JsonSerializable(typeof(DeleteSecretResponse))]
[JsonSerializable(typeof(ShareFileResponse))]
[JsonSerializable(typeof(ImagePrepareResponse))]
[JsonSerializable(typeof(PreparedModelImage))]
[JsonSerializable(typeof(TauriEvent))]
[JsonSerializable(typeof(UpdateResponse))]
[JsonSerializable(typeof(SetClipboardResponse))]
[JsonSerializable(typeof(DirectorySelectionResponse))]
[JsonSerializable(typeof(FileSelectionResponse))]
[JsonSerializable(typeof(FilesSelectionResponse))]
[JsonSerializable(typeof(FileSaveResponse))]
[JsonSerializable(typeof(OpenPathResponse))]
[JsonSerializable(typeof(OpenDocumentResponse))]
[JsonSerializable(typeof(ShortcutRegistrationResult))]
[JsonSerializable(typeof(ShortcutValidationResponse))]
[JsonSerializable(typeof(ShortcutResponse))]
[JsonSerializable(typeof(RuntimeInfoResponse))]
[JsonSerializable(typeof(AppExitResponse))]
[JsonSerializable(typeof(AppRestartResponse))]
[JsonSerializable(typeof(InterfaceSizeResponse))]
[JsonSerializable(typeof(CreateMediaJobResponse))]
[JsonSerializable(typeof(MediaJobEvent))]
[JsonSerializable(typeof(List<EnterpriseConfig>))]
[JsonSerializable(typeof(GetLogPathsResponse))]
[JsonSerializable(typeof(TokenizerResponse))]

// The vector store, with the result of every query its client sends:
[JsonSerializable(typeof(QdrantEdgeInfo))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.EnsureVectorStoreRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.CheckVectorStoreRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.InsertEmbeddingRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.SearchEmbeddingRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.RetrieveVectorsRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.DeleteEmbeddingByFileRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.RelocateEmbeddingFileRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.OptimizeVectorStoreRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.DeleteVectorStoreRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.PrepareBackupRequest))]
[JsonSerializable(typeof(QdrantEdgeClientImplementation.CopyStoreForBackupRequest))]
[JsonSerializable(typeof(RustService.DatabaseOperationResponse))]
[JsonSerializable(typeof(RustService.DatabaseQueryResponse<VectorStoreEnsureResult>))]
[JsonSerializable(typeof(RustService.DatabaseQueryResponse<bool?>))]
[JsonSerializable(typeof(RustService.DatabaseQueryResponse<List<VectorSearchResult>>))]
[JsonSerializable(typeof(RustService.DatabaseQueryResponse<List<StoredVector>>))]
[JsonSerializable(typeof(RustService.DatabaseQueryResponse<VectorStoreBackupPreparation>))]
[JsonSerializable(typeof(RustService.DatabaseQueryResponse<long?>))]
internal sealed partial class RustJsonContext : JsonSerializerContext;