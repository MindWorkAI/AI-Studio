namespace AIStudio.Tools.RAG;

/// <summary>
/// The type of the retrieved content.
/// </summary>
public enum RetrievalContentType
{
    NOT_SPECIFIED,
    
    //
    // Text Content:
    //
    DOCUMENT,
    ARTICLE,
    BOOK,
    CHAPTER,
    PAPER,
    THESIS,
    BUSINESS_CONCEPT,
    DICTIONARY,
    ENCYCLOPEDIA,
    GLOSSARY,
    JOURNAL,
    MAGAZINE,
    NEWSPAPER,
    REPORT,
    REVIEW,
    WEBSITE,
    IDEA,
    CONCEPT,
    DEFINITION,
    EXAMPLE,
    QUOTE,
    DRAFT,
    SCRIPT,
    TRANSCRIPT,
    SUBTITLE,
    CAPTION,
    DIALOGUE,
    
    //
    // Image Content:
    //
    PHOTO,
    ILLUSTRATION,
    DIAGRAM,
    CHART,
    ART,
    DRAWING,
    PAINTING,
    SKETCH,
    MAP,
    CHARACTER,
    SCENE,
    
    //
    // Audio Content:
    //
    SPEECH,
    PODCAST,
    AUDIOBOOK,
    INTERVIEW,
    LECTURE,
    TALK,
    SONG,
    MUSIC,
    SOUND,
    CALL,
    VOICE_ACTING,
    AUDIO_DESCRIPTION,
    AUDIO_GUIDE,
    VOICE_DIALOGUE,
    
    //
    // Video Content:
    //
    MOVIE,
    FILM,
    TV_SHOW,
    SERIES,
    EPISODE,
    DOCUMENTARY,
    TUTORIAL,
    RECORDED_LECTURE,
    WEBINAR,
    VIDEO_GAME,
    ANIMATION,
    CUTSCENE,
    TRAILER,
    ADVERTISEMENT,
}