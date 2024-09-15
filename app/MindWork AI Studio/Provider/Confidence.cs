using AIStudio.Settings;

namespace AIStudio.Provider;

public sealed record Confidence
{
    public ConfidenceLevel Level { get; private init; } = ConfidenceLevel.UNKNOWN;

    public string Region { get; private init; } = string.Empty;

    public string Description { get; private init; } = string.Empty;

    public List<string> Sources { get; private init; } = [];

    private Confidence()
    {
    }
    
    public Confidence WithSources(params string[] sources) => this with { Sources = sources.ToList() };

    public Confidence WithRegion(string region) => this with { Region = region };
    
    public Confidence WithLevel(ConfidenceLevel level) => this with { Level = level };
    
    public string StyleBorder(SettingsManager settingsManager) => $"border: 2px solid {this.Level.GetColor(settingsManager)}; border-radius: 6px;";
    
    public string SetColorStyle(SettingsManager settingsManager) => $"--confidence-color: {this.Level.GetColor(settingsManager)};";

    public static readonly Confidence NONE = new()
    {
        Level = ConfidenceLevel.NONE,
        Description = 
            """
            No provider selected. Please select a provider to get see its confidence level.
            """,
    };
    
    public static readonly Confidence USA_NOT_TRUSTED = new()
    {
        Level = ConfidenceLevel.UNTRUSTED,
        Description = "The provider operates its service from the USA and is subject to **U.S. jurisdiction**. In case of suspicion, authorities in the USA can access your data. The provider's terms of service state that **all your data can be used by the provider at will.**",
    };

    public static readonly Confidence UNKNOWN = new()
    {
        Level = ConfidenceLevel.UNKNOWN,
        Description = "The trust level of this provider **has not yet** been thoroughly **investigated and evaluated**. We do not know if your data is safe.",
    };

    public static readonly Confidence USA_NO_TRAINING = new()
    {
        Level = ConfidenceLevel.MODERATE,
        Description = "The provider operates its service from the USA and is subject to **US jurisdiction**. In case of suspicion, authorities in the USA can access your data. However, **your data is not used for training** purposes.",
    };
    
    public static readonly Confidence GDPR_NO_TRAINING = new()
    {
        Level = ConfidenceLevel.MEDIUM,
        Description = "The provider is located in the EU and is subject to the **GDPR** (General Data Protection Regulation). Additionally, the provider states that **your data is not used for training**.",
    };
    
    public static readonly Confidence SELF_HOSTED = new()
    {
        Level = ConfidenceLevel.HIGH,
        Description = "You or your organization operate the LLM locally or within your trusted network. In terms of data processing and security, this is the best possible way.",
    };
}