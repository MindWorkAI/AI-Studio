using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MandatoryInfoDisplay
{
    private enum MandatoryInfoAcceptanceStatus
    {
        MISSING,
        VERSION_CHANGED,
        CONTENT_CHANGED,
        ACCEPTED,
    }

    [Parameter]
    public DataMandatoryInfo Info { get; set; } = new();

    [Parameter]
    public DataMandatoryInfoAcceptance? Acceptance { get; set; }

    [Parameter]
    public bool ShowAcceptanceMetadata { get; set; }

    private MandatoryInfoAcceptanceStatus AcceptanceStatus
    {
        get
        {
            if (this.Acceptance is null)
                return MandatoryInfoAcceptanceStatus.MISSING;

            if (!string.Equals(this.Acceptance.AcceptedVersion, this.Info.VersionText, StringComparison.Ordinal))
                return MandatoryInfoAcceptanceStatus.VERSION_CHANGED;

            if (!string.Equals(this.Acceptance.AcceptedHash, this.Info.GetAcceptanceHash(), StringComparison.Ordinal))
                return MandatoryInfoAcceptanceStatus.CONTENT_CHANGED;

            return MandatoryInfoAcceptanceStatus.ACCEPTED;
        }
    }
}