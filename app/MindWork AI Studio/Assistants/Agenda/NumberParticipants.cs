namespace AIStudio.Assistants.Agenda;

public enum NumberParticipants
{
    NOT_SPECIFIED,
    
    PEER_TO_PEER,          // 2 participants
    
    SMALL_GROUP,           // 3   -  5 participants
    LARGE_GROUP,           // 6   - 12 participants
    MULTIPLE_SMALL_GROUPS, // 13  - 20 participants
    MULTIPLE_LARGE_GROUPS, // 21  - 30 participants
    
    SYMPOSIUM,             // 31  - 100 participants
    CONFERENCE,            // 101 - 200 participants
    CONGRESS,              // 201 - 1000 participants
    LARGE_EVENT,           // 1000+ participants
}