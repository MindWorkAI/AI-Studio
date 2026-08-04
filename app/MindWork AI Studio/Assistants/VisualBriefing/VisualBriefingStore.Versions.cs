using System.Text;
using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

public sealed partial class VisualBriefingStore
{
    /// <summary>
    /// Defines <c>AddRevisionAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<VisualBriefingRevisionResult> AddRevisionAsync(
        VisualBriefingRevisionRequest request,
        CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var gate = this.GetLock(request.BriefingId);
        await gate.WaitAsync(token);
        
        try
        {
            var manifest = await this.LoadRequiredWithoutInitializeAsync(request.BriefingId, token);
            RefreshSourceStatuses(manifest);
            if (request.EditMode is not (VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.RECOMPILE) &&
                manifest.Sources.All(source => source.Kind is not VisualBriefingSourceKind.SOURCE_MATERIAL))
            {
                return VisualBriefingRevisionResult.Failure("Please add at least one source material file.");
            }

            var blockingSources = manifest.Sources
                .Where(source => source.Status is VisualBriefingSourceStatus.UNREACHABLE or VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED)
                .ToArray();
            
            if (request.EditMode is not (VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.RECOMPILE) &&
                blockingSources.Length > 0)
                return VisualBriefingRevisionResult.Failure("One or more sources are missing or have an outdated transcript.");

            var parent = request.ParentRevisionId is null
                ? null
                : manifest.Versions.FirstOrDefault(version => version.RevisionId == request.ParentRevisionId);
            
            if (request.EditMode is not VisualBriefingEditMode.INITIAL && parent is null)
                return VisualBriefingRevisionResult.Failure("The selected parent revision no longer exists.");
            
            VisualBriefingArtifactParts? parentParts = null;
            if (parent is not null)
            {
                parentParts = request.EditMode switch
                {
                    VisualBriefingEditMode.RECOMPILE => await this.ReadVersionPartsForRecompileAsync(manifest.BriefingId, parent.RevisionId, token),
                    VisualBriefingEditMode.REBUILD => await this.ReadVersionPartsForRebuildAsync(manifest.BriefingId, parent.RevisionId, token),
                    _ => await this.ReadVersionPartsAsync(manifest.BriefingId, parent.RevisionId, token),
                };
                if (parentParts is null)
                    return VisualBriefingRevisionResult.Failure("The selected parent revision is invalid or damaged.");

                var parentHashes = ComputeSectionHashes(parentParts);
                if (!string.Equals(parent.DataHash, parentHashes.DataHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.AssetHash, parentHashes.AssetHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.TemplateHash, parentHashes.TemplateHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.CssHash, parentHashes.CssHash, StringComparison.Ordinal) ||
                    !string.Equals(parent.RuntimeHash, parentHashes.RuntimeHash, StringComparison.Ordinal))
                    return VisualBriefingRevisionResult.Failure("The selected parent revision does not match its protected section hashes.");
            }

            var preserveRuntime = request.EditMode is VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.UPDATE_CONTENT;
            var html = await artifactService.BuildAsync(
                manifest,
                request,
                preserveRuntime ? parentParts?.RuntimeScript : null,
                preserveRuntime ? parentParts?.EChartsScript : null,
                token);
            
            if (!VisualBriefingArtifactService.TryParse(html, out var parts, out var parseIssue))
                return VisualBriefingRevisionResult.Failure(parseIssue);

            var hashes = ComputeSectionHashes(parts);
            if (parent is not null)
            {
                if (request.EditMode is VisualBriefingEditMode.CHANGE_DESIGN &&
                    (!string.Equals(parent.DataHash, hashes.DataHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.AssetHash, hashes.AssetHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.RuntimeHash, hashes.RuntimeHash, StringComparison.Ordinal)))
                    return VisualBriefingRevisionResult.Failure("A design change attempted to modify facts, embedded assets, or the runtime.");

                if (request.EditMode is VisualBriefingEditMode.UPDATE_CONTENT &&
                    (!string.Equals(parent.TemplateHash, hashes.TemplateHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.CssHash, hashes.CssHash, StringComparison.Ordinal) ||
                     !string.Equals(parent.RuntimeHash, hashes.RuntimeHash, StringComparison.Ordinal)))
                    return VisualBriefingRevisionResult.Failure("A content update attempted to modify the template, CSS, or runtime.");

                if (request.EditMode is VisualBriefingEditMode.RECOMPILE &&
                    (request.EvidenceArtifactId != parent.EvidenceArtifactId ||
                     request.PlanArtifactId != parent.PlanArtifactId ||
                     request.ContentArtifactId != parent.ContentArtifactId ||
                     !string.Equals(parent.AssetHash, hashes.AssetHash, StringComparison.Ordinal)))
                    return VisualBriefingRevisionResult.Failure("A recompile attempted to modify semantic artifacts or embedded assets.");

                if (request.EditMode is not VisualBriefingEditMode.RECOMPILE &&
                    string.Equals(parent.DataHash, hashes.DataHash, StringComparison.Ordinal) &&
                    string.Equals(parent.AssetHash, hashes.AssetHash, StringComparison.Ordinal) &&
                    string.Equals(parent.TemplateHash, hashes.TemplateHash, StringComparison.Ordinal) &&
                    string.Equals(parent.CssHash, hashes.CssHash, StringComparison.Ordinal) &&
                    string.Equals(parent.RuntimeHash, hashes.RuntimeHash, StringComparison.Ordinal))
                    return VisualBriefingRevisionResult.Failure("The model response did not change the briefing.");
            }

            var version = new VisualBriefingVersion
            {
                VersionNumber = this.NextVersionNumber(manifest),
                RevisionId = parts.ExportManifest.RevisionId,
                ParentRevisionId = request.ParentRevisionId,
                CreatedAtUtc = parts.ExportManifest.CreatedAtUtc,
                EditMode = request.EditMode,
                Instruction = request.Instruction,
                DocumentHash = parts.DocumentHash,
                Origin = request.Origin,
                DataHash = hashes.DataHash,
                AssetHash = hashes.AssetHash,
                TemplateHash = hashes.TemplateHash,
                CssHash = hashes.CssHash,
                RuntimeHash = hashes.RuntimeHash,
                ContentArtifactId = request.ContentArtifactId,
                PresentationArtifactId = request.PresentationArtifactId,
                EvidenceArtifactId = request.EvidenceArtifactId,
                PlanArtifactId = request.PlanArtifactId,
                BuildId = request.BuildId,
                OperationId = request.OperationId,
                ModelContributions = request.ModelContributions?.ToList() ?? [],
            };
            
            version.FileName = $"{version.VersionNumber:000000}-{version.RevisionId:D}.html";
            await WriteTextAtomicAsync(
                Path.Combine(this.VersionsDirectory(manifest.BriefingId), version.FileName),
                html,
                token,
                overwrite: false);
            
            manifest.Versions.Add(version);
            if (request.EditMode is not (VisualBriefingEditMode.CHANGE_DESIGN or VisualBriefingEditMode.RECOMPILE))
                foreach (var source in manifest.Sources.Where(source => File.Exists(source.Path)))
                    ApplyFileSnapshot(source, source.Path);
            
            manifest.ModifiedAtUtc = version.CreatedAtUtc;
            await this.StoreManifestAtomicAsync(manifest, token);
            return new(true, version, string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            logger.LogWarning(
                new EventId((int)VisualBriefingLogEventId.STORE_REJECTED, nameof(VisualBriefingLogEventId.STORE_REJECTED)),
                "Could not create a visual briefing revision. BriefingId={BriefingId} BuildId={BuildId} OperationId={OperationId} ExceptionType={ExceptionType}",
                request.BriefingId,
                request.BuildId,
                request.OperationId,
                exception.GetType().Name);
            
            var safeIssue = exception is InvalidDataException
                ? exception.Message
                : "The visual briefing version could not be stored.";
            
            return VisualBriefingRevisionResult.Failure(safeIssue);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>GetVersionPathAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<string?> GetVersionPathAsync(Guid briefingId, Guid revisionId, CancellationToken token = default)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        var version = manifest?.Versions.FirstOrDefault(candidate => candidate.RevisionId == revisionId);
        if (version is null)
            return null;

        var path = this.VersionPath(briefingId, version);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Reads a local immutable version that is compatible with the current semantic schema.
    /// </summary>
    public Task<VisualBriefingArtifactParts?> ReadVersionPartsAsync(Guid briefingId, Guid revisionId, CancellationToken token = default) =>
        this.ReadVersionPartsCoreAsync(briefingId, revisionId, requireCurrentSchema: true, token: token);

    /// <summary>
    /// Reads an intact historical parent for rebuild lineage without requiring its semantic schema
    /// to match the newly generated revision.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="revisionId">The historical parent revision identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The verified parent artifact parts, or <see langword="null"/>.</returns>
    private Task<VisualBriefingArtifactParts?> ReadVersionPartsForRebuildAsync(Guid briefingId, Guid revisionId, CancellationToken token) =>
        this.ReadVersionPartsCoreAsync(briefingId, revisionId, requireCurrentSchema: false, token: token);

    /// <summary>
    /// Reads and integrity-checks one local immutable version with the requested schema policy.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="revisionId">The revision identifier.</param>
    /// <param name="requireCurrentSchema">Whether the current semantic schema is required.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The verified artifact parts, or <see langword="null"/>.</returns>
    private async Task<VisualBriefingArtifactParts?> ReadVersionPartsCoreAsync(Guid briefingId, Guid revisionId, bool requireCurrentSchema, CancellationToken token)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        var version = manifest?.Versions.FirstOrDefault(candidate => candidate.RevisionId == revisionId);
        if (version is null)
            return null;

        var path = this.VersionPath(briefingId, version);
        if (!File.Exists(path))
            return null;

        var html = await File.ReadAllTextAsync(path, token);
        var parsed = requireCurrentSchema
            ? VisualBriefingArtifactService.TryParseForRecompile(html, out var parts, out _)
            : VisualBriefingArtifactService.TryParse(html, out parts, out _);

        if (!parsed || parts.ExportManifest.BriefingId != briefingId || parts.ExportManifest.RevisionId != revisionId || !string.Equals(parts.DocumentHash, version.DocumentHash, StringComparison.OrdinalIgnoreCase))
            return null;

        return parts;
    }

    /// <summary>
    /// Reads a local immutable version for recompilation, accepting an older runtime only when every
    /// protected section still matches the locally persisted version hashes.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="revisionId">The revision identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The verified parent artifact parts, or <see langword="null"/>.</returns>
    internal async Task<VisualBriefingArtifactParts?> ReadVersionPartsForRecompileAsync(
        Guid briefingId,
        Guid revisionId,
        CancellationToken token = default)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        var version = manifest?.Versions.FirstOrDefault(candidate => candidate.RevisionId == revisionId);
        if (version is null)
            return null;

        var path = this.VersionPath(briefingId, version);
        if (!File.Exists(path))
            return null;

        var html = await File.ReadAllTextAsync(path, token);
        if (!VisualBriefingArtifactService.TryParseForRecompile(html, out var parts, out _) ||
            parts.ExportManifest.BriefingId != briefingId ||
            parts.ExportManifest.RevisionId != revisionId ||
            !string.Equals(parts.DocumentHash, version.DocumentHash, StringComparison.OrdinalIgnoreCase))
            return null;

        var hashes = ComputeSectionHashes(parts);
        return string.Equals(version.DataHash, hashes.DataHash, StringComparison.Ordinal) &&
               string.Equals(version.AssetHash, hashes.AssetHash, StringComparison.Ordinal) &&
               string.Equals(version.TemplateHash, hashes.TemplateHash, StringComparison.Ordinal) &&
               string.Equals(version.CssHash, hashes.CssHash, StringComparison.Ordinal) &&
               string.Equals(version.RuntimeHash, hashes.RuntimeHash, StringComparison.Ordinal)
            ? parts
            : null;
    }

    /// <summary>
    /// Opens a validated immutable version for direct streaming.
    /// </summary>
    /// <param name="briefingId">The briefing identifier.</param>
    /// <param name="revisionId">The revision identifier.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The positioned stream and parsed artifact, or <see langword="null"/>.</returns>
    public async Task<(FileStream Stream, VisualBriefingArtifactParts Parts)?> OpenIntegrityCheckedVersionAsync(Guid briefingId, Guid revisionId, CancellationToken token = default)
    {
        var manifest = await this.LoadAsync(briefingId, token);
        var version = manifest?.Versions.FirstOrDefault(candidate => candidate.RevisionId == revisionId);
        if (version is null)
            return null;

        var path = this.VersionPath(briefingId, version);
        if (!File.Exists(path))
            return null;

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, true);
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 65_536, leaveOpen: true);
            var html = await reader.ReadToEndAsync(token);
            if (!VisualBriefingArtifactService.TryParse(html, out var parts, out var issue) ||
                parts.ExportManifest.BriefingId != briefingId ||
                parts.ExportManifest.RevisionId != revisionId ||
                !string.Equals(parts.DocumentHash, version.DocumentHash, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    new EventId((int)VisualBriefingLogEventId.SECURITY_REJECTED, nameof(VisualBriefingLogEventId.SECURITY_REJECTED)),
                    "Visual briefing document integrity check failed. BriefingId={BriefingId} RevisionId={RevisionId} Issue={Issue}",
                    briefingId,
                    revisionId,
                    string.IsNullOrWhiteSpace(issue) ? "The stored header does not match the requested revision or project manifest." : issue);
                await stream.DisposeAsync();
                return null;
            }

            stream.Position = 0;
            return (stream, parts);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Defines <c>ImportAsync</c> for the visual briefing feature.
    /// </summary>
    public async Task<VisualBriefingImportResult> ImportAsync(string sourcePath, bool importNameConflictAsCopy, CancellationToken token = default)
    {
        await this.InitializeAsync(token);
        var html = await File.ReadAllTextAsync(sourcePath, token);
        if (!VisualBriefingArtifactService.TryParse(html, out var parts, out var issue))
            return new(false, Guid.Empty, Guid.Empty, false, false, issue);

        var export = parts.ExportManifest;
        var existing = await this.LoadAsync(export.BriefingId, token);
        if (existing is not null && !NamesEqual(existing.Name, export.Name))
        {
            if (!importNameConflictAsCopy)
                return new(false, existing.BriefingId, export.RevisionId, true, false, "The briefing ID exists locally under a different name.");

            return await this.ImportCopyAsync(html, token);
        }

        if (existing is null)
        {
            existing = await this.CreateAsync(
                export.Name,
                export.Author,
                SettingsFromExport(export),
                export.BriefingId,
                token);
        }

        var gate = this.GetLock(existing.BriefingId);
        await gate.WaitAsync(token);
        try
        {
            existing = await this.LoadRequiredWithoutInitializeAsync(existing.BriefingId, token);
            var knownRevision = existing.Versions.FirstOrDefault(version => version.RevisionId == export.RevisionId);
            if (knownRevision is not null)
            {
                if (string.Equals(knownRevision.DocumentHash, parts.DocumentHash, StringComparison.OrdinalIgnoreCase))
                {
                    var storedVersion = await this.OpenIntegrityCheckedVersionAsync(existing.BriefingId, knownRevision.RevisionId, token);
                    if (storedVersion is null)
                    {
                        await WriteTextAtomicAsync(this.VersionPath(existing.BriefingId, knownRevision), html, token);
                        var restoredHashes = ComputeSectionHashes(parts);
                        knownRevision.DataHash = restoredHashes.DataHash;
                        knownRevision.AssetHash = restoredHashes.AssetHash;
                        knownRevision.TemplateHash = restoredHashes.TemplateHash;
                        knownRevision.CssHash = restoredHashes.CssHash;
                        knownRevision.RuntimeHash = restoredHashes.RuntimeHash;
                        existing.ModifiedAtUtc = DateTimeOffset.UtcNow;
                        await this.StoreManifestAtomicAsync(existing, token);
                    }
                    else
                        await storedVersion.Value.Stream.DisposeAsync();

                    return new(true, existing.BriefingId, export.RevisionId, false, true, string.Empty);
                }

                return new(false, existing.BriefingId, export.RevisionId, false, false, "The revision ID exists with a different document hash.");
            }

            var hashes = ComputeSectionHashes(parts);
            (VisualBriefingContentArtifact Content, VisualBriefingPresentationArtifact Presentation)? importedArtifacts = null;
            
            if (VisualBriefingArtifactService.TryParseForRecompile(html, out var compatibleParts, out _))
                importedArtifacts = await this.MaterializeImportedArtifactsAsync(existing.BriefingId, compatibleParts, projectLockHeld: true, token: token);
            
            var version = new VisualBriefingVersion
            {
                VersionNumber = this.NextVersionNumber(existing),
                SchemaVersion = export.SchemaVersion,
                IntermediateArtifactVersion = importedArtifacts is null ? 0 : VisualBriefingVersions.INTERMEDIATE_ARTIFACT,
                EvidenceContractVersion = importedArtifacts is null ? 0 : VisualBriefingVersions.EVIDENCE_CONTRACT,
                PlanContractVersion = importedArtifacts is null ? 0 : VisualBriefingVersions.PLAN_CONTRACT,
                ContentContractVersion = importedArtifacts is null ? 0 : VisualBriefingVersions.CONTENT_CONTRACT,
                DesignContractVersion = importedArtifacts is null ? 0 : VisualBriefingVersions.DESIGN_CONTRACT,
                RevisionId = export.RevisionId,
                ParentRevisionId = export.ParentRevisionId,
                CreatedAtUtc = export.CreatedAtUtc,
                EditMode = VisualBriefingEditMode.IMPORT,
                DocumentHash = parts.DocumentHash,
                Origin = Path.GetFileName(sourcePath),
                DataHash = hashes.DataHash,
                AssetHash = hashes.AssetHash,
                TemplateHash = hashes.TemplateHash,
                CssHash = hashes.CssHash,
                RuntimeHash = hashes.RuntimeHash,
                ContentArtifactId = importedArtifacts?.Content.ArtifactId,
                PresentationArtifactId = importedArtifacts?.Presentation.ArtifactId,
                ModelContributions = importedArtifacts is { } artifacts ?
                [
                    new(VisualBriefingModelRole.CONTENT, artifacts.Content.Model),
                    new(VisualBriefingModelRole.DESIGN, artifacts.Presentation.Model),
                ] : [],
            };
            
            version.FileName = $"{version.VersionNumber:000000}-{version.RevisionId:D}.html";
            await WriteTextAtomicAsync(
                Path.Combine(this.VersionsDirectory(existing.BriefingId), version.FileName),
                html,
                token,
                overwrite: false);
            
            existing.Versions.Add(version);
            existing.ModifiedAtUtc = DateTimeOffset.UtcNow;
            await this.StoreManifestAtomicAsync(existing, token);
            return new(true, existing.BriefingId, version.RevisionId, false, false, string.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Defines <c>ImportCopyAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task<VisualBriefingImportResult> ImportCopyAsync(string html, CancellationToken token)
    {
        if (!VisualBriefingArtifactService.TryParseForRecompile(html, out var parts, out _))
            return new(false, Guid.Empty, Guid.Empty, false, false, "This historical briefing can be imported under its original identity, but it cannot be rewritten as a copy with the current compiler.");

        var copyId = Guid.NewGuid();
        var manifest = await this.CreateAsync(
            parts.ExportManifest.Name,
            parts.ExportManifest.Author,
            SettingsFromExport(parts.ExportManifest),
            copyId,
            token);
        
        var importedArtifacts = await this.MaterializeImportedArtifactsAsync(
            manifest.BriefingId,
            parts,
            projectLockHeld: false,
            token: token);
        
        var data = RemoveProtectedData(parts.Data);
        var assets = VisualBriefingData.ExtractAssets(parts.Data);
        var result = await this.AddRevisionAsync(new(
            manifest.BriefingId,
            null,
            VisualBriefingEditMode.INITIAL,
            string.Empty,
            data,
            parts.TemplateHtml,
            parts.Css,
            string.Empty,
            "Imported copy",
            importedArtifacts.Content.ArtifactId,
            importedArtifacts.Presentation.ArtifactId,
            ModelContributions:
            [
                new(VisualBriefingModelRole.CONTENT, importedArtifacts.Content.Model),
                new(VisualBriefingModelRole.DESIGN, importedArtifacts.Presentation.Model),
            ],
            EmbeddedAssets: assets,
            AssetPlan: importedArtifacts.Content.AssetPlan), token);
        
        return result is { Success: true, Version: not null }
            ? new(true, manifest.BriefingId, result.Version.RevisionId, false, false, string.Empty)
            : new(false, manifest.BriefingId, Guid.Empty, false, false, result.Issue);
    }

    /// <summary>
    /// Materializes local immutable intermediate artifacts from a validated imported standalone version.
    /// </summary>
    /// <param name="briefingId">The local briefing identifier.</param>
    /// <param name="parts">The validated standalone artifact parts.</param>
    /// <param name="projectLockHeld">Whether the caller already owns the project lock.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The local content and presentation artifacts.</returns>
    private async Task<(VisualBriefingContentArtifact Content, VisualBriefingPresentationArtifact Presentation)> MaterializeImportedArtifactsAsync(
            Guid briefingId,
            VisualBriefingArtifactParts parts,
            bool projectLockHeld,
            CancellationToken token)
    {
        var businessData = VisualBriefingData.RemoveProtectedData(parts.Data);
        var assetPlan = VisualBriefingData.ExtractAssetPlan(parts.Data);
        var structuralSignature = VisualBriefingHashing.StructuralSignature(businessData);
        
        List<VisualBriefingSourceCoverage> coverage = [];
        var importedSlots = new List<VisualBriefingSlotValue>
        {
            new() { SlotId = "imported_data", Value = businessData },
        };
        
        var content = new VisualBriefingContentArtifact
        {
            ArtifactId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Data = businessData,
            Slots = importedSlots,
            ResetLabel = "Reset",
            SourceCoverage = coverage,
            AssetPlan = assetPlan,
            StructuralSignature = structuralSignature,
            Model = "Imported artifact",
        };

        // An imported briefing carries no charts, controls, formulas, accessibility texts, or source
        // references. Hashing the artifact itself keeps those empty sections in the right places
        // without spelling them out as literals here.
        content.PayloadHash = VisualBriefingPayloadHash.ForContent(content.Slots, content.Charts, content.Controls, content.Formulas, content.AccessibilityTexts,
            content.SourceReferences, content.ResetLabel, content.SourceCoverage, content.AssetPlan, content.StructuralSignature);
        
        var importedLayout = new VisualBriefingLayoutNode
        {
            NodeId = "imported",
            Kind = VisualBriefingLayoutNodeKind.STACK,
            Children =
            [
                new()
                {
                    NodeId = "imported_component_node",
                    Kind = VisualBriefingLayoutNodeKind.COMPONENT,
                    ComponentId = "imported_component",
                },
            ],
        };
        
        var templateHash = VisualBriefingHashing.Compute(parts.TemplateHtml);
        var cssHash = VisualBriefingHashing.Compute(parts.Css);
        var presentation = new VisualBriefingPresentationArtifact
        {
            ArtifactId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PayloadHash = VisualBriefingPayloadHash.ForPresentation(importedLayout, VisualBriefingDesignProfile.EDITORIAL, templateHash, cssHash),
            Layout = importedLayout,
            Profile = VisualBriefingDesignProfile.EDITORIAL,
            TemplateHtml = parts.TemplateHtml,
            Css = parts.Css,
            TemplateHash = templateHash,
            CssHash = cssHash,
            Model = "Imported artifact",
        };
        
        if (projectLockHeld)
        {
            await this.WriteContentArtifactWithoutLockAsync(briefingId, content, token);
            await this.WritePresentationArtifactWithoutLockAsync(briefingId, presentation, token);
        }
        else
        {
            await this.WriteContentArtifactAsync(briefingId, content, token);
            await this.WritePresentationArtifactAsync(briefingId, presentation, token);
        }
        
        return (content, presentation);
    }

    /// <summary>
    /// Defines <c>SettingsFromExport</c> for the visual briefing feature.
    /// </summary>
    private static VisualBriefingLocalSettings SettingsFromExport(VisualBriefingExportManifest export) => new()
    {
        TargetLanguage = export.TargetLanguage,
        CustomTargetLanguage = export.CustomTargetLanguage,
        AudienceProfile = export.AudienceProfile,
        AudienceAgeGroup = export.AudienceAgeGroup,
        AudienceOrganizationalLevel = export.AudienceOrganizationalLevel,
        AudienceExpertise = export.AudienceExpertise,
        ShowSourceReferences = export.ShowSourceReferences,
        ProtectionLevel = export.ProtectionLevel,
        CustomProtectionLevel = export.CustomProtectionLevel,
    };

    /// <summary>
    /// Defines <c>RemoveProtectedData</c> for the visual briefing feature.
    /// </summary>
    private static JsonElement RemoveProtectedData(JsonElement data) => VisualBriefingData.RemoveProtectedData(data);

    /// <summary>
    /// Defines <c>ComputeSectionHashes</c> for the visual briefing feature.
    /// </summary>
    private static SectionHashes ComputeSectionHashes(VisualBriefingArtifactParts parts)
    {
        var businessData = VisualBriefingHashing.CanonicalJson(VisualBriefingData.RemoveProtectedData(parts.Data));
        var assets = JsonSerializer.Serialize(
            VisualBriefingData.ExtractAssets(parts.Data),
            VisualBriefingJson.Canonical);
        
        return new(
            VisualBriefingHashing.Compute(businessData),
            VisualBriefingHashing.Compute(assets),
            VisualBriefingHashing.Compute(parts.TemplateHtml),
            VisualBriefingHashing.Compute(parts.Css),
            VisualBriefingHashing.Compute(parts.RuntimeScript + (parts.EChartsScript ?? string.Empty)));
    }

    /// <summary>
    /// Defines <c>SectionHashes</c> for the visual briefing feature.
    /// </summary>
    private sealed record SectionHashes(string DataHash, string AssetHash, string TemplateHash, string CssHash, string RuntimeHash);

    /// <summary>
    /// Defines <c>ParseVersionNumber</c> for the visual briefing feature.
    /// </summary>
    private static int ParseVersionNumber(string fileName) => fileName.Length >= 6 && int.TryParse(fileName.AsSpan(0, 6), out var value) ? value : 0;

    /// <summary>
    /// Defines <c>NextVersionNumber</c> for the visual briefing feature.
    /// </summary>
    private int NextVersionNumber(VisualBriefingManifest manifest)
    {
        var manifestMaximum = manifest.Versions.Select(version => version.VersionNumber).DefaultIfEmpty().Max();
        var diskMaximum = Directory.EnumerateFiles(this.VersionsDirectory(manifest.BriefingId), "*.html")
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null)
            .Select(fileName => ParseVersionNumber(fileName!))
            .DefaultIfEmpty()
            .Max();
        
        return Math.Max(manifestMaximum, diskMaximum) + 1;
    }
}