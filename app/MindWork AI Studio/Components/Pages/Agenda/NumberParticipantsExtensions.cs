namespace AIStudio.Components.Pages.Agenda;

public static class NumberParticipantsExtensions
{
    public static string Name(this NumberParticipants numberParticipants) => numberParticipants switch
    {
        NumberParticipants.NOT_SPECIFIED => "Please select how many participants are expected",
        
        NumberParticipants.PEER_TO_PEER => "2 (peer to peer)",
        
        NumberParticipants.SMALL_GROUP => "3 - 5 (small group)",
        NumberParticipants.LARGE_GROUP => "6 - 12 (large group)",
        NumberParticipants.MULTIPLE_SMALL_GROUPS => "13 - 20 (multiple small groups)",
        NumberParticipants.MULTIPLE_LARGE_GROUPS => "21 - 30 (multiple large groups)",
        
        NumberParticipants.SYMPOSIUM => "31 - 100 (symposium)",
        NumberParticipants.CONFERENCE => "101 - 200 (conference)",
        NumberParticipants.CONGRESS => "201 - 1,000 (congress)",
        
        NumberParticipants.LARGE_EVENT => "1,000+ (large event)",
        
        _ => "Unknown"
    };
}