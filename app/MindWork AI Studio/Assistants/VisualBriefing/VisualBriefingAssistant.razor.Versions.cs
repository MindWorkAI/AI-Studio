using AIStudio.Dialogs;
using AIStudio.Tools.Rust;

using DialogOptions = AIStudio.Dialogs.DialogOptions;

namespace AIStudio.Assistants.VisualBriefing;

public partial class VisualBriefingAssistant
{
    /// <summary>
    /// Gets whether the selected revision references all four intermediate artifacts.
    /// </summary>
    private bool SelectedVersionSupportsEdits => this.VersionSupportsSemanticEdits(this.selectedRevisionId);

    /// <summary>
    /// Gets whether one revision references the complete semantic artifact set.
    /// </summary>
    /// <param name="revisionId">The revision to inspect.</param>
    /// <returns>Whether the revision can be edited or recompiled without rebuilding its inputs.</returns>
    private bool VersionSupportsSemanticEdits(Guid revisionId) =>
        this.selectedBriefing?.Versions.FirstOrDefault(version =>
            version.RevisionId == revisionId) is
        {
            SchemaVersion: VisualBriefingVersions.SCHEMA,
            IntermediateArtifactVersion: VisualBriefingVersions.INTERMEDIATE_ARTIFACT,
            EvidenceContractVersion: VisualBriefingVersions.EVIDENCE_CONTRACT,
            PlanContractVersion: VisualBriefingVersions.PLAN_CONTRACT,
            ContentContractVersion: VisualBriefingVersions.CONTENT_CONTRACT,
            DesignContractVersion: VisualBriefingVersions.DESIGN_CONTRACT,
            EvidenceArtifactId: not null,
            PlanArtifactId: not null,
            ContentArtifactId: not null,
            PresentationArtifactId: not null,
        };

    /// <summary>
    /// Defines <c>CanGoBackward</c> for the visual briefing feature.
    /// </summary>
    private bool CanGoBackward => this.GetSelectedVersionIndex() > 0;

    /// <summary>
    /// Gets whether a newer immutable revision can be selected.
    /// </summary>
    private bool CanGoForward
    {
        get
        {
            var index = this.GetSelectedVersionIndex();
            return index >= 0 && index < (this.selectedBriefing?.Versions.Count ?? 0) - 1;
        }
    }

    /// <summary>
    /// Defines <c>PreviewContainerClass</c> for the visual briefing feature.
    /// </summary>
    private string PreviewContainerClass => $"visual-briefing-preview visual-briefing-preview-{this.previewDevice.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Defines <c>SelectRevisionAsync</c> for the visual briefing feature.
    /// </summary>
    private Task SelectRevisionAsync(Guid revisionId)
    {
        if (this.selectedBriefing is null ||
            this.selectedBriefing.Versions.All(version => version.RevisionId != revisionId))
            return Task.CompletedTask;

        this.selectedRevisionId = revisionId;
        var token = this.PreviewTokenService.Issue(this.selectedBriefing.BriefingId, revisionId);
        this.previewUrl = $"/visual-briefing/preview/{this.selectedBriefing.BriefingId:D}/{revisionId:D}?token={Uri.EscapeDataString(token)}";

        return Task.CompletedTask;
    }

    /// <summary>
    /// Defines <c>PreviousVersionAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task PreviousVersionAsync()
    {
        var versions = this.OrderedVersions();
        var index = this.GetSelectedVersionIndex();
        if (index > 0)
            await this.SelectRevisionAsync(versions[index - 1].RevisionId);
    }

    /// <summary>
    /// Defines <c>NextVersionAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task NextVersionAsync()
    {
        var versions = this.OrderedVersions();
        var index = this.GetSelectedVersionIndex();
        if (index >= 0 && index < versions.Count - 1)
            await this.SelectRevisionAsync(versions[index + 1].RevisionId);
    }

    /// <summary>
    /// Defines <c>ExportAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ExportAsync()
    {
        if (this.selectedBriefing is null || this.selectedRevisionId == Guid.Empty)
            return;

        var sourcePath = await this.Store.GetVersionPathAsync(this.selectedBriefing.BriefingId, this.selectedRevisionId);
        if (sourcePath is null)
            return;

        if (!await this.ConfirmLargeFileAsync(sourcePath, T("export")))
            return;

        var response = await this.RustService.SaveFile(
            T("Export visual briefing"),
            [FileTypes.VISUAL_BRIEFING_HTML],
            $"{SafeFileName(this.editor.Name)}.html");

        if (response.UserCancelled)
            return;

        if (PathComparer().Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(response.SaveFilePath)))
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.SaveAs, T("Choose a different export location so the immutable briefing version is not overwritten.")));
            return;
        }

        var verified = await this.Store.OpenIntegrityCheckedVersionAsync(this.selectedBriefing.BriefingId, this.selectedRevisionId);
        if (verified is null)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.GppBad, T("The selected briefing version failed its integrity check and cannot be exported.")));
            return;
        }

        await using var source = verified.Value.Stream;
        await using var destination = new FileStream(response.SaveFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 65_536, true);
        await source.CopyToAsync(destination);

        var exportedVersion = this.selectedBriefing.Versions.First(version =>
            version.RevisionId == this.selectedRevisionId);

        this.Logger.LogInformation(
            new EventId((int)VisualBriefingLogEventId.EXPORT, VisualBriefingLogEventId.EXPORT.ToString()),
            "Visual briefing version exported. OperationId={OperationId} BuildId={BuildId} BriefingId={BriefingId} RevisionId={RevisionId} DocumentHash={DocumentHash} Bytes={Bytes}",
            exportedVersion.OperationId,
            exportedVersion.BuildId,
            this.selectedBriefing.BriefingId,
            exportedVersion.RevisionId,
            exportedVersion.DocumentHash,
            source.Length);

        await this.MessageBus.SendSuccess(new(Icons.Material.Filled.FileDownload, T("The visual briefing was exported.")));
    }

    /// <summary>
    /// Defines <c>ImportAsync</c> for the visual briefing feature.
    /// </summary>
    private async Task ImportAsync()
    {
        var response = await this.RustService.SelectFile(T("Import visual briefing"), [FileTypes.VISUAL_BRIEFING_HTML]);
        if (response.UserCancelled || !await this.ConfirmLargeFileAsync(response.SelectedFilePath, T("import")))
            return;

        var imported = await this.Store.ImportAsync(response.SelectedFilePath, importNameConflictAsCopy: false);
        if (imported.RequiresCopyConfirmation)
        {
            var parameters = new DialogParameters<ConfirmDialog>
            {
                { dialog => dialog.Message, T("This briefing ID already exists under another name. Import it as a copy with a new ID?") },
            };

            var reference = await this.DialogService.ShowAsync<ConfirmDialog>(T("Import as copy"), parameters, DialogOptions.FULLSCREEN);
            var result = await reference.Result;
            if (result is null || result.Canceled)
                return;

            imported = await this.Store.ImportAsync(response.SelectedFilePath, importNameConflictAsCopy: true);
        }

        if (!imported.Success)
        {
            await this.MessageBus.SendError(new(Icons.Material.Filled.FileUpload, imported.Issue));
            return;
        }

        await this.ReloadListAsync(imported.BriefingId);
        await this.SelectRevisionAsync(imported.RevisionId);

        this.Logger.LogInformation(
            new EventId((int)VisualBriefingLogEventId.IMPORT, VisualBriefingLogEventId.IMPORT.ToString()),
            "Visual briefing version imported. BriefingId={BriefingId} RevisionId={RevisionId} Deduplicated={Deduplicated}",
            imported.BriefingId,
            imported.RevisionId,
            imported.WasDeduplicated);

        await this.MessageBus.SendSuccess(new(Icons.Material.Filled.FileUpload, imported.WasDeduplicated ? T("This briefing revision was already imported.") : T("The visual briefing was imported.")));
    }

    /// <summary>
    /// Defines <c>OrderedVersions</c> for the visual briefing feature.
    /// </summary>
    private IReadOnlyList<VisualBriefingVersion> OrderedVersions() =>
        this.selectedBriefing?.Versions.OrderBy(version => version.VersionNumber).ToArray() ?? [];

    /// <summary>
    /// Defines <c>GetSelectedVersionIndex</c> for the visual briefing feature.
    /// </summary>
    private int GetSelectedVersionIndex()
    {
        var versions = this.OrderedVersions();
        for (var index = 0; index < versions.Count; index++)
            if (versions[index].RevisionId == this.selectedRevisionId)
                return index;

        return -1;
    }

    /// <summary>
    /// Defines <c>SafeFileName</c> for the visual briefing feature.
    /// </summary>
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var name = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(name) ? "visual-briefing" : name;
    }
}