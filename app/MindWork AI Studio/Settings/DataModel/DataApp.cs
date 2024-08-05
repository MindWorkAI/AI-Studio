namespace AIStudio.Settings.DataModel;

public sealed class DataApp
{
    /// <summary>
    /// Should we save energy? When true, we will update content streamed
    /// from the server, i.e., AI, less frequently.
    /// </summary>
    public bool IsSavingEnergy { get; set; }
    
    /// <summary>
    /// Should we enable spellchecking for all input fields?
    /// </summary>
    public bool EnableSpellchecking { get; set; }

    /// <summary>
    /// If and when we should look for updates.
    /// </summary>
    public UpdateBehavior UpdateBehavior { get; set; } = UpdateBehavior.ONCE_STARTUP;
    
    /// <summary>
    /// The navigation behavior.
    /// </summary>
    public NavBehavior NavigationBehavior { get; set; } = NavBehavior.EXPAND_ON_HOVER;
}