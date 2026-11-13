using AIStudio.Components;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The mode of the Model Comparison assistant in which the user works on real tasks with both models, one after the
/// other, and decides which chat helped more.
/// </summary>
public partial class AssistantModelComparisonTasks : MSGComponentBase
{
    [Inject]
    private NavigationManager NavigationManager { get; init; } = null!;

    #region Overrides of MSGComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        if (!ModelComparisonAccess.CanOpen(this.SettingsManager, T("Model Comparison")))
            this.NavigationManager.NavigateTo(Routes.ASSISTANTS);
    }

    #endregion

    /// <summary>
    /// Goes back to the choice between the two modes.
    /// </summary>
    private void SwitchMode() => this.NavigationManager.NavigateTo(Routes.ASSISTANT_MODEL_COMPARISON);
}