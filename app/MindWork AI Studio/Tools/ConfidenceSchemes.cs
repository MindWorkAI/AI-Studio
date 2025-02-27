namespace AIStudio.Tools;

public enum ConfidenceSchemes
{
    TRUST_ALL,
    TRUST_USA_EUROPE,
    TRUST_USA,
    TRUST_EUROPE,
    TRUST_ASIA,
    
    LOCAL_TRUST_ONLY,
    
    CUSTOM = 10_000,
}