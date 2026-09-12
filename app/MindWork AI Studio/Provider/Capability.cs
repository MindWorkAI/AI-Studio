namespace AIStudio.Provider;

/// <summary>
/// Represents the capabilities of an AI model.
/// </summary>
/// <remarks>
/// A set of capabilities is one value, not a collection: a model profile carries this enum as a
/// single field, and asking whether a capability is present is one bit test instead of a walk
/// through a list. That is why the members are powers of two.
///
/// The numeric values are an implementation detail and are never written anywhere. Overrides,
/// plugins, and the settings file all address a capability by its name, so the names are the part
/// which must not change. Removing a member would silently drop the override an organization wrote
/// for it, which is why the members we no longer hand out ourselves are still here.
///
/// Adding a member means adding the next free bit. Sixty-four of them fit; should they ever run
/// out, the answer is a second enum next to this one rather than a wider underlying type, because
/// widening changes the meaning of every value already written down.
/// </remarks>
[Flags]
public enum Capability : ulong
{
    /// <summary>
    /// No capabilities specified.
    /// </summary>
    NONE = 0,

    /// <summary>
    /// We don't know what the AI model can do.
    /// </summary>
    UNKNOWN = 1UL << 0,

    /// <summary>
    /// The AI model can perform text input.
    /// </summary>
    TEXT_INPUT = 1UL << 1,

    /// <summary>
    /// The AI model can perform audio input, such as music or sound.
    /// </summary>
    AUDIO_INPUT = 1UL << 2,

    /// <summary>
    /// The AI model can perform one image input, such as one photo or drawing.
    /// </summary>
    SINGLE_IMAGE_INPUT = 1UL << 3,

    /// <summary>
    /// The AI model can perform multiple images as input, such as multiple photos or drawings.
    /// </summary>
    MULTIPLE_IMAGE_INPUT = 1UL << 4,

    /// <summary>
    /// The AI model can perform speech input.
    /// </summary>
    SPEECH_INPUT = 1UL << 5,

    /// <summary>
    /// The AI model can perform video input, such as video files or streams.
    /// </summary>
    VIDEO_INPUT = 1UL << 6,

    /// <summary>
    /// The AI model can generate text output.
    /// </summary>
    TEXT_OUTPUT = 1UL << 7,

    /// <summary>
    /// The AI model can generate audio output, such as music or sound.
    /// </summary>
    AUDIO_OUTPUT = 1UL << 8,

    /// <summary>
    /// The AI model can generate image output, such as photos or drawings.
    /// </summary>
    IMAGE_OUTPUT = 1UL << 9,

    /// <summary>
    /// The AI model can generate speech output.
    /// </summary>
    SPEECH_OUTPUT = 1UL << 10,

    /// <summary>
    /// The AI model can generate video output.
    /// </summary>
    VIDEO_OUTPUT = 1UL << 11,

    /// <summary>
    /// The AI model can perform reasoning tasks. You can enable reasoning optionally, but it is disabled by default.
    /// </summary>
    /// <remarks>
    /// Override vocabulary. A model profile states how a model reasons through its ReasoningSupport
    /// field and never sets this flag, because the three reasoning flags can be combined into
    /// answers no model can give. Asking a profile whether it has this capability always says no.
    /// </remarks>
    OPTIONAL_REASONING = 1UL << 12,

    /// <summary>
    /// The AI model always performs reasoning. There is no option to disable reasoning.
    /// </summary>
    /// <remarks>
    /// Override vocabulary. A model profile states how a model reasons through its ReasoningSupport
    /// field and never sets this flag, because the three reasoning flags can be combined into
    /// answers no model can give. Asking a profile whether it has this capability always says no.
    /// </remarks>
    ALWAYS_REASONING = 1UL << 13,

    /// <summary>
    /// The AI model performs optional reasoning, but it is enabled by default.
    /// </summary>
    /// <remarks>
    /// Override vocabulary. A model profile states how a model reasons through its ReasoningSupport
    /// field and never sets this flag, because the three reasoning flags can be combined into
    /// answers no model can give. Asking a profile whether it has this capability always says no.
    /// </remarks>
    REASONING_BY_DEFAULT = 1UL << 14,

    /// <summary>
    /// The AI model can embed information or data.
    /// </summary>
    EMBEDDING = 1UL << 15,

    /// <summary>
    /// The AI model can perform in real-time.
    /// </summary>
    REALTIME = 1UL << 16,

    /// <summary>
    /// The AI model can perform function calling, such as invoking APIs or executing functions.
    /// </summary>
    FUNCTION_CALLING = 1UL << 17,

    /// <summary>
    /// The AI model can perform web search to retrieve information from the internet.
    /// </summary>
    WEB_SEARCH = 1UL << 18,

    /// <summary>
    /// The AI model is used via the Chat Completion API.
    /// </summary>
    CHAT_COMPLETION_API = 1UL << 19,

    /// <summary>
    /// The AI model is used via the Responses API.
    /// </summary>
    RESPONSES_API = 1UL << 20,
}