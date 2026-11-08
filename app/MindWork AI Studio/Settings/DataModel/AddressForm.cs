namespace AIStudio.Settings.DataModel;

/// <summary>
/// How a text addresses the person in languages which tell a formal address from an informal one, such as German.
/// </summary>
public enum AddressForm
{
    /// <summary>
    /// The formal address, such as "Sie" in German. All texts of AI Studio use it, unless the person chose otherwise.
    /// </summary>
    FORMAL,

    /// <summary>
    /// The informal address, such as "du" in German.
    /// </summary>
    INFORMAL,
}