namespace AIStudio.Settings.DataModel;

/// <summary>
/// How a text addresses the person in languages which tell a formal address from an informal one, such as German,
/// French, or Spanish.
/// </summary>
/// <remarks>
/// Only texts of TA() have both forms; see I18N.AddressForm.
/// </remarks>
public enum AddressForm
{
    /// <summary>
    /// The formal address, such as "Sie" in German, "vous" in French, or "usted" in Spanish. All texts of AI Studio
    /// use it, unless the person chose otherwise.
    /// </summary>
    FORMAL,

    /// <summary>
    /// The informal address, such as "du" in German, "tu" in French, or "tú" in Spanish.
    /// </summary>
    INFORMAL,
}