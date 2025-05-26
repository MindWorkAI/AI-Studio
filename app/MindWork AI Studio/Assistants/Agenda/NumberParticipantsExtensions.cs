namespace AIStudio.Assistants.Agenda;

public static class NumberParticipantsExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(NumberParticipantsExtensions).Namespace, nameof(NumberParticipantsExtensions));
    
    public static string Name(this NumberParticipants numberParticipants) => numberParticipants switch
    {
        NumberParticipants.NOT_SPECIFIED => TB("Please select how many participants are expected"),
        
        NumberParticipants.PEER_TO_PEER => TB("2 (peer to peer)"),
        
        NumberParticipants.SMALL_GROUP => TB("3 - 5 (small group)"),
        NumberParticipants.LARGE_GROUP => TB("6 - 12 (large group)"),
        NumberParticipants.MULTIPLE_SMALL_GROUPS => TB("13 - 20 (multiple small groups)"),
        NumberParticipants.MULTIPLE_LARGE_GROUPS => TB("21 - 30 (multiple large groups)"),
        
        NumberParticipants.SYMPOSIUM => TB("31 - 100 (symposium)"),
        NumberParticipants.CONFERENCE => TB("101 - 200 (conference)"),
        NumberParticipants.CONGRESS => TB("201 - 1,000 (congress)"),
        
        NumberParticipants.LARGE_EVENT => TB("1,000+ (large event)"),
        
        _ => "Unknown"
    };
}