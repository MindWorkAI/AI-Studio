using AIStudio.Tools.PowerPoint;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Groups the choices of a layout per kind of slide into one collapsed panel per area, e.g., teaching
/// or science, since more than a hundred choices in one list help nobody.
/// </summary>
/// <remarks>
/// The choice itself comes from the caller: the Slide Assistant uses a plain select, and its
/// settings dialog a configuration select. The header of an area tells how many of its kinds have a
/// layout, so that no choice hides in a collapsed panel.
/// </remarks>
public partial class LayoutChoicePanels : ComponentBase
{
    /// <summary>
    /// Renders the choice of a layout for one kind of slide.
    /// </summary>
    [Parameter]
    public RenderFragment<LayoutPurpose> ChoiceContent { get; set; } = _ => _ => { };

    /// <summary>
    /// Whether a layout is chosen for a kind of slide.
    /// </summary>
    [Parameter]
    public Func<LayoutPurpose, bool> IsChosen { get; set; } = _ => false;

    private readonly HashSet<LayoutPurposeArea> expandedAreas = [];

    private string HeaderTextOf(LayoutPurposeArea area)
    {
        var chosen = Enum.GetValues<LayoutPurpose>().Count(purpose => purpose.Area() == area && this.IsChosen(purpose));
        return chosen == 0 ? area.Label() : $"{area.Label()} ({chosen})";
    }

    private void SetExpanded(LayoutPurposeArea area, bool expanded)
    {
        if (expanded)
            this.expandedAreas.Add(area);
        else
            this.expandedAreas.Remove(area);
    }

    private static string IconOf(LayoutPurposeArea area) => area switch
    {
        LayoutPurposeArea.FRAMING => Icons.Material.Filled.FormatListNumbered,
        LayoutPurposeArea.GENERAL => Icons.Material.Filled.ViewQuilt,
        LayoutPurposeArea.TEACHING => Icons.Material.Filled.School,
        LayoutPurposeArea.BUSINESS => Icons.Material.Filled.BusinessCenter,
        LayoutPurposeArea.SCIENCE => Icons.Material.Filled.Science,
        LayoutPurposeArea.MEDICINE => Icons.Material.Filled.MedicalServices,
        LayoutPurposeArea.LAW => Icons.Material.Filled.Gavel,

        _ => Icons.Material.Filled.ViewQuilt,
    };
}