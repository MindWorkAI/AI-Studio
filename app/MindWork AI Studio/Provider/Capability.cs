namespace AIStudio.Provider;

/// <summary>
/// Represents the capabilities of an AI model.
/// </summary>
public enum Capability
{
    /// <summary>
    /// No capabilities specified.
    /// </summary>
    NONE,
    
    /// <summary>
    /// We don't know what the AI model can do.
    /// </summary>
    UNKNOWN,
    
    /// <summary>
    /// The AI model can perform text input.
    /// </summary>
    TEXT_INPUT,
    
    /// <summary>
    /// The AI model can perform audio input, such as music or sound.
    /// </summary>
    AUDIO_INPUT,
    
    /// <summary>
    /// The AI model can perform one image input, such as one photo or drawing.
    /// </summary>
    SINGLE_IMAGE_INPUT,
    
    /// <summary>
    /// The AI model can perform multiple images as input, such as multiple photos or drawings.
    /// </summary>
    MULTIPLE_IMAGE_INPUT,
    
    /// <summary>
    /// The AI model can perform speech input.
    /// </summary>
    SPEECH_INPUT,
    
    /// <summary>
    /// The AI model can perform video input, such as video files or streams.
    /// </summary>
    VIDEO_INPUT,
    
    /// <summary>
    /// The AI model can generate text output.
    /// </summary>
    TEXT_OUTPUT,
    
    /// <summary>
    /// The AI model can generate audio output, such as music or sound.
    /// </summary>
    AUDIO_OUTPUT,
    
    /// <summary>
    /// The AI model can generate image output, such as photos or drawings.
    /// </summary>
    IMAGE_OUTPUT,
    
    /// <summary>
    /// The AI model can generate speech output.
    /// </summary>
    SPEECH_OUTPUT,
    
    /// <summary>
    /// The AI model can generate video output.
    /// </summary>
    VIDEO_OUTPUT,
    
    /// <summary>
    /// The AI model can perform reasoning tasks.
    /// </summary>
    OPTIONAL_REASONING,
    
    /// <summary>
    /// The AI model always performs reasoning.
    /// </summary>
    ALWAYS_REASONING,
    
    /// <summary>
    /// The AI model can embed information or data.
    /// </summary>
    EMBEDDING,
    
    /// <summary>
    /// The AI model can perform in real-time.
    /// </summary>
    REALTIME,
    
    /// <summary>
    /// The AI model can perform function calling, such as invoking APIs or executing functions.
    /// </summary>
    FUNCTION_CALLING,
}