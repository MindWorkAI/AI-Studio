namespace AIStudio.Agents;

public enum Type
{
    /// <summary>
    /// Represents an unspecified agent type.
    /// </summary>
    UNSPECIFIED = 0,
    
    /// <summary>
    /// Represents a conversational agent who produces human-like responses and feedback (depending on the context and its job description).
    /// For example, an expert agent for a specific domain. Answers might be detailed and informative.
    /// </summary>
    CONVERSATIONAL,

    /// <summary>
    /// Represents a worker agent type who performs tasks and provides information or services (depending on the context and its job description).
    /// For example, a quality assurance agent who assesses the quality of a product or service. Answers might be short and concise.
    /// </summary>
    WORKER,

    /// <summary>
    /// Represents the system agent type who processes the input and provides a specific response (depending on the context and its job description).
    /// For example, a HTML content agent who processes the arbitrary HTML content and provides a structured Markdown response. Answers might be structured and formatted.
    /// </summary>
    SYSTEM,
}