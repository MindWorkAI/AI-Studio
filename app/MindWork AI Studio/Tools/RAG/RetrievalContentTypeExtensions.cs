using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.RAG;

public static class RetrievalContentTypeExtensions
{
    public static RetrievalContentType ToRetrievalContentType(this Context eriContext)
    {
        //
        // Right now, we have to parse the category string along the type enum to
        // determine the common retrieval content type. In future ERI versions, we
        // might use the same enum.
        //
        
        var lowerCategory = eriContext.Category.ToLowerInvariant();
        var type = eriContext.Type;
        return type switch
        {
            ContentType.TEXT when lowerCategory.Contains("book") => RetrievalContentType.TEXT_BOOK,
            ContentType.TEXT when lowerCategory.Contains("paper") => RetrievalContentType.TEXT_PAPER,
            ContentType.TEXT when lowerCategory.Contains("dictionary") => RetrievalContentType.TEXT_DICTIONARY,
            ContentType.TEXT when lowerCategory.Contains("encyclopedia") => RetrievalContentType.TEXT_ENCYCLOPEDIA,
            ContentType.TEXT when lowerCategory.Contains("glossary") => RetrievalContentType.TEXT_GLOSSARY,
            ContentType.TEXT when lowerCategory.Contains("journal") => RetrievalContentType.TEXT_JOURNAL,
            ContentType.TEXT when lowerCategory.Contains("magazine") => RetrievalContentType.TEXT_MAGAZINE,
            ContentType.TEXT when lowerCategory.Contains("newspaper") => RetrievalContentType.TEXT_NEWSPAPER,
            ContentType.TEXT when lowerCategory.Contains("report") => RetrievalContentType.TEXT_REPORT,
            ContentType.TEXT when lowerCategory.Contains("review") => RetrievalContentType.TEXT_REVIEW,
            ContentType.TEXT when lowerCategory.Contains("website") => RetrievalContentType.TEXT_WEBSITE,
            ContentType.TEXT when lowerCategory.Contains("idea") => RetrievalContentType.TEXT_IDEA,
            
            ContentType.TEXT when lowerCategory.Contains("business concept") => RetrievalContentType.TEXT_BUSINESS_CONCEPT,
            ContentType.TEXT when lowerCategory.Contains("concept") => RetrievalContentType.TEXT_CONCEPT,
            
            ContentType.TEXT when lowerCategory.Contains("definition") => RetrievalContentType.TEXT_DEFINITION,
            ContentType.TEXT when lowerCategory.Contains("example") => RetrievalContentType.TEXT_EXAMPLE,
            ContentType.TEXT when lowerCategory.Contains("quote") => RetrievalContentType.TEXT_QUOTE,
            ContentType.TEXT when lowerCategory.Contains("article") => RetrievalContentType.TEXT_ARTICLE,
            ContentType.TEXT when lowerCategory.Contains("chapter") => RetrievalContentType.TEXT_CHAPTER,
            
            ContentType.TEXT when lowerCategory.Contains("thesis") => RetrievalContentType.TEXT_THESIS,
            ContentType.TEXT when lowerCategory.Contains("dissertation") => RetrievalContentType.TEXT_THESIS,
            
            ContentType.TEXT when lowerCategory.Contains("draft") => RetrievalContentType.TEXT_DRAFT,
            ContentType.TEXT when lowerCategory.Contains("script") => RetrievalContentType.TEXT_SCRIPT,
            ContentType.TEXT when lowerCategory.Contains("transcript") => RetrievalContentType.TEXT_TRANSCRIPT,
            ContentType.TEXT when lowerCategory.Contains("subtitle") => RetrievalContentType.TEXT_SUBTITLE,
            ContentType.TEXT when lowerCategory.Contains("caption") => RetrievalContentType.TEXT_CAPTION,
            ContentType.TEXT when lowerCategory.Contains("dialogue") => RetrievalContentType.TEXT_DIALOGUE,
            ContentType.TEXT when lowerCategory.Contains("project proposal") => RetrievalContentType.TEXT_PROJECT_PROPOSAL,
            ContentType.TEXT when lowerCategory.Contains("project plan") => RetrievalContentType.TEXT_PROJECT_PLAN,
            ContentType.TEXT when lowerCategory.Contains("spreadsheet") => RetrievalContentType.TEXT_SPREADSHEET,
            
            ContentType.TEXT when lowerCategory.Contains("presentation") => RetrievalContentType.TEXT_PRESENTATION,
            ContentType.TEXT when lowerCategory.Contains("powerpoint") => RetrievalContentType.TEXT_PRESENTATION,
            ContentType.TEXT when lowerCategory.Contains("slide") => RetrievalContentType.TEXT_PRESENTATION,
            
            ContentType.TEXT when lowerCategory.Contains("meeting minutes") => RetrievalContentType.TEXT_MEETING_MINUTES,
            ContentType.TEXT when lowerCategory.Contains("email") => RetrievalContentType.TEXT_EMAIL,
            ContentType.TEXT when lowerCategory.Contains("protocol") => RetrievalContentType.TEXT_PROTOCOL,
            
            ContentType.TEXT => RetrievalContentType.TEXT_DOCUMENT,
            
            
            ContentType.IMAGE when lowerCategory.Contains("photo") => RetrievalContentType.IMAGE_PHOTO,
            ContentType.IMAGE when lowerCategory.Contains("illustration") => RetrievalContentType.IMAGE_ILLUSTRATION,
            ContentType.IMAGE when lowerCategory.Contains("diagram") => RetrievalContentType.IMAGE_DIAGRAM,
            ContentType.IMAGE when lowerCategory.Contains("chart") => RetrievalContentType.IMAGE_CHART,
            ContentType.IMAGE when lowerCategory.Contains("art") => RetrievalContentType.IMAGE_ART,
            ContentType.IMAGE when lowerCategory.Contains("drawing") => RetrievalContentType.IMAGE_DRAWING,
            ContentType.IMAGE when lowerCategory.Contains("painting") => RetrievalContentType.IMAGE_PAINTING,
            ContentType.IMAGE when lowerCategory.Contains("sketch") => RetrievalContentType.IMAGE_SKETCH,
            ContentType.IMAGE when lowerCategory.Contains("map") => RetrievalContentType.IMAGE_MAP,
            ContentType.IMAGE when lowerCategory.Contains("scene") => RetrievalContentType.IMAGE_SCENE,
            ContentType.IMAGE when lowerCategory.Contains("character") => RetrievalContentType.IMAGE_CHARACTER,
            ContentType.IMAGE when lowerCategory.Contains("landscape") => RetrievalContentType.IMAGE_LANDSCAPE,
            ContentType.IMAGE when lowerCategory.Contains("portrait") => RetrievalContentType.IMAGE_PORTRAIT,
            ContentType.IMAGE when lowerCategory.Contains("poster") => RetrievalContentType.IMAGE_POSTER,
            ContentType.IMAGE when lowerCategory.Contains("logo") => RetrievalContentType.IMAGE_LOGO,
            ContentType.IMAGE when lowerCategory.Contains("icon") => RetrievalContentType.IMAGE_ICON,
            
            ContentType.IMAGE when lowerCategory.Contains("satellite") => RetrievalContentType.IMAGE_SATELLITE_IMAGE,
            ContentType.IMAGE when lowerCategory.Contains("EO") => RetrievalContentType.IMAGE_SATELLITE_IMAGE,
            ContentType.IMAGE when lowerCategory.Contains("earth observation") => RetrievalContentType.IMAGE_SATELLITE_IMAGE,
            
            ContentType.IMAGE => RetrievalContentType.NOT_SPECIFIED,
            
            
            ContentType.AUDIO when lowerCategory.Contains("speech") => RetrievalContentType.AUDIO_SPEECH,
            
            ContentType.AUDIO when lowerCategory.Contains("podcast") => RetrievalContentType.AUDIO_PODCAST,
            ContentType.SPEECH when lowerCategory.Contains("podcast") => RetrievalContentType.AUDIO_PODCAST,
            
            ContentType.AUDIO when lowerCategory.Contains("book") => RetrievalContentType.AUDIO_BOOK,
            ContentType.SPEECH when lowerCategory.Contains("book") => RetrievalContentType.AUDIO_BOOK,
            
            ContentType.AUDIO when lowerCategory.Contains("interview") => RetrievalContentType.AUDIO_INTERVIEW,
            ContentType.SPEECH when lowerCategory.Contains("interview") => RetrievalContentType.AUDIO_INTERVIEW,
            
            ContentType.AUDIO when lowerCategory.Contains("lecture") => RetrievalContentType.AUDIO_LECTURE,
            ContentType.SPEECH when lowerCategory.Contains("lecture") => RetrievalContentType.AUDIO_LECTURE,
            
            ContentType.AUDIO when lowerCategory.Contains("talk") => RetrievalContentType.AUDIO_TALK,
            ContentType.SPEECH when lowerCategory.Contains("talk") => RetrievalContentType.AUDIO_TALK,
            
            ContentType.AUDIO when lowerCategory.Contains("song") => RetrievalContentType.AUDIO_SONG,
            ContentType.AUDIO when lowerCategory.Contains("music") => RetrievalContentType.AUDIO_MUSIC,
            ContentType.AUDIO when lowerCategory.Contains("sound") => RetrievalContentType.AUDIO_SOUND,
            ContentType.AUDIO when lowerCategory.Contains("call") => RetrievalContentType.AUDIO_CALL,
            ContentType.AUDIO when lowerCategory.Contains("voice acting") => RetrievalContentType.AUDIO_VOICE_ACTING,
            ContentType.AUDIO when lowerCategory.Contains("description") => RetrievalContentType.AUDIO_DESCRIPTION,
            ContentType.AUDIO when lowerCategory.Contains("guide") => RetrievalContentType.AUDIO_GUIDE,
            ContentType.AUDIO when lowerCategory.Contains("dialogue") => RetrievalContentType.AUDIO_DIALOGUE,
            
            ContentType.SPEECH => RetrievalContentType.AUDIO_SPEECH,
            ContentType.AUDIO => RetrievalContentType.NOT_SPECIFIED,
            
            
            ContentType.VIDEO when lowerCategory.Contains("movie") => RetrievalContentType.VIDEO_MOVIE,
            ContentType.VIDEO when lowerCategory.Contains("film") => RetrievalContentType.VIDEO_FILM,
            ContentType.VIDEO when lowerCategory.Contains("tv show") => RetrievalContentType.VIDEO_TV_SHOW,
            ContentType.VIDEO when lowerCategory.Contains("series") => RetrievalContentType.VIDEO_SERIES,
            ContentType.VIDEO when lowerCategory.Contains("episode") => RetrievalContentType.VIDEO_EPISODE,
            ContentType.VIDEO when lowerCategory.Contains("documentary") => RetrievalContentType.VIDEO_DOCUMENTARY,
            ContentType.VIDEO when lowerCategory.Contains("tutorial") => RetrievalContentType.VIDEO_TUTORIAL,
            ContentType.VIDEO when lowerCategory.Contains("lecture") => RetrievalContentType.VIDEO_LECTURE,
            ContentType.VIDEO when lowerCategory.Contains("webinar") => RetrievalContentType.VIDEO_WEBINAR,
            ContentType.VIDEO when lowerCategory.Contains("game") => RetrievalContentType.VIDEO_GAME,
            ContentType.VIDEO when lowerCategory.Contains("animation") => RetrievalContentType.VIDEO_ANIMATION,
            ContentType.VIDEO when lowerCategory.Contains("cutscene") => RetrievalContentType.VIDEO_CUTSCENE,
            ContentType.VIDEO when lowerCategory.Contains("trailer") => RetrievalContentType.VIDEO_TRAILER,
            ContentType.VIDEO when lowerCategory.Contains("advertisement") => RetrievalContentType.VIDEO_ADVERTISEMENT,
            
            ContentType.VIDEO => RetrievalContentType.NOT_SPECIFIED,
            
            ContentType.NONE => RetrievalContentType.NOT_SPECIFIED,
            
            _ => RetrievalContentType.UNKNOWN,
        };
    }
}