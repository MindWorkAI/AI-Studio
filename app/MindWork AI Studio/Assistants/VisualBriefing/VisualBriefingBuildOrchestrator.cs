using System.Collections.Concurrent;

using AIStudio.Settings;
using AIStudio.Tools.Services;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Coordinates the persistent, resumable visual briefing build pipeline.
/// </summary>
internal sealed partial class VisualBriefingBuildOrchestrator
{
    private readonly VisualBriefingStore store;
    private readonly VisualBriefingBuildProgressService progressService;
    private readonly ILogger<VisualBriefingBuildOrchestrator> logger;
    private readonly VisualBriefingSourcePreparationService sourcePreparation;
    private readonly VisualBriefingEvidenceStage evidenceStage;
    private readonly VisualBriefingPlanStage planStage;
    private readonly VisualBriefingContentStage contentStage;
    private readonly VisualBriefingPresentationStage presentationStage;

    /// <summary>
    /// Initializes the pipeline. Only the collaborators that other parts of AI Studio also use come
    /// from the service container. The stages and compilers below are implementation details of this
    /// pipeline - one implementation and one caller each - so they are composed here instead of
    /// being registered globally.
    /// </summary>
    /// <param name="store">The briefing store, also used by the preview endpoint and the UI.</param>
    /// <param name="progressService">The progress channel the assistant UI subscribes to.</param>
    /// <param name="rustService">The Rust runtime bridge used while preparing sources.</param>
    /// <param name="loggerFactory">The factory for this pipeline's loggers.</param>
    public VisualBriefingBuildOrchestrator(VisualBriefingStore store, VisualBriefingBuildProgressService progressService, RustService rustService, ILoggerFactory loggerFactory)
    {
        this.store = store;
        this.progressService = progressService;
        this.logger = loggerFactory.CreateLogger<VisualBriefingBuildOrchestrator>();

        var stageRunner = new StructuredLlmStageRunner(loggerFactory.CreateLogger<StructuredLlmStageRunner>());
        this.sourcePreparation = new(store, rustService, loggerFactory.CreateLogger<VisualBriefingSourcePreparationService>());
        this.evidenceStage = new(stageRunner, store, progressService);
        this.planStage = new(stageRunner, store, progressService);
        this.contentStage = new(stageRunner, store, progressService);
        this.presentationStage = new(stageRunner, store, progressService, loggerFactory.CreateLogger<VisualBriefingPresentationStage>());
    }

    /// <summary>
    /// Prevents concurrent active builds for one briefing within the current app process.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> buildLocks = [];

    /// <summary>
    /// Stores safe live diagnostics for the UI.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, VisualBriefingOperationDiagnostics> liveDiagnostics = [];

    /// <summary>
    /// Gets the most recent safe operation diagnostics for a briefing.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <returns>The diagnostics, or <see langword="null"/>.</returns>
    public VisualBriefingOperationDiagnostics? GetDiagnostics(Guid briefingId) =>
        this.liveDiagnostics.GetValueOrDefault(briefingId);

    /// <summary>
    /// Builds or resumes a visual briefing operation.
    /// </summary>
    /// <param name="manifest">The current persisted project manifest.</param>
    /// <param name="mode">The edit mode.</param>
    /// <param name="parentRevisionId">The selected parent revision.</param>
    /// <param name="provider">The selected provider.</param>
    /// <param name="profile">The selected profile.</param>
    /// <param name="reusableContentBuildId">An incompatible update build whose content should be reused as a rebuild.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The terminal build result.</returns>
    public async Task<VisualBriefingBuildResult> BuildAsync(VisualBriefingManifest manifest, VisualBriefingEditMode mode, Guid? parentRevisionId, ProviderSettings provider, Profile profile, Guid? reusableContentBuildId = null, CancellationToken token = default)
    {
        var operationId = Guid.NewGuid();
        var proposedBuildId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var diagnostics = new VisualBriefingOperationDiagnostics
        {
            OperationId = operationId,
            BuildId = proposedBuildId,
            Stage = VisualBriefingBuildStage.SOURCE_PREPARATION,
            ProviderFamily = provider.UsedLLMProvider.ToString(),
            Model = provider.Model.ToString(),
            StartedAtUtc = startedAt,
        };
        
        this.liveDiagnostics[manifest.BriefingId] = diagnostics;
        var gate = this.buildLocks.GetOrAdd(manifest.BriefingId, _ => new(1, 1));
        
        await gate.WaitAsync(token);
        VisualBriefingBuildRecord? build = null;

        IReadOnlyDictionary<string, string> embeddedAssets;
        try
        {
            ValidateProvider(provider);
            ValidateSourceMaterial(manifest, mode);
            var parentContext = await this.LoadParentContextAsync(manifest, mode, parentRevisionId, token);
            VisualBriefingEvidenceArtifact? reusableEvidence = null;
            
            string? reusableEvidenceSourceFingerprint = null;
            string? reusableEvidenceInputFingerprint = null;
            if (reusableContentBuildId is not null)
            {
                var reusable = await this.LoadReusableEvidenceAsync(manifest.BriefingId, reusableContentBuildId.Value, token);
                reusableEvidence = reusable.Evidence;
                reusableEvidenceSourceFingerprint = reusable.SourceFingerprint;
                reusableEvidenceInputFingerprint = reusable.InputFingerprint;
            }

            if (mode is not VisualBriefingEditMode.CHANGE_DESIGN && reusableEvidence is null)
                ValidateVisionCapabilities(manifest, provider);

            var sourceFingerprint = mode is VisualBriefingEditMode.CHANGE_DESIGN ? parentContext.ParentVersion!.AssetHash : await this.ComputeCurrentSourceFingerprintAsync(manifest, token);
            
            if (reusableEvidence is not null &&
                (!string.Equals(
                     sourceFingerprint,
                     reusableEvidenceSourceFingerprint,
                     StringComparison.Ordinal) ||
                 !string.Equals(
                     VisualBriefingEvidenceStage.ComputeInputFingerprint(
                         manifest,
                         provider,
                         profile,
                         sourceFingerprint),
                     reusableEvidenceInputFingerprint,
                     StringComparison.Ordinal)))
                throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED,
                    VisualBriefingBuildStage.SOURCE_PREPARATION,
                    "The sources or evidence settings changed after the evidence was validated. Start a full rebuild.",
                    $"EvidenceArtifactId={reusableEvidence.ArtifactId:D}; Rule={VisualBriefingValidationRule.REFERENCE_INVALID}.");
            
            var inputFingerprint = ComputeBuildInputFingerprint(manifest, mode, parentRevisionId, provider, profile, sourceFingerprint, reusableEvidence?.PayloadHash);
            var now = DateTimeOffset.UtcNow;
            var candidate = new VisualBriefingBuildRecord
            {
                BuildId = proposedBuildId,
                OperationId = operationId,
                BriefingId = manifest.BriefingId,
                Mode = mode,
                ParentRevisionId = parentRevisionId,
                Instruction = manifest.Settings.Instruction,
                InputFingerprint = inputFingerprint,
                SourceFingerprint = sourceFingerprint,
                ProviderFamily = provider.UsedLLMProvider.ToString(),
                Model = provider.Model.ToString(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                EvidenceArtifactId = reusableEvidence?.ArtifactId,
                Stages =
                [
                    .. Enum.GetValues<VisualBriefingBuildStage>().Select(stage => new VisualBriefingBuildStageRecord { Stage = stage })
                ],
            };
            
            var selectedBuild = await this.store.StartOrResumeBuildAsync(candidate, token);
            build = selectedBuild.Build;
            build.OperationId = operationId;
            this.progressService.Publish(build);
            diagnostics.BuildId = build.BuildId;

            if (selectedBuild.Resumed)
                this.logger.LogInformation(Event(VisualBriefingLogEventId.BUILD_RESUMED), "Visual briefing build resumed. OperationId={OperationId} BuildId={BuildId} Mode={Mode} ParentRevisionId={ParentRevisionId} InputFingerprint={InputFingerprint}", operationId, build.BuildId, mode, parentRevisionId, inputFingerprint);
            else
                this.logger.LogInformation(Event(VisualBriefingLogEventId.BUILD_STARTED), "Visual briefing build started. OperationId={OperationId} BuildId={BuildId} Mode={Mode} ParentRevisionId={ParentRevisionId} ProviderFamily={ProviderFamily} Model={Model} SourceCount={SourceCount} AssetCount={AssetCount} InputFingerprint={InputFingerprint}", operationId, build.BuildId, mode, parentRevisionId, provider.UsedLLMProvider, provider.Model, manifest.Sources.Count, manifest.Sources.Count(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET), inputFingerprint);

            VisualBriefingPreparedSources? prepared = null;
            await using var preparedScope = new AsyncDisposableScope(async () =>
            {
                if (prepared is not null)
                    await prepared.DisposeAsync();
            });

            if (mode is VisualBriefingEditMode.CHANGE_DESIGN)
            {
                MarkSkipped(build, VisualBriefingBuildStage.SOURCE_PREPARATION, sourceFingerprint);
                embeddedAssets = VisualBriefingData.ExtractAssets(parentContext.Parts!.Data);
                await this.store.SaveBuildAsync(build, token);
            }
            else
            {
                var sourceStep = new VisualBriefingBuildStep(VisualBriefingBuildStage.SOURCE_PREPARATION, async stepToken =>
                {
                    diagnostics.Stage = VisualBriefingBuildStage.SOURCE_PREPARATION;
                    var stage = GetStage(build, VisualBriefingBuildStage.SOURCE_PREPARATION);
                    stage.Status = VisualBriefingBuildStageStatus.RUNNING;
                    stage.StartedAtUtc = DateTimeOffset.UtcNow;
                    stage.Failure = null;
                    build.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    await this.store.SaveBuildAsync(build, stepToken);
                    this.progressService.Publish(build);
                    this.logger.LogInformation(Event(VisualBriefingLogEventId.SOURCE_PREPARATION_STARTED), "Visual briefing source preparation started. OperationId={OperationId} BuildId={BuildId} SourceCount={SourceCount} AssetCount={AssetCount}", build.OperationId, build.BuildId, manifest.Sources.Count, manifest.Sources.Count(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET));
                    prepared = await this.sourcePreparation.PrepareAsync(manifest, build.OperationId, build.BuildId, stepToken);
                    
                    if (!string.Equals(prepared.SourceFingerprint, build.SourceFingerprint, StringComparison.Ordinal))
                        throw new VisualBriefingBuildException(VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED, VisualBriefingBuildStage.SOURCE_PREPARATION, "The briefing sources changed while the build was starting. Please try again.", "The prepared source fingerprint differs from the persisted build fingerprint.");
                    
                    stage.Status = VisualBriefingBuildStageStatus.COMPLETED;
                    stage.InputFingerprint = build.SourceFingerprint;
                    stage.OutputHash = prepared.SourceFingerprint;
                    stage.FinishedAtUtc = DateTimeOffset.UtcNow;
                    build.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    
                    await this.store.SaveBuildAsync(build, stepToken);
                    this.progressService.Publish(build);
                });
                
                await sourceStep.ExecuteAsync(token);
                embeddedAssets = prepared!.Assets.ToDictionary(asset => asset.Key, asset => asset.Value.DataUrl, StringComparer.Ordinal);
            }

            VisualBriefingEvidenceArtifact evidence;
            if (mode is VisualBriefingEditMode.CHANGE_DESIGN)
            {
                evidence = parentContext.Evidence!;
                MarkSkipped(build, VisualBriefingBuildStage.EVIDENCE, evidence.PayloadHash);
                build.EvidenceArtifactId = evidence.ArtifactId;
            }
            else if (reusableEvidence is not null)
            {
                evidence = reusableEvidence;
                MarkSkipped(build, VisualBriefingBuildStage.EVIDENCE, evidence.PayloadHash);
                build.EvidenceArtifactId = evidence.ArtifactId;
            }
            else
            {
                diagnostics.Stage = VisualBriefingBuildStage.EVIDENCE;
                evidence = await this.evidenceStage.ExecuteAsync(manifest, provider, profile, prepared!, build, token);
            }
            
            diagnostics.ContentHashes["evidence"] = evidence.PayloadHash;
            diagnostics.ArtifactIds["evidence"] = evidence.ArtifactId;
            this.progressService.Publish(build);

            VisualBriefingPlanArtifact plan;
            if (mode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT)
            {
                plan = parentContext.Plan!;
                MarkSkipped(build, VisualBriefingBuildStage.PLAN, plan.PayloadHash);
                build.PlanArtifactId = plan.ArtifactId;
                await this.store.SaveBuildAsync(build, token);
            }
            else
            {
                diagnostics.Stage = VisualBriefingBuildStage.PLAN;
                plan = await this.planStage.ExecuteAsync(manifest, provider, profile, evidence, build, token);
            }
            
            diagnostics.ContentHashes["plan"] = plan.PayloadHash;
            diagnostics.ArtifactIds["plan"] = plan.ArtifactId;
            this.progressService.Publish(build);

            VisualBriefingContentArtifact content;
            if (mode is VisualBriefingEditMode.CHANGE_DESIGN)
            {
                content = parentContext.Content!;
                MarkSkipped(build, VisualBriefingBuildStage.CONTENT, content.PayloadHash);
                build.ContentArtifactId = content.ArtifactId;
                await this.store.SaveBuildAsync(build, token);
            }
            else
            {
                diagnostics.Stage = VisualBriefingBuildStage.CONTENT;
                
                try
                {
                    content = await this.contentStage.ExecuteAsync(manifest, provider, profile, evidence, plan, build, token);
                }
                catch (VisualBriefingBuildException exception) when (mode is VisualBriefingEditMode.UPDATE_CONTENT && exception.Code is VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID && build.Failure?.ValidationRule is VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID)
                {
                    var failure = new VisualBriefingFailure
                    {
                        Code = VisualBriefingFailureCode.CONTENT_SIGNATURE_INCOMPATIBLE,
                        Stage = VisualBriefingBuildStage.CONTENT,
                        ValidationRule = VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID,
                        UserMessage = "The updated evidence no longer fulfils the frozen plan. Continue as a rebuild to reuse the validated evidence.",
                        TechnicalDetails = $"Rule={VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID}; EvidenceArtifactId={evidence.ArtifactId:D}; PlanArtifactId={plan.ArtifactId:D}.",
                    };
                    
                    var contentBuildStage = GetStage(build, VisualBriefingBuildStage.CONTENT);
                    contentBuildStage.Status = VisualBriefingBuildStageStatus.FAILED;
                    contentBuildStage.FinishedAtUtc ??= DateTimeOffset.UtcNow;
                    contentBuildStage.Failure = failure;
                    
                    build.Status = VisualBriefingBuildStatus.AWAITING_REBUILD;
                    build.Failure = failure;
                    build.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    
                    await this.store.SaveBuildAsync(build, token);
                    this.progressService.Publish(build);
                    
                    return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: true);
                }
            }
            
            diagnostics.ContentHashes["content"] = content.PayloadHash;
            diagnostics.ArtifactIds["content"] = content.ArtifactId;
            this.progressService.Publish(build);

            VisualBriefingPresentationArtifact presentation;
            if (mode is VisualBriefingEditMode.UPDATE_CONTENT)
            {
                presentation = parentContext.Presentation!;
                MarkSkipped(build, VisualBriefingBuildStage.DESIGN, presentation.PayloadHash);
                build.PresentationArtifactId = presentation.ArtifactId;
                await this.store.SaveBuildAsync(build, token);
            }
            else
            {
                diagnostics.Stage = VisualBriefingBuildStage.DESIGN;
                presentation = await this.presentationStage.ExecuteAsync(manifest, provider, profile, plan, content, mode is VisualBriefingEditMode.CHANGE_DESIGN ? parentContext.Presentation : null, build, token);
            }
            
            diagnostics.ContentHashes["design"] = presentation.PayloadHash;
            diagnostics.ArtifactIds["design"] = presentation.ArtifactId;
            this.progressService.Publish(build);

            diagnostics.Stage = VisualBriefingBuildStage.COMPILATION;
            var compilationStage = GetStage(build, VisualBriefingBuildStage.COMPILATION);
            compilationStage.Status = VisualBriefingBuildStageStatus.RUNNING;
            compilationStage.StartedAtUtc = DateTimeOffset.UtcNow;
            compilationStage.InputFingerprint = VisualBriefingHashing.ComputeSections(plan.PayloadHash, content.PayloadHash, presentation.PayloadHash, VisualBriefingVersions.SCHEMA.ToString());
            
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);
            
            var compiled = VisualBriefingLayoutCompiler.Compile(plan, content, presentation.Layout, presentation.Profile);
            
            if (!string.Equals(compiled.TemplateHash, presentation.TemplateHash, StringComparison.Ordinal) || !string.Equals(compiled.CssHash, presentation.CssHash, StringComparison.Ordinal))
                throw new VisualBriefingBuildException(VisualBriefingFailureCode.PRESENTATION_INVALID, VisualBriefingBuildStage.COMPILATION, "The deterministic briefing compiler produced an inconsistent result.", $"Rule={VisualBriefingValidationRule.COMPILER_OUTPUT_INVALID}; DesignArtifactId={presentation.ArtifactId:D}.");
            
            compilationStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            compilationStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            compilationStage.OutputHash = VisualBriefingHashing.ComputeSections(VisualBriefingHashing.Compute(VisualBriefingHashing.CanonicalJson(compiled.Data)), compiled.TemplateHash, compiled.CssHash);
            
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);

            diagnostics.Stage = VisualBriefingBuildStage.ASSEMBLY;
            var revisionId = build.RevisionId ?? Guid.NewGuid();
            var revisionCreatedAt = DateTimeOffset.UtcNow;
            build.RevisionId = revisionId;
            
            var assemblyStage = GetStage(build, VisualBriefingBuildStage.ASSEMBLY);
            assemblyStage.Status = VisualBriefingBuildStageStatus.RUNNING;
            assemblyStage.StartedAtUtc = revisionCreatedAt;
            assemblyStage.InputFingerprint = VisualBriefingHashing.ComputeSections(
                content.PayloadHash,
                presentation.PayloadHash,
                VisualBriefingHashing.Compute(
                    string.Join('\u001e', embeddedAssets.OrderBy(asset => asset.Key, StringComparer.Ordinal)
                            .Select(asset => $"{asset.Key}:{VisualBriefingHashing.Compute(asset.Value)}"))),
                parentContext.ParentVersion?.RuntimeHash,
                manifest.Settings.TargetLanguage.ToString(),
                manifest.Settings.CustomTargetLanguage,
                manifest.Settings.ProtectionLevel.ToString(),
                VisualBriefingHashing.Compute(manifest.Settings.CustomProtectionLevel),
                VisualBriefingVersions.ARTIFACT.ToString(),
                VisualBriefingVersions.SCHEMA.ToString(),
                VisualBriefingVersions.RUNTIME.ToString());
            
            var commitStage = GetStage(build, VisualBriefingBuildStage.COMMIT);
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);
            this.logger.LogInformation(Event(VisualBriefingLogEventId.ASSEMBLY_STARTED), "Visual briefing assembly started. OperationId={OperationId} BuildId={BuildId} ContentHash={ContentHash} PresentationHash={PresentationHash} AssetCount={AssetCount}", build.OperationId, build.BuildId, content.PayloadHash, presentation.PayloadHash, embeddedAssets.Count);

            var contributions = new List<VisualBriefingModelContribution>
            {
                new(VisualBriefingModelRole.EVIDENCE, evidence.Model),
                new(VisualBriefingModelRole.PLAN, plan.Model),
                new(VisualBriefingModelRole.CONTENT, content.Model),
                new(VisualBriefingModelRole.DESIGN, presentation.Model),
            };
            
            var revision = await this.store.AddRevisionAsync(new(manifest.BriefingId, parentRevisionId, mode, manifest.Settings.Instruction,
                compiled.Data, compiled.TemplateHtml, compiled.Css, VisualBriefingModelNames.ExportLabel(provider), "MindWork AI Studio",
                content.ArtifactId, presentation.ArtifactId, build.BuildId, build.OperationId, contributions, revisionId, revisionCreatedAt, embeddedAssets,
                content.AssetPlan, evidence.ArtifactId, plan.ArtifactId), token);
            
            if (!revision.Success || revision.Version is null)
            {
                var code = revision.Issue.Contains("did not change", StringComparison.OrdinalIgnoreCase) ? VisualBriefingFailureCode.NO_CHANGES : VisualBriefingFailureCode.STORE_FAILED;
                throw new VisualBriefingBuildException(code, VisualBriefingBuildStage.COMMIT, revision.Issue, $"The immutable revision commit was rejected. StoreIssue={revision.Issue}");
            }

            assemblyStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            assemblyStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            assemblyStage.OutputHash = revision.Version.DocumentHash;
            
            commitStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            commitStage.StartedAtUtc ??= assemblyStage.FinishedAtUtc;
            commitStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            commitStage.InputFingerprint = revision.Version.DocumentHash;
            commitStage.OutputHash = revision.Version.DocumentHash;
            
            build.CommittedRevisionId = revision.Version.RevisionId;
            build.Status = VisualBriefingBuildStatus.COMPLETED;
            build.Failure = null;
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);
            
            diagnostics.ContentHashes["document"] = revision.Version.DocumentHash;
            diagnostics.FinishedAtUtc = DateTimeOffset.UtcNow;
            
            this.logger.LogInformation(Event(VisualBriefingLogEventId.REVISION_COMMITTED), "Visual briefing revision committed. OperationId={OperationId} BuildId={BuildId} VersionNumber={VersionNumber} RevisionId={RevisionId} DocumentHash={DocumentHash}", build.OperationId, build.BuildId, revision.Version.VersionNumber, revision.Version.RevisionId, revision.Version.DocumentHash);
            return new(true, revision.Version, string.Empty, VisualBriefingFailureCode.NONE, diagnostics, false);
        }
        catch (OperationCanceledException)
        {
            var failure = new VisualBriefingFailure
            {
                Code = VisualBriefingFailureCode.CANCELED,
                Stage = diagnostics.Stage,
                UserMessage = "The visual briefing generation was canceled.",
                TechnicalDetails = "The operation cancellation token was signaled.",
            };
            
            if (build is not null)
                await this.SaveTerminalStateAsync(build, VisualBriefingBuildStatus.CANCELED, failure, CancellationToken.None);
            
            return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: false);
        }
        catch (VisualBriefingBuildException exception)
        {
            var failure = new VisualBriefingFailure
            {
                Code = exception.Code,
                Stage = exception.Stage,
                ValidationRule = build?.Failure?.ValidationRule ??
                                 (exception.Stage is VisualBriefingBuildStage.COMPILATION
                                     ? VisualBriefingValidationRule.COMPILER_OUTPUT_INVALID
                                     : VisualBriefingValidationRule.NONE),
                UserMessage = exception.Message,
                TechnicalDetails = exception.TechnicalDetails,
                StructuredResponse = build?.Failure?.StructuredResponse,
            };
            
            if (build is not null)
                await this.SaveTerminalStateAsync(build, VisualBriefingBuildStatus.FAILED, failure, CancellationToken.None);
            
            this.logger.LogWarning(Event(VisualBriefingLogEventId.VALIDATION_REJECTED), "Visual briefing build rejected. OperationId={OperationId} BuildId={BuildId} Stage={Stage} FailureCode={FailureCode} ValidationRule={ValidationRule} TechnicalDetails={TechnicalDetails}", operationId, build?.BuildId ?? proposedBuildId, exception.Stage, exception.Code, failure.ValidationRule, failure.TechnicalDetails);
            return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: false);
        }
        catch (Exception exception)
        {
            var failure = new VisualBriefingFailure
            {
                Code = VisualBriefingFailureCode.UNEXPECTED,
                Stage = diagnostics.Stage,
                UserMessage = "The visual briefing could not be completed because of an unexpected internal error.",
                TechnicalDetails = $"{exception.GetType().Name} at stage {diagnostics.Stage}.",
            };
            
            if (build is not null)
                await this.SaveTerminalStateAsync(build, VisualBriefingBuildStatus.FAILED, failure, CancellationToken.None);
            
            this.logger.LogError(Event(VisualBriefingLogEventId.BUILD_FINISHED), "Unexpected visual briefing build failure. OperationId={OperationId} BuildId={BuildId} Stage={Stage} FailureCode={FailureCode} ExceptionType={ExceptionType}", operationId, build?.BuildId ?? proposedBuildId, diagnostics.Stage, failure.Code, exception.GetType().Name);
            return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Adapts asynchronous cleanup to an await-using scope.
    /// </summary>
    /// <param name="dispose">The cleanup action.</param>
    private sealed class AsyncDisposableScope(Func<Task> dispose) : IAsyncDisposable
    {
        /// <summary>
        /// Runs the cleanup action.
        /// </summary>
        /// <returns>A value task representing cleanup.</returns>
        public async ValueTask DisposeAsync() => await dispose();
    }
}