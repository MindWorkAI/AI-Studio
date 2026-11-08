namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// How the BeyondFacts Lab addresses the person in languages which tell a formal address from an informal one, such as German.
/// </summary>
public enum BflAddressForm
{
    /// <summary>
    /// The formal address, such as "Sie" in German. The texts of AI Studio use it as well.
    /// </summary>
    FORMAL,

    /// <summary>
    /// The informal address, such as "du" in German.
    /// </summary>
    INFORMAL,
}