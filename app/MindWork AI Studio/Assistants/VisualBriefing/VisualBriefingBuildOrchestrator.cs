using System.Collections.Concurrent;

using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Rust;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Contains the terminal result of one visual briefing build.
/// </summary>
/// <param name="Success">Whether a revision was committed.</param>
/// <param name="Version">The committed immutable version.</param>
/// <param name="Issue">The user-safe issue.</param>
/// <param name="FailureCode">The stable failure code.</param>
/// <param name="Diagnostics">Safe technical diagnostics.</param>
/// <param name="CanContinueAsRebuild">Whether incompatible valid content can continue without another content call.</param>
internal sealed record VisualBriefingBuildResult(
    bool Success,
    VisualBriefingVersion? Version,
    string Issue,
    VisualBriefingFailureCode FailureCode,
    VisualBriefingOperationDiagnostics Diagnostics,
    bool CanContinueAsRebuild);

/// <summary>
/// Coordinates the persistent, resumable visual briefing build pipeline.
/// </summary>
internal sealed class VisualBriefingBuildOrchestrator(
    VisualBriefingStore store,
    IVisualBriefingSourcePreparation sourcePreparation,
    IVisualBriefingEvidenceStage evidenceStage,
    IVisualBriefingPlanStage planStage,
    IVisualBriefingContentStage contentStage,
    IVisualBriefingPresentationStage presentationStage,
    VisualBriefingLayoutCompiler layoutCompiler,
    VisualBriefingBuildProgressService progressService,
    ILogger<VisualBriefingBuildOrchestrator> logger)
{
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
    public async Task<VisualBriefingBuildResult> BuildAsync(
        VisualBriefingManifest manifest,
        VisualBriefingEditMode mode,
        Guid? parentRevisionId,
        ProviderSettings provider,
        Profile profile,
        Guid? reusableContentBuildId = null,
        CancellationToken token = default)
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
        try
        {
            ValidateProvider(provider);
            var parentContext = await this.LoadParentContextAsync(
                manifest,
                mode,
                parentRevisionId,
                token);
            VisualBriefingEvidenceArtifact? reusableEvidence = null;
            string? reusableEvidenceSourceFingerprint = null;
            string? reusableEvidenceInputFingerprint = null;
            if (reusableContentBuildId is not null)
            {
                var reusable = await this.LoadReusableEvidenceAsync(
                    manifest.BriefingId,
                    reusableContentBuildId.Value,
                    token);
                reusableEvidence = reusable.Evidence;
                reusableEvidenceSourceFingerprint = reusable.SourceFingerprint;
                reusableEvidenceInputFingerprint = reusable.InputFingerprint;
            }

            if (mode is not VisualBriefingEditMode.CHANGE_DESIGN && reusableEvidence is null)
                ValidateVisionCapabilities(manifest, provider);

            var sourceFingerprint = mode is VisualBriefingEditMode.CHANGE_DESIGN
                ? parentContext.ParentVersion!.AssetHash
                : await this.ComputeCurrentSourceFingerprintAsync(manifest, token);
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
            var inputFingerprint = ComputeBuildInputFingerprint(
                manifest,
                mode,
                parentRevisionId,
                provider,
                profile,
                sourceFingerprint,
                reusableEvidence?.PayloadHash);
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
                Stages = Enum.GetValues<VisualBriefingBuildStage>()
                    .Select(stage => new VisualBriefingBuildStageRecord { Stage = stage })
                    .ToList(),
            };
            var selectedBuild = await store.StartOrResumeBuildAsync(candidate, token);
            build = selectedBuild.Build;
            build.OperationId = operationId;
            progressService.Publish(build);
            diagnostics.BuildId = build.BuildId;
            if (selectedBuild.Resumed)
            {
                logger.LogInformation(
                    Event(VisualBriefingLogEventId.BUILD_RESUMED),
                    "Visual briefing build resumed. OperationId={OperationId} BuildId={BuildId} Mode={Mode} ParentRevisionId={ParentRevisionId} InputFingerprint={InputFingerprint}",
                    operationId,
                    build.BuildId,
                    mode,
                    parentRevisionId,
                    inputFingerprint);
            }
            else
            {
                logger.LogInformation(
                    Event(VisualBriefingLogEventId.BUILD_STARTED),
                    "Visual briefing build started. OperationId={OperationId} BuildId={BuildId} Mode={Mode} ParentRevisionId={ParentRevisionId} ProviderFamily={ProviderFamily} Model={Model} SourceCount={SourceCount} AssetCount={AssetCount} InputFingerprint={InputFingerprint}",
                    operationId,
                    build.BuildId,
                    mode,
                    parentRevisionId,
                    provider.UsedLLMProvider,
                    provider.Model,
                    manifest.Sources.Count,
                    manifest.Sources.Count(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET),
                    inputFingerprint);
            }

            VisualBriefingPreparedSources? prepared = null;
            await using var preparedScope = new AsyncDisposableScope(async () =>
            {
                if (prepared is not null)
                    await prepared.DisposeAsync();
            });
            IReadOnlyDictionary<string, string> embeddedAssets;
            if (mode is VisualBriefingEditMode.CHANGE_DESIGN)
            {
                MarkSkipped(build, VisualBriefingBuildStage.SOURCE_PREPARATION, sourceFingerprint);
                embeddedAssets = VisualBriefingData.ExtractAssets(parentContext.Parts!.Data);
                await store.SaveBuildAsync(build, token);
            }
            else
            {
                var sourceStep = new VisualBriefingBuildStep(
                    VisualBriefingBuildStage.SOURCE_PREPARATION,
                    async stepToken =>
                    {
                        diagnostics.Stage = VisualBriefingBuildStage.SOURCE_PREPARATION;
                        var stage = GetStage(build, VisualBriefingBuildStage.SOURCE_PREPARATION);
                        stage.Status = VisualBriefingBuildStageStatus.RUNNING;
                        stage.StartedAtUtc = DateTimeOffset.UtcNow;
                        stage.Failure = null;
                        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
                        await store.SaveBuildAsync(build, stepToken);
                        progressService.Publish(build);
                        logger.LogInformation(
                            Event(VisualBriefingLogEventId.SOURCE_PREPARATION_STARTED),
                            "Visual briefing source preparation started. OperationId={OperationId} BuildId={BuildId} SourceCount={SourceCount} AssetCount={AssetCount}",
                            build.OperationId,
                            build.BuildId,
                            manifest.Sources.Count,
                            manifest.Sources.Count(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET));
                        prepared = await sourcePreparation.PrepareAsync(
                            manifest,
                            build.OperationId,
                            build.BuildId,
                            stepToken);
                        if (!string.Equals(prepared.SourceFingerprint, build.SourceFingerprint, StringComparison.Ordinal))
                            throw new VisualBriefingBuildException(
                                VisualBriefingFailureCode.SOURCE_PREPARATION_FAILED,
                                VisualBriefingBuildStage.SOURCE_PREPARATION,
                                "The briefing sources changed while the build was starting. Please try again.",
                                "The prepared source fingerprint differs from the persisted build fingerprint.");
                        stage.Status = VisualBriefingBuildStageStatus.COMPLETED;
                        stage.InputFingerprint = build.SourceFingerprint;
                        stage.OutputHash = prepared.SourceFingerprint;
                        stage.FinishedAtUtc = DateTimeOffset.UtcNow;
                        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
                        await store.SaveBuildAsync(build, stepToken);
                        progressService.Publish(build);
                    });
                await sourceStep.ExecuteAsync(token);
                embeddedAssets = prepared!.Assets.ToDictionary(
                    asset => asset.Key,
                    asset => asset.Value.DataUrl,
                    StringComparer.Ordinal);
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
                evidence = await evidenceStage.ExecuteAsync(
                    manifest,
                    provider,
                    profile,
                    prepared!,
                    build,
                    token);
            }
            diagnostics.ContentHashes["evidence"] = evidence.PayloadHash;
            diagnostics.ArtifactIds["evidence"] = evidence.ArtifactId;
            progressService.Publish(build);

            VisualBriefingPlanArtifact plan;
            if (mode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT)
            {
                plan = parentContext.Plan!;
                MarkSkipped(build, VisualBriefingBuildStage.PLAN, plan.PayloadHash);
                build.PlanArtifactId = plan.ArtifactId;
                await store.SaveBuildAsync(build, token);
            }
            else
            {
                diagnostics.Stage = VisualBriefingBuildStage.PLAN;
                plan = await planStage.ExecuteAsync(
                    manifest,
                    provider,
                    profile,
                    evidence,
                    build,
                    token);
            }
            diagnostics.ContentHashes["plan"] = plan.PayloadHash;
            diagnostics.ArtifactIds["plan"] = plan.ArtifactId;
            progressService.Publish(build);

            VisualBriefingContentArtifact content;
            if (mode is VisualBriefingEditMode.CHANGE_DESIGN)
            {
                content = parentContext.Content!;
                MarkSkipped(build, VisualBriefingBuildStage.CONTENT, content.PayloadHash);
                build.ContentArtifactId = content.ArtifactId;
                await store.SaveBuildAsync(build, token);
            }
            else
            {
                diagnostics.Stage = VisualBriefingBuildStage.CONTENT;
                try
                {
                    content = await contentStage.ExecuteAsync(
                        manifest,
                        provider,
                        profile,
                        evidence,
                        plan,
                        build,
                        token);
                }
                catch (VisualBriefingBuildException exception)
                    when (mode is VisualBriefingEditMode.UPDATE_CONTENT &&
                          exception.Code is VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID &&
                          build.Failure?.ValidationRule is VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID)
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
                    await store.SaveBuildAsync(build, token);
                    progressService.Publish(build);
                    return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: true);
                }
            }
            diagnostics.ContentHashes["content"] = content.PayloadHash;
            diagnostics.ArtifactIds["content"] = content.ArtifactId;
            progressService.Publish(build);

            VisualBriefingPresentationArtifact presentation;
            if (mode is VisualBriefingEditMode.UPDATE_CONTENT)
            {
                presentation = parentContext.Presentation!;
                MarkSkipped(build, VisualBriefingBuildStage.DESIGN, presentation.PayloadHash);
                build.PresentationArtifactId = presentation.ArtifactId;
                await store.SaveBuildAsync(build, token);
            }
            else
            {
                diagnostics.Stage = VisualBriefingBuildStage.DESIGN;
                presentation = await presentationStage.ExecuteAsync(
                    manifest,
                    provider,
                    profile,
                    plan,
                    content,
                    mode is VisualBriefingEditMode.CHANGE_DESIGN ? parentContext.Presentation : null,
                    build,
                    token);
            }
            diagnostics.ContentHashes["design"] = presentation.PayloadHash;
            diagnostics.ArtifactIds["design"] = presentation.ArtifactId;
            progressService.Publish(build);

            diagnostics.Stage = VisualBriefingBuildStage.COMPILATION;
            var compilationStage = GetStage(build, VisualBriefingBuildStage.COMPILATION);
            compilationStage.Status = VisualBriefingBuildStageStatus.RUNNING;
            compilationStage.StartedAtUtc = DateTimeOffset.UtcNow;
            compilationStage.InputFingerprint = VisualBriefingHashing.ComputeSections(
                plan.PayloadHash,
                content.PayloadHash,
                presentation.PayloadHash,
                VisualBriefingVersions.SCHEMA.ToString());
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await store.SaveBuildAsync(build, token);
            progressService.Publish(build);
            var compiled = layoutCompiler.Compile(plan, content, presentation.Layout, presentation.Tokens);
            if (!string.Equals(compiled.TemplateHash, presentation.TemplateHash, StringComparison.Ordinal) ||
                !string.Equals(compiled.CssHash, presentation.CssHash, StringComparison.Ordinal))
                throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.PRESENTATION_INVALID,
                    VisualBriefingBuildStage.COMPILATION,
                    "The deterministic briefing compiler produced an inconsistent result.",
                    $"Rule={VisualBriefingValidationRule.COMPILER_OUTPUT_INVALID}; DesignArtifactId={presentation.ArtifactId:D}.");
            compilationStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            compilationStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            compilationStage.OutputHash = VisualBriefingHashing.ComputeSections(
                VisualBriefingHashing.Compute(VisualBriefingHashing.CanonicalJson(compiled.Data)),
                compiled.TemplateHash,
                compiled.CssHash);
            await store.SaveBuildAsync(build, token);
            progressService.Publish(build);

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
                    string.Join(
                        '\u001e',
                        embeddedAssets.OrderBy(asset => asset.Key, StringComparer.Ordinal)
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
            await store.SaveBuildAsync(build, token);
            progressService.Publish(build);
            logger.LogInformation(
                Event(VisualBriefingLogEventId.ASSEMBLY_STARTED),
                "Visual briefing assembly started. OperationId={OperationId} BuildId={BuildId} ContentHash={ContentHash} PresentationHash={PresentationHash} AssetCount={AssetCount}",
                build.OperationId,
                build.BuildId,
                content.PayloadHash,
                presentation.PayloadHash,
                embeddedAssets.Count);

            var contributions = new List<VisualBriefingModelContribution>
            {
                new(VisualBriefingModelRole.EVIDENCE, evidence.Model),
                new(VisualBriefingModelRole.PLAN, plan.Model),
                new(VisualBriefingModelRole.CONTENT, content.Model),
                new(VisualBriefingModelRole.DESIGN, presentation.Model),
            };
            var revision = await store.AddRevisionAsync(new(
                manifest.BriefingId,
                parentRevisionId,
                mode,
                manifest.Settings.Instruction,
                compiled.Data,
                compiled.TemplateHtml,
                compiled.Css,
                VisualBriefingModelNames.ExportLabel(provider.Model),
                "MindWork AI Studio",
                content.ArtifactId,
                presentation.ArtifactId,
                build.BuildId,
                build.OperationId,
                contributions,
                revisionId,
                revisionCreatedAt,
                embeddedAssets,
                content.CustomLanguageLabels,
                content.AssetPlan,
                evidence.ArtifactId,
                plan.ArtifactId), token);
            if (!revision.Success || revision.Version is null)
            {
                var code = revision.Issue.Contains("did not change", StringComparison.OrdinalIgnoreCase)
                    ? VisualBriefingFailureCode.NO_CHANGES
                    : VisualBriefingFailureCode.STORE_FAILED;
                throw new VisualBriefingBuildException(
                    code,
                    VisualBriefingBuildStage.COMMIT,
                    revision.Issue,
                    "The immutable revision commit was rejected.");
            }

            assemblyStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            assemblyStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            assemblyStage.OutputHash = revision.Version.PayloadHash;
            commitStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            commitStage.StartedAtUtc ??= assemblyStage.FinishedAtUtc;
            commitStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            commitStage.InputFingerprint = revision.Version.PayloadHash;
            commitStage.OutputHash = revision.Version.PayloadHash;
            build.CommittedRevisionId = revision.Version.RevisionId;
            build.Status = VisualBriefingBuildStatus.COMPLETED;
            build.Failure = null;
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await store.SaveBuildAsync(build, token);
            progressService.Publish(build);
            diagnostics.ContentHashes["payload"] = revision.Version.PayloadHash;
            diagnostics.FinishedAtUtc = DateTimeOffset.UtcNow;
            logger.LogInformation(
                Event(VisualBriefingLogEventId.REVISION_COMMITTED),
                "Visual briefing revision committed. OperationId={OperationId} BuildId={BuildId} VersionNumber={VersionNumber} RevisionId={RevisionId} PayloadHash={PayloadHash}",
                build.OperationId,
                build.BuildId,
                revision.Version.VersionNumber,
                revision.Version.RevisionId,
                revision.Version.PayloadHash);
            return new(
                true,
                revision.Version,
                string.Empty,
                VisualBriefingFailureCode.NONE,
                diagnostics,
                false);
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
            {
                await this.SaveTerminalStateAsync(
                    build,
                    VisualBriefingBuildStatus.CANCELED,
                    failure,
                    CancellationToken.None);
            }
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
            logger.LogWarning(
                Event(VisualBriefingLogEventId.VALIDATION_REJECTED),
                "Visual briefing build rejected. OperationId={OperationId} BuildId={BuildId} Stage={Stage} FailureCode={FailureCode} ValidationRule={ValidationRule} TechnicalDetails={TechnicalDetails}",
                operationId,
                build?.BuildId ?? proposedBuildId,
                exception.Stage,
                exception.Code,
                failure.ValidationRule,
                failure.TechnicalDetails);
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
            logger.LogError(
                Event(VisualBriefingLogEventId.BUILD_FINISHED),
                "Unexpected visual briefing build failure. OperationId={OperationId} BuildId={BuildId} Stage={Stage} FailureCode={FailureCode} ExceptionType={ExceptionType}",
                operationId,
                build?.BuildId ?? proposedBuildId,
                diagnostics.Stage,
                failure.Code,
                exception.GetType().Name);
            return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Loads and verifies the selected parent revision and its intermediate artifacts.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="mode">The edit mode.</param>
    /// <param name="parentRevisionId">The parent revision identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The parent context.</returns>
    private async Task<ParentContext> LoadParentContextAsync(
        VisualBriefingManifest manifest,
        VisualBriefingEditMode mode,
        Guid? parentRevisionId,
        CancellationToken token)
    {
        if (mode is VisualBriefingEditMode.INITIAL)
            return new(null, null, null, null, null, null);
        if (parentRevisionId is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                "The selected parent revision could not be loaded.",
                "A non-initial build has no parent revision ID.");

        var version = manifest.Versions.FirstOrDefault(candidate => candidate.RevisionId == parentRevisionId);
        if (mode is VisualBriefingEditMode.REBUILD)
            return version is not null
                ? new(version, null, null, null, null, null)
                : throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                    VisualBriefingBuildStage.SOURCE_PREPARATION,
                    "The selected parent revision could not be loaded.",
                    "The rebuild parent revision does not exist.");
        var parts = await store.ReadVersionPartsAsync(manifest.BriefingId, parentRevisionId.Value, token);
        if (version is null || parts is null ||
            version.EvidenceArtifactId is null ||
            version.PlanArtifactId is null ||
            version.ContentArtifactId is null ||
            version.PresentationArtifactId is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                "The selected parent revision is invalid or incomplete.",
                "The parent revision or its intermediate artifact references are unavailable.");

        var evidence = await store.ReadEvidenceArtifactAsync(
            manifest.BriefingId,
            version.EvidenceArtifactId.Value,
            token);
        var plan = await store.ReadPlanArtifactAsync(
            manifest.BriefingId,
            version.PlanArtifactId.Value,
            token);
        var content = await store.ReadContentArtifactAsync(
            manifest.BriefingId,
            version.ContentArtifactId.Value,
            token);
        var presentation = await store.ReadPresentationArtifactAsync(
            manifest.BriefingId,
            version.PresentationArtifactId.Value,
            token);
        if (evidence is null || plan is null || content is null || presentation is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                "The selected parent revision has damaged intermediate artifacts.",
                "A referenced evidence, plan, content, or design artifact failed hash validation.");
        return new(version, parts, evidence, plan, content, presentation);
    }

    /// <summary>
    /// Loads validated evidence for the explicit continue-as-rebuild action.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="buildId">The source build identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The reusable evidence artifact.</returns>
    private async Task<(VisualBriefingEvidenceArtifact Evidence, string SourceFingerprint, string InputFingerprint)> LoadReusableEvidenceAsync(
        Guid briefingId,
        Guid buildId,
        CancellationToken token)
    {
        var sourceBuild = await store.LoadBuildAsync(briefingId, buildId, token);
        if (sourceBuild is null ||
            sourceBuild.Status is not VisualBriefingBuildStatus.AWAITING_REBUILD ||
            sourceBuild.EvidenceArtifactId is null)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.CONTENT_SIGNATURE_INCOMPATIBLE,
                VisualBriefingBuildStage.EVIDENCE,
                "The validated evidence is no longer available to continue as a rebuild.",
                "The source build is not awaiting rebuild or has no evidence artifact.");
        var evidence = await store.ReadEvidenceArtifactAsync(
                   briefingId,
                   sourceBuild.EvidenceArtifactId.Value,
                   token)
               ?? throw new VisualBriefingBuildException(
                   VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                   VisualBriefingBuildStage.EVIDENCE,
                   "The validated evidence artifact is damaged.",
                   "The reusable evidence artifact failed hash validation.");
        var persistedEvidenceStage = sourceBuild.Stages.FirstOrDefault(stage =>
            stage.Stage is VisualBriefingBuildStage.EVIDENCE &&
            stage.Status is VisualBriefingBuildStageStatus.COMPLETED);
        if (persistedEvidenceStage is null || string.IsNullOrWhiteSpace(persistedEvidenceStage.InputFingerprint))
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                VisualBriefingBuildStage.EVIDENCE,
                "The validated evidence dependencies are unavailable.",
                "The reusable evidence stage has no validated input fingerprint.");
        return (evidence, sourceBuild.SourceFingerprint, persistedEvidenceStage.InputFingerprint);
    }

    /// <summary>
    /// Computes a current source fingerprint including persistent transcript hashes.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The current source fingerprint.</returns>
    private async Task<string> ComputeCurrentSourceFingerprintAsync(
        VisualBriefingManifest manifest,
        CancellationToken token)
    {
        List<string> entries = [];
        foreach (var source in manifest.Sources.OrderBy(source => source.SourceId))
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(source.Path))
                throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.SOURCE_UNREACHABLE,
                    VisualBriefingBuildStage.SOURCE_PREPARATION,
                    "A briefing source is no longer reachable.",
                    $"Source {source.SourceId:D} failed the reachability check.");
            var sourceHash = await VisualBriefingHashing.ComputeFileAsync(source.Path, token);
            var transcriptHash = string.Empty;
            if (source.IsMedia)
            {
                var transcript = await store.ReadTranscriptAsync(manifest.BriefingId, source.SourceId, token);
                if (string.IsNullOrWhiteSpace(transcript) ||
                    source.TranscriptStatus is not VisualBriefingTranscriptStatus.CURRENT)
                    throw new VisualBriefingBuildException(
                        VisualBriefingFailureCode.TRANSCRIPT_UNAVAILABLE,
                        VisualBriefingBuildStage.SOURCE_PREPARATION,
                        "A media transcript is missing or outdated.",
                        $"Transcript status for source {source.SourceId:D} is {source.TranscriptStatus}.");
                transcriptHash = VisualBriefingHashing.Compute(transcript);
            }
            entries.Add(string.Join(
                '\u001f',
                source.SourceId,
                source.Kind,
                source.AssetId,
                sourceHash,
                transcriptHash));
        }
        return VisualBriefingHashing.ComputeSections(
            [manifest.Settings.OptimizeImages.ToString(), .. entries]);
    }

    /// <summary>
    /// Computes the full safe build input fingerprint.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="mode">The edit mode.</param>
    /// <param name="parentRevisionId">The parent revision.</param>
    /// <param name="provider">The provider.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="sourceFingerprint">The source fingerprint.</param>
    /// <param name="reusedContentHash">The optional reused content hash.</param>
    /// <returns>The build input fingerprint.</returns>
    private static string ComputeBuildInputFingerprint(
        VisualBriefingManifest manifest,
        VisualBriefingEditMode mode,
        Guid? parentRevisionId,
        ProviderSettings provider,
        Profile profile,
        string sourceFingerprint,
        string? reusedContentHash) =>
        VisualBriefingHashing.ComputeSections(
            mode.ToString(),
            parentRevisionId?.ToString("D"),
            provider.Id,
            provider.Model.Id,
            profile.Id,
            sourceFingerprint,
            VisualBriefingHashing.Compute(manifest.Settings.Instruction),
            manifest.Settings.TargetLanguage.ToString(),
            manifest.Settings.CustomTargetLanguage,
            manifest.Settings.AudienceProfile.ToString(),
            manifest.Settings.AudienceAgeGroup.ToString(),
            manifest.Settings.AudienceOrganizationalLevel.ToString(),
            manifest.Settings.AudienceExpertise.ToString(),
            manifest.Settings.ShowSourceReferences.ToString(),
            manifest.Settings.OptimizeImages.ToString(),
            manifest.Settings.ProtectionLevel.ToString(),
            VisualBriefingHashing.Compute(manifest.Settings.CustomProtectionLevel),
            reusedContentHash,
            VisualBriefingVersions.EVIDENCE_CONTRACT.ToString(),
            VisualBriefingVersions.PLAN_CONTRACT.ToString(),
            VisualBriefingVersions.CONTENT_CONTRACT.ToString(),
            VisualBriefingVersions.DESIGN_CONTRACT.ToString(),
            VisualBriefingVersions.SCHEMA.ToString(),
            VisualBriefingVersions.RUNTIME.ToString());

    /// <summary>
    /// Validates the selected provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    private static void ValidateProvider(ProviderSettings provider)
    {
        if (provider == ProviderSettings.NONE || provider.UsedLLMProvider is LLMProviders.NONE)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.PROVIDER_NOT_SELECTED,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                "Please select an LLM provider.",
                "No provider is selected.");
    }

    /// <summary>
    /// Validates image-input capabilities for content analysis.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="provider">The provider.</param>
    private static void ValidateVisionCapabilities(
        VisualBriefingManifest manifest,
        ProviderSettings provider)
    {
        var imageSources = manifest.Sources.Where(source =>
            source.Kind is VisualBriefingSourceKind.VISUAL_ASSET ||
            FileTypes.IsAllowedPath(source.Path, FileTypes.IMAGE)).ToArray();
        if (imageSources.Length == 0)
            return;
        var capabilities = provider.GetModelCapabilities();
        var acceptsImages = imageSources.Length == 1
            ? capabilities.Contains(Capability.SINGLE_IMAGE_INPUT) ||
              capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT)
            : capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT);
        if (!acceptsImages)
            throw new VisualBriefingBuildException(
                VisualBriefingFailureCode.MODEL_CAPABILITY_MISSING,
                VisualBriefingBuildStage.SOURCE_PREPARATION,
                "The selected model cannot process the number of source images and visual assets.",
                $"ImageCount={imageSources.Length}; SingleImage={capabilities.Contains(Capability.SINGLE_IMAGE_INPUT)}; MultipleImages={capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT)}.");
    }

    /// <summary>
    /// Marks an intentionally reused stage as skipped.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="stage">The stage.</param>
    /// <param name="outputHash">The reused output hash.</param>
    private static void MarkSkipped(
        VisualBriefingBuildRecord build,
        VisualBriefingBuildStage stage,
        string outputHash)
    {
        var record = GetStage(build, stage);
        record.Status = VisualBriefingBuildStageStatus.SKIPPED;
        record.StartedAtUtc ??= DateTimeOffset.UtcNow;
        record.FinishedAtUtc = DateTimeOffset.UtcNow;
        record.InputFingerprint = outputHash;
        record.OutputHash = outputHash;
        record.Failure = null;
        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets or creates one stage record.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="stage">The desired stage.</param>
    /// <returns>The stage record.</returns>
    private static VisualBriefingBuildStageRecord GetStage(
        VisualBriefingBuildRecord build,
        VisualBriefingBuildStage stage)
    {
        var record = build.Stages.FirstOrDefault(candidate => candidate.Stage == stage);
        if (record is not null)
            return record;
        record = new() { Stage = stage };
        build.Stages.Add(record);
        return record;
    }

    /// <summary>
    /// Persists a terminal build failure.
    /// </summary>
    /// <param name="build">The build record.</param>
    /// <param name="status">The terminal status.</param>
    /// <param name="failure">The safe failure.</param>
    /// <param name="token">The cancellation token.</param>
    private async Task SaveTerminalStateAsync(
        VisualBriefingBuildRecord build,
        VisualBriefingBuildStatus status,
        VisualBriefingFailure failure,
        CancellationToken token)
    {
        var stage = GetStage(build, failure.Stage);
        var terminalStageStatus = status is VisualBriefingBuildStatus.CANCELED
            ? VisualBriefingBuildStageStatus.CANCELED
            : VisualBriefingBuildStageStatus.FAILED;
        foreach (var runningStage in build.Stages.Where(item =>
                     item.Status is VisualBriefingBuildStageStatus.RUNNING))
        {
            runningStage.Status = terminalStageStatus;
            runningStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            runningStage.Failure = failure;
        }
        if (stage.Status is not (VisualBriefingBuildStageStatus.COMPLETED or VisualBriefingBuildStageStatus.SKIPPED))
        {
            stage.Status = terminalStageStatus;
            stage.StartedAtUtc ??= DateTimeOffset.UtcNow;
            stage.FinishedAtUtc = DateTimeOffset.UtcNow;
            stage.Failure = failure;
        }
        build.Status = status;
        build.Failure = failure;
        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await store.SaveBuildAsync(build, token);
        progressService.Publish(build);
    }

    /// <summary>
    /// Finishes diagnostics and creates a failed result.
    /// </summary>
    /// <param name="diagnostics">The operation diagnostics.</param>
    /// <param name="build">The optional persisted build.</param>
    /// <param name="failure">The safe failure.</param>
    /// <param name="canContinueAsRebuild">Whether content can continue as a rebuild.</param>
    /// <returns>The failed result.</returns>
    private static VisualBriefingBuildResult FinishFailure(
        VisualBriefingOperationDiagnostics diagnostics,
        VisualBriefingBuildRecord? build,
        VisualBriefingFailure failure,
        bool canContinueAsRebuild)
    {
        diagnostics.BuildId = build?.BuildId ?? diagnostics.BuildId;
        diagnostics.Stage = failure.Stage;
        diagnostics.FailureCode = failure.Code;
        diagnostics.ValidationRule = failure.ValidationRule;
        diagnostics.StructuredResponse = failure.StructuredResponse;
        diagnostics.FinishedAtUtc = DateTimeOffset.UtcNow;
        return new(
            false,
            null,
            failure.UserMessage,
            failure.Code,
            diagnostics,
            canContinueAsRebuild);
    }

    /// <summary>
    /// Creates a logging event from a stable identifier.
    /// </summary>
    /// <param name="eventId">The stable event identifier.</param>
    /// <returns>The logging event.</returns>
    private static EventId Event(VisualBriefingLogEventId eventId) => new((int)eventId, eventId.ToString());

    /// <summary>
    /// Groups validated parent-revision inputs.
    /// </summary>
    /// <param name="ParentVersion">The local version metadata.</param>
    /// <param name="Parts">The parsed standalone artifact.</param>
    /// <param name="Content">The content artifact.</param>
    /// <param name="Presentation">The presentation artifact.</param>
    private sealed record ParentContext(
        VisualBriefingVersion? ParentVersion,
        VisualBriefingArtifactParts? Parts,
        VisualBriefingEvidenceArtifact? Evidence,
        VisualBriefingPlanArtifact? Plan,
        VisualBriefingContentArtifact? Content,
        VisualBriefingPresentationArtifact? Presentation);

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
