using System.Reflection;
using System.Text.Json;

using AIStudio.Tools;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Serialization;

public static partial class JsonTestRoots
{
    /// <summary>
    /// What AI Studio sends to its runtime.
    /// </summary>
    public static IEnumerable<TestCaseData> RuntimeRequests()
    {
        TestHost.PrepareStaticState();
        var options = RustService.JSON_OPTIONS;

        yield return Case(new(typeof(SelectSecretRequest), options));
        yield return Case(new(typeof(StoreSecretRequest), options));
        yield return Case(new(typeof(RustService.ShareFileRequest), options));
        yield return Case(new(typeof(PreviousDirectory), options));
        yield return Case(new(typeof(SelectFileOptions), options));
        yield return Case(new(typeof(SaveFileOptions), options));
        yield return Case(new(typeof(OpenPathRequest), options));
        yield return Case(new(typeof(OpenDocumentRequest), options));
        yield return Case(new(typeof(RegisterShortcutRequest), options));
        yield return Case(new(typeof(ValidateShortcutRequest), options));
        yield return Case(new(typeof(SetInterfaceSizeRequest), options));
        yield return Case(new(typeof(CreateMediaJobRequest), options));
        yield return Case(new(typeof(LogEventRequest), options));
        yield return Case(new(typeof(ValidateTokenizerRequest), options));
        yield return Case(new(typeof(StoreTokenizerRequest), options));
        yield return Case(new(typeof(DeleteTokenizerRequest), options));
        yield return Case(new(typeof(CountTokensRequest), options));
        yield return Case(new(typeof(PrepareImageRequest), options));
        yield return Case(new(typeof(PrepareImageForModelRequest), options));

        // Every request of the vector store:
        foreach (var type in typeof(QdrantEdgeClientImplementation).GetNestedTypes(BindingFlags.NonPublic).Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal)).OrderBy(type => type.Name, StringComparer.Ordinal))
            yield return Case(new(type, options));

        // The prompt injection filter, which gets called with the defaults of HttpClient, i.e., with camel case:
        yield return Case(new(typeof(SanitizePromptInjectionsRequest), JsonSerializerOptions.Web));
        yield return Case(new(typeof(SanitizePromptInjectionsBatchRequest), JsonSerializerOptions.Web));
    }

    /// <summary>
    /// What AI Studio receives from its runtime.
    /// </summary>
    public static IEnumerable<TestCaseData> RuntimeAnswers()
    {
        TestHost.PrepareStaticState();
        var options = RustService.JSON_OPTIONS;

        yield return Case(new(typeof(RequestedSecret), options));
        yield return Case(new(typeof(StoreSecretResponse), options));
        yield return Case(new(typeof(DeleteSecretResponse), options));
        yield return Case(new(typeof(ShareFileResponse), options));
        yield return Case(new(typeof(ImagePrepareResponse), options));
        yield return Case(new(typeof(PreparedModelImage), options));
        yield return Case(new(typeof(TauriEvent), options));
        yield return Case(new(typeof(UpdateResponse), options));
        yield return Case(new(typeof(SetClipboardResponse), options));
        yield return Case(new(typeof(DirectorySelectionResponse), options));
        yield return Case(new(typeof(FileSelectionResponse), options));
        yield return Case(new(typeof(FilesSelectionResponse), options));
        yield return Case(new(typeof(FileSaveResponse), options));
        yield return Case(new(typeof(OpenPathResponse), options));
        yield return Case(new(typeof(OpenDocumentResponse), options));
        yield return Case(new(typeof(ShortcutRegistrationResult), options));
        yield return Case(new(typeof(ShortcutValidationResponse), options));
        yield return Case(new(typeof(ShortcutResponse), options));
        yield return Case(new(typeof(RuntimeInfoResponse), options));
        yield return Case(new(typeof(AppExitResponse), options));
        yield return Case(new(typeof(AppRestartResponse), options));
        yield return Case(new(typeof(InterfaceSizeResponse), options));
        yield return Case(new(typeof(CreateMediaJobResponse), options));
        yield return Case(new(typeof(MediaJobEvent), options));
        yield return Case(new(typeof(List<EnterpriseConfig>), options));
        yield return Case(new(typeof(GetLogPathsResponse), options));
        yield return Case(new(typeof(TokenizerResponse), options));

        // The vector store, with the result of every query its client sends:
        yield return Case(new(typeof(QdrantEdgeInfo), options));
        yield return Case(new(typeof(RustService.DatabaseOperationResponse), options));
        yield return Case(new(typeof(RustService.DatabaseQueryResponse<VectorStoreEnsureResult>), options));
        yield return Case(new(typeof(RustService.DatabaseQueryResponse<bool?>), options));
        yield return Case(new(typeof(RustService.DatabaseQueryResponse<List<VectorSearchResult>>), options));
        yield return Case(new(typeof(RustService.DatabaseQueryResponse<List<StoredVector>>), options));
        yield return Case(new(typeof(RustService.DatabaseQueryResponse<VectorStoreBackupPreparation>), options));
        yield return Case(new(typeof(RustService.DatabaseQueryResponse<long?>), options));

        //
        // The events of a file extraction, which get read with the defaults of System.Text.Json. A converter reads
        // their metadata as the type the JSON tells:
        //
        yield return Case(new(typeof(ContentStreamSseEvent), JsonSerializerOptions.Default));
        yield return Case(new(typeof(string), JsonSerializerOptions.Default), "error of a file extraction");
        foreach (var type in ImplementationsOf(typeof(ContentStreamSseMetadata)))
            yield return Case(new(type, JsonSerializerOptions.Default));

        // The prompt injection filter, which gets called with the defaults of HttpClient, i.e., with camel case:
        yield return Case(new(typeof(SanitizePromptInjectionsResponse), JsonSerializerOptions.Web));
        yield return Case(new(typeof(SanitizePromptInjectionsBatchResponse), JsonSerializerOptions.Web));
    }
}