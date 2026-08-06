using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

internal sealed partial class VisualBriefingBuildOrchestrator
{
    /// <summary>
    /// Recompiles one immutable revision with the current deterministic export pipeline without
    /// accessing sources or calling a model.
    /// </summary>
    /// <param name="manifest">The current local briefing manifest.</param>
    /// <param name="parentRevisionId">The revision whose semantic artifacts are reused.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The terminal recompile result.</returns>
    public async Task<VisualBriefingBuildResult> RecompileAsync(VisualBriefingManifest manifest, Guid parentRevisionId, CancellationToken token = default)
    {
        var operationId = Guid.NewGuid();
        var proposedBuildId = Guid.NewGuid();
        var diagnostics = new VisualBriefingOperationDiagnostics
        {
            OperationId = operationId,
            BuildId = proposedBuildId,
            Stage = VisualBriefingBuildStage.COMPILATION,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        
        this.liveDiagnostics[manifest.BriefingId] = diagnostics;
        var gate = this.buildLocks.GetOrAdd(manifest.BriefingId, _ => new(1, 1));
        await gate.WaitAsync(token);
        VisualBriefingBuildRecord? build = null;

        try
        {
            var parent = await this.LoadParentContextAsync(manifest, VisualBriefingEditMode.RECOMPILE, parentRevisionId, token);
            if (parent is not
                {
                    ParentVersion: { } parentVersion,
                    Parts: { } parentParts,
                    Evidence: { } evidence,
                    Plan: { } plan,
                    Content: { } content,
                    Presentation: { } previousPresentation,
                })
                throw new VisualBriefingBuildException(
                    VisualBriefingFailureCode.ARTIFACT_VALIDATION_FAILED,
                    VisualBriefingBuildStage.COMPILATION,
                    "This briefing version cannot be recompiled with the current AI Studio version. Rebuild the briefing instead.",
                    "The selected revision does not contain a complete compatible set of semantic artifacts.");

            var inputFingerprint = VisualBriefingHashing.ComputeSections(
                parentRevisionId.ToString("D"),
                evidence.PayloadHash,
                plan.PayloadHash,
                content.PayloadHash,
                previousPresentation.PayloadHash,
                parentVersion.AssetHash,
                VisualBriefingVersions.COMPILER.ToString(),
                VisualBriefingVersions.SCHEMA.ToString(),
                VisualBriefingVersions.RUNTIME.ToString());
            
            var now = DateTimeOffset.UtcNow;
            var candidate = new VisualBriefingBuildRecord
            {
                BuildId = proposedBuildId,
                OperationId = operationId,
                BriefingId = manifest.BriefingId,
                Mode = VisualBriefingEditMode.RECOMPILE,
                ParentRevisionId = parentRevisionId,
                InputFingerprint = inputFingerprint,
                SourceFingerprint = parentVersion.AssetHash,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                EvidenceArtifactId = evidence.ArtifactId,
                PlanArtifactId = plan.ArtifactId,
                ContentArtifactId = content.ArtifactId,
                Stages =
                [
                    .. Enum.GetValues<VisualBriefingBuildStage>().Select(stage => new VisualBriefingBuildStageRecord { Stage = stage })
                ],
            };
            
            var selectedBuild = await this.store.StartOrResumeBuildAsync(candidate, token);
            build = selectedBuild.Build;
            build.OperationId = operationId;
            diagnostics.BuildId = build.BuildId;

            MarkSkipped(build, VisualBriefingBuildStage.SOURCE_PREPARATION, parentVersion.AssetHash);
            MarkSkipped(build, VisualBriefingBuildStage.EVIDENCE, evidence.PayloadHash);
            MarkSkipped(build, VisualBriefingBuildStage.PLAN, plan.PayloadHash);
            MarkSkipped(build, VisualBriefingBuildStage.CONTENT, content.PayloadHash);
            MarkSkipped(build, VisualBriefingBuildStage.DESIGN, previousPresentation.PayloadHash);
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);

            diagnostics.ContentHashes["evidence"] = evidence.PayloadHash;
            diagnostics.ContentHashes["plan"] = plan.PayloadHash;
            diagnostics.ContentHashes["content"] = content.PayloadHash;
            diagnostics.ArtifactIds["evidence"] = evidence.ArtifactId;
            diagnostics.ArtifactIds["plan"] = plan.ArtifactId;
            diagnostics.ArtifactIds["content"] = content.ArtifactId;

            diagnostics.Stage = VisualBriefingBuildStage.COMPILATION;
            var compilationStage = GetStage(build, VisualBriefingBuildStage.COMPILATION);
            compilationStage.Status = VisualBriefingBuildStageStatus.RUNNING;
            compilationStage.StartedAtUtc = DateTimeOffset.UtcNow;
            compilationStage.FinishedAtUtc = null;
            compilationStage.Failure = null;
            compilationStage.InputFingerprint = inputFingerprint;
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);

            var compiled = VisualBriefingCompilerInvariant.Guard(
                VisualBriefingBuildStage.COMPILATION,
                () => VisualBriefingLayoutCompiler.Compile(
                    plan,
                    content,
                    previousPresentation.Layout,
                    previousPresentation.Profile));
            
            var validationDataProperties = compiled.Data.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
            
            validationDataProperties["_mwai"] = JsonSerializer.SerializeToElement(new
            {
                schemaVersion = VisualBriefingVersions.SCHEMA,
                runtimeVersion = VisualBriefingVersions.RUNTIME,
                aiStudioVersion = "validation",
                assets = content.AssetPlan.ToDictionary(asset => asset.AssetId, _ => "data:image/png;base64,AA==", StringComparer.Ordinal),
                footer = new
                {
                    createdWith = "validation",
                    models = "validation",
                    createdAt = "validation",
                    authors = "validation",
                    protection = "validation",
                },
            }, VisualBriefingJson.Canonical);
            
            VisualBriefingCompilerInvariant.Guard(
                VisualBriefingBuildStage.COMPILATION,
                VisualBriefingArtifactService.ValidateGeneratedParts(manifest,
                    JsonSerializer.SerializeToElement(validationDataProperties, VisualBriefingJson.Canonical),
                    compiled.TemplateHtml, compiled.Css,
                    content.Charts.Count > 0));

            var contributions = await this.ResolveRecompileModelContributionsAsync(manifest.BriefingId, parentVersion, evidence, plan, content, previousPresentation, token);
            var presentationModel = contributions.First(contribution => contribution.Role is VisualBriefingModelRole.DESIGN).Model;
            var presentation = new VisualBriefingPresentationArtifact
            {
                ArtifactId = Guid.NewGuid(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                PayloadHash = VisualBriefingPayloadHash.ForPresentation(previousPresentation.Layout, previousPresentation.Profile, compiled.TemplateHash, compiled.CssHash),
                Layout = previousPresentation.Layout,
                Profile = previousPresentation.Profile,
                TemplateHtml = compiled.TemplateHtml,
                Css = compiled.Css,
                TemplateHash = compiled.TemplateHash,
                CssHash = compiled.CssHash,
                Model = presentationModel,
            };
            
            await this.store.WritePresentationArtifactAsync(manifest.BriefingId, presentation, token);
            build.PresentationArtifactId = presentation.ArtifactId;
            diagnostics.ContentHashes["design"] = presentation.PayloadHash;
            diagnostics.ArtifactIds["design"] = presentation.ArtifactId;
            
            compilationStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            compilationStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            compilationStage.OutputHash = VisualBriefingHashing.ComputeSections(
                VisualBriefingHashing.Compute(VisualBriefingHashing.CanonicalJson(compiled.Data)),
                compiled.TemplateHash,
                compiled.CssHash);
            
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);

            diagnostics.Stage = VisualBriefingBuildStage.ASSEMBLY;
            var revisionId = build.RevisionId ?? Guid.NewGuid();
            var revisionCreatedAt = DateTimeOffset.UtcNow;
            
            build.RevisionId = revisionId;
            
            var assemblyStage = GetStage(build, VisualBriefingBuildStage.ASSEMBLY);
            assemblyStage.Status = VisualBriefingBuildStageStatus.RUNNING;
            assemblyStage.StartedAtUtc = revisionCreatedAt;
            assemblyStage.FinishedAtUtc = null;
            assemblyStage.Failure = null;
            
            assemblyStage.InputFingerprint = VisualBriefingHashing.ComputeSections(
                content.PayloadHash,
                presentation.PayloadHash,
                parentVersion.AssetHash,
                VisualBriefingVersions.ARTIFACT.ToString(),
                VisualBriefingVersions.COMPILER.ToString(),
                VisualBriefingVersions.SCHEMA.ToString(),
                VisualBriefingVersions.RUNTIME.ToString());
            
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await this.store.SaveBuildAsync(build, token);
            this.progressService.Publish(build);

            var revision = await this.store.AddRevisionAsync(new(
                manifest.BriefingId,
                parentRevisionId,
                VisualBriefingEditMode.RECOMPILE,
                string.Empty,
                compiled.Data,
                compiled.TemplateHtml,
                compiled.Css,
                string.Empty,
                "MindWork AI Studio",
                content.ArtifactId,
                presentation.ArtifactId,
                build.BuildId,
                build.OperationId,
                contributions,
                revisionId,
                revisionCreatedAt,
                VisualBriefingData.ExtractAssets(parentParts.Data),
                content.AssetPlan,
                evidence.ArtifactId,
                plan.ArtifactId,
                parentParts.ExportManifest), token);
            
            var commitStage = GetStage(build, VisualBriefingBuildStage.COMMIT);
            if (!revision.Success || revision.Version is null)
                throw new VisualBriefingBuildException(VisualBriefingFailureCode.STORE_FAILED, VisualBriefingBuildStage.COMMIT, revision.Issue, $"The immutable recompiled revision commit was rejected. StoreIssue={revision.Issue}");

            assemblyStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            assemblyStage.FinishedAtUtc = DateTimeOffset.UtcNow;
            assemblyStage.OutputHash = revision.Version.DocumentHash;
            
            commitStage.Status = VisualBriefingBuildStageStatus.COMPLETED;
            commitStage.StartedAtUtc = assemblyStage.FinishedAtUtc;
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
                UserMessage = "The visual briefing recompilation was canceled.",
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
                ValidationRule = exception.Stage is VisualBriefingBuildStage.COMPILATION
                    ? VisualBriefingValidationRule.COMPILER_OUTPUT_INVALID
                    : VisualBriefingValidationRule.NONE,
                UserMessage = exception.Message,
                TechnicalDetails = exception.TechnicalDetails,
            };
            
            if (build is not null)
                await this.SaveTerminalStateAsync(build, VisualBriefingBuildStatus.FAILED, failure, CancellationToken.None);
            
            return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: false);
        }
        catch (Exception exception)
        {
            var failure = new VisualBriefingFailure
            {
                Code = VisualBriefingFailureCode.UNEXPECTED,
                Stage = diagnostics.Stage,
                UserMessage = "The visual briefing could not be recompiled because of an unexpected internal error.",
                TechnicalDetails = $"{exception.GetType().Name} at stage {diagnostics.Stage}.",
            };
            
            if (build is not null)
                await this.SaveTerminalStateAsync(build, VisualBriefingBuildStatus.FAILED, failure, CancellationToken.None);
            
            return FinishFailure(diagnostics, build, failure, canContinueAsRebuild: false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reconstructs the most specific model attribution available for each reused semantic artifact.
    /// </summary>
    private async Task<List<VisualBriefingModelContribution>> ResolveRecompileModelContributionsAsync(Guid briefingId, VisualBriefingVersion parentVersion,
        VisualBriefingEvidenceArtifact evidence, VisualBriefingPlanArtifact plan, VisualBriefingContentArtifact content, VisualBriefingPresentationArtifact presentation,
        CancellationToken token)
    {
        var builds = await this.store.ListBuildsAsync(briefingId, token);
        
        return
        [
            new(
                VisualBriefingModelRole.EVIDENCE,
                ResolveRecompileModelLabel(
                    builds,
                    build => build.EvidenceArtifactId,
                    evidence.ArtifactId,
                    VisualBriefingBuildStage.EVIDENCE,
                    ExistingModelLabel(parentVersion, VisualBriefingModelRole.EVIDENCE, evidence.Model))),
            
            new(
                VisualBriefingModelRole.PLAN,
                ResolveRecompileModelLabel(
                    builds,
                    build => build.PlanArtifactId,
                    plan.ArtifactId,
                    VisualBriefingBuildStage.PLAN,
                    ExistingModelLabel(parentVersion, VisualBriefingModelRole.PLAN, plan.Model))),
            
            new(
                VisualBriefingModelRole.CONTENT,
                ResolveRecompileModelLabel(
                    builds,
                    build => build.ContentArtifactId,
                    content.ArtifactId,
                    VisualBriefingBuildStage.CONTENT,
                    ExistingModelLabel(parentVersion, VisualBriefingModelRole.CONTENT, content.Model))),
            
            new(
                VisualBriefingModelRole.DESIGN,
                ResolveRecompileModelLabel(
                    builds,
                    build => build.PresentationArtifactId,
                    presentation.ArtifactId,
                    VisualBriefingBuildStage.DESIGN,
                    ExistingModelLabel(parentVersion, VisualBriefingModelRole.DESIGN, presentation.Model))),
        ];
    }

    /// <summary>
    /// Resolves the provider and model that originally produced one immutable artifact.
    /// </summary>
    private static string ResolveRecompileModelLabel(IReadOnlyList<VisualBriefingBuildRecord> builds, Func<VisualBriefingBuildRecord, Guid?> artifactId,
        Guid expectedArtifactId, VisualBriefingBuildStage stage, string fallback)
    {
        var producingBuild = builds.FirstOrDefault(build =>
            artifactId(build) == expectedArtifactId &&
            !string.IsNullOrWhiteSpace(build.ProviderFamily) &&
            !string.IsNullOrWhiteSpace(build.Model) &&
            build.Stages.Any(candidate => candidate.Stage == stage && candidate.Status is VisualBriefingBuildStageStatus.COMPLETED));

        return producingBuild is null ? fallback : VisualBriefingModelNames.ExportLabel(producingBuild.ProviderFamily, producingBuild.Model);
    }

    /// <summary>
    /// Returns the persisted role attribution, falling back to the immutable artifact label.
    /// </summary>
    private static string ExistingModelLabel(VisualBriefingVersion parentVersion, VisualBriefingModelRole role, string artifactModel)
    {
        var contribution = parentVersion.ModelContributions.FirstOrDefault(candidate => candidate.Role == role && !string.IsNullOrWhiteSpace(candidate.Model));
        return contribution?.Model ?? artifactModel;
    }
}