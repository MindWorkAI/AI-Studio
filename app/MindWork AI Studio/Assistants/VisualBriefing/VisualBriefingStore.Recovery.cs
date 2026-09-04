using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingStore
{
    /// <summary>
    /// Defines <c>ReconcileAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ReconcileAsync(Guid briefingId, CancellationToken token)
    {
        var gate = this.GetLock(briefingId);
        await gate.WaitAsync(token);
        
        try
        {
            var manifest = await this.LoadWithoutInitializeAsync(briefingId, token);
            if (manifest is null)
                return;
            
            Directory.CreateDirectory(this.VersionsDirectory(briefingId));
            Directory.CreateDirectory(this.TranscriptsDirectory(briefingId));
            Directory.CreateDirectory(this.EvidenceArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.PlanArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.ContentArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.PresentationArtifactsDirectory(briefingId));
            Directory.CreateDirectory(this.BuildsDirectory(briefingId));
            
            var builds = await this.LoadBuildsWithoutLockAsync(briefingId, token);
            foreach (var committedBuild in builds.Where(build =>
                         build.Status is VisualBriefingBuildStatus.ACTIVE &&
                         build.RevisionId is not null &&
                         manifest.Versions.Any(version =>
                             version.RevisionId == build.RevisionId &&
                             version.BuildId == build.BuildId)))
            {
                var committedVersion = manifest.Versions.Single(version =>
                    version.RevisionId == committedBuild.RevisionId &&
                    version.BuildId == committedBuild.BuildId);
                
                foreach (var stageName in new[]
                         {
                             VisualBriefingBuildStage.ASSEMBLY,
                             VisualBriefingBuildStage.COMMIT,
                         })
                {
                    var stage = committedBuild.Stages.FirstOrDefault(item => item.Stage == stageName);
                    if (stage is null)
                        continue;
                    
                    stage.Status = VisualBriefingBuildStageStatus.COMPLETED;
                    stage.FinishedAtUtc ??= committedVersion.CreatedAtUtc;
                    stage.OutputHash = committedVersion.DocumentHash;
                    stage.Failure = null;
                }
                
                committedBuild.CommittedRevisionId = committedVersion.RevisionId;
                committedBuild.Status = VisualBriefingBuildStatus.COMPLETED;
                committedBuild.Failure = null;
                committedBuild.UpdatedAtUtc = DateTimeOffset.UtcNow;
                
                await this.StoreBuildAtomicAsync(committedBuild, overwrite: true, token);
            }
            
            foreach (var interruptedBuild in builds.Where(build => build.Status is VisualBriefingBuildStatus.ACTIVE))
            {
                var interruptedStages = interruptedBuild.Stages.Where(stage =>
                    stage.Status is VisualBriefingBuildStageStatus.RUNNING).ToArray();
                
                if (interruptedStages.Length == 0)
                {
                    var nextStage = interruptedBuild.Stages
                        .OrderBy(stage => stage.Stage)
                        .FirstOrDefault(stage => stage.Status is VisualBriefingBuildStageStatus.NOT_STARTED);
                    
                    if (nextStage is not null)
                        interruptedStages = [nextStage];
                }
                
                VisualBriefingFailure? interruptedFailure = null;
                foreach (var interruptedStage in interruptedStages)
                {
                    interruptedStage.Status = VisualBriefingBuildStageStatus.FAILED;
                    interruptedStage.FinishedAtUtc = DateTimeOffset.UtcNow;
                    interruptedFailure = new()
                    {
                        Code = VisualBriefingFailureCode.BUILD_INTERRUPTED,
                        Stage = interruptedStage.Stage,
                        UserMessage = "The interrupted visual briefing build can be resumed.",
                        TechnicalDetails = "The app stopped before this stage completed.",
                    };
                    
                    interruptedStage.Failure = interruptedFailure;
                }
                
                if (interruptedFailure is null)
                    continue;
                
                interruptedBuild.Status = VisualBriefingBuildStatus.FAILED;
                interruptedBuild.Failure = interruptedFailure;
                interruptedBuild.UpdatedAtUtc = DateTimeOffset.UtcNow;
                await this.StoreBuildAtomicAsync(interruptedBuild, overwrite: true, token);
            }

            var changed = manifest.Versions.RemoveAll(version =>
                !File.Exists(this.VersionPath(briefingId, version))) > 0;
            
            var knownFiles = manifest.Versions.Select(version => version.FileName).ToHashSet(StringComparer.Ordinal);
            foreach (var versionPath in Directory.EnumerateFiles(this.VersionsDirectory(briefingId), "*.html"))
            {
                token.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(versionPath);
                if (knownFiles.Contains(fileName))
                    continue;

                var html = await File.ReadAllTextAsync(versionPath, token);
                if (!VisualBriefingArtifactService.TryParse(html, out var parts, out _))
                    continue;

                var hashes = ComputeSectionHashes(parts);
                var versionNumber = ParseVersionNumber(fileName);
                if (versionNumber <= 0 ||
                    !string.Equals(fileName, $"{versionNumber:000000}-{parts.ExportManifest.RevisionId:D}.html", StringComparison.Ordinal) ||
                    manifest.Versions.Any(version => version.RevisionId == parts.ExportManifest.RevisionId ||
                                                     version.VersionNumber == versionNumber))
                    continue;

                var matchingBuild = builds.FirstOrDefault(build => build.RevisionId == parts.ExportManifest.RevisionId);
                var semanticallyCompatible = VisualBriefingArtifactService.TryParseForRecompile(html, out _, out _);
                manifest.Versions.Add(new()
                {
                    VersionNumber = versionNumber,
                    SchemaVersion = parts.ExportManifest.SchemaVersion,
                    IntermediateArtifactVersion = semanticallyCompatible && matchingBuild is not null
                        ? VisualBriefingVersions.INTERMEDIATE_ARTIFACT
                        : 0,
                    EvidenceContractVersion = semanticallyCompatible ? matchingBuild?.EvidenceContractVersion ?? 0 : 0,
                    PlanContractVersion = semanticallyCompatible ? matchingBuild?.PlanContractVersion ?? 0 : 0,
                    ContentContractVersion = semanticallyCompatible ? matchingBuild?.ContentContractVersion ?? 0 : 0,
                    DesignContractVersion = semanticallyCompatible ? matchingBuild?.DesignContractVersion ?? 0 : 0,
                    RevisionId = parts.ExportManifest.RevisionId,
                    ParentRevisionId = parts.ExportManifest.ParentRevisionId,
                    CreatedAtUtc = parts.ExportManifest.CreatedAtUtc,
                    EditMode = matchingBuild?.Mode ?? VisualBriefingEditMode.IMPORT,
                    Instruction = matchingBuild?.Instruction ?? string.Empty,
                    DocumentHash = parts.DocumentHash,
                    Origin = "Recovered from disk",
                    FileName = fileName,
                    DataHash = hashes.DataHash,
                    AssetHash = hashes.AssetHash,
                    TemplateHash = hashes.TemplateHash,
                    CssHash = hashes.CssHash,
                    RuntimeHash = hashes.RuntimeHash,
                    EvidenceArtifactId = semanticallyCompatible ? matchingBuild?.EvidenceArtifactId : null,
                    PlanArtifactId = semanticallyCompatible ? matchingBuild?.PlanArtifactId : null,
                    ContentArtifactId = semanticallyCompatible ? matchingBuild?.ContentArtifactId : null,
                    PresentationArtifactId = semanticallyCompatible ? matchingBuild?.PresentationArtifactId : null,
                    BuildId = matchingBuild?.BuildId,
                    OperationId = matchingBuild?.OperationId,
                    ModelContributions = BuildRecoveredContributions(matchingBuild),
                });
                
                if (matchingBuild is not null)
                {
                    matchingBuild.CommittedRevisionId = parts.ExportManifest.RevisionId;
                    matchingBuild.Status = VisualBriefingBuildStatus.COMPLETED;
                    matchingBuild.Failure = null;
                    matchingBuild.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    await this.StoreBuildAtomicAsync(matchingBuild, overwrite: true, token);
                }
                
                changed = true;
            }

            if (changed)
            {
                manifest.Versions = manifest.Versions.OrderBy(version => version.VersionNumber).ToList();
                manifest.ModifiedAtUtc = DateTimeOffset.UtcNow;
                await this.StoreManifestAtomicAsync(manifest, token);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            logger.LogError(
                new EventId((int)VisualBriefingLogEventId.STORE_RECOVERY, VisualBriefingLogEventId.STORE_RECOVERY.ToString()),
                exception,
                "Could not reconcile visual briefing. BriefingId={BriefingId} ExceptionType={ExceptionType}",
                briefingId,
                exception.GetType().Name);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Reconstructs footer model roles for an orphaned committed version.
    /// </summary>
    /// <param name="build">The matching build record.</param>
    /// <returns>The recovered contributions.</returns>
    private static List<VisualBriefingModelContribution> BuildRecoveredContributions(VisualBriefingBuildRecord? build)
    {
        if (build is null || string.IsNullOrWhiteSpace(build.Model))
            return [];

        var model = VisualBriefingModelNames.ExportLabel(build.ProviderFamily, build.Model);
        List<VisualBriefingModelContribution> contributions = [];
        if (build.EvidenceArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.EVIDENCE, model));
        
        if (build.PlanArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.PLAN, model));
        
        if (build.ContentArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.CONTENT, model));
        
        if (build.PresentationArtifactId is not null)
            contributions.Add(new(VisualBriefingModelRole.DESIGN, model));
        
        return contributions;
    }
}