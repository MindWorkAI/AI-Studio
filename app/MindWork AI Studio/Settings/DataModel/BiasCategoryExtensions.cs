namespace AIStudio.Settings.DataModel;

public static class BiasCategoryExtensions
{
    public static string ToName(this BiasCategory biasCategory) => biasCategory switch
    {
        BiasCategory.WHAT_SHOULD_WE_REMEMBER => "What should we remember?",
        BiasCategory.TOO_MUCH_INFORMATION => "Too much information",
        BiasCategory.NOT_ENOUGH_MEANING => "Not enough meaning",
        BiasCategory.NEED_TO_ACT_FAST => "Need to act fast",
        
        _ => "Unknown category",
    };
    
    public static string GetThoughts(this BiasCategory biasCategory) => biasCategory switch
    {
        BiasCategory.WHAT_SHOULD_WE_REMEMBER => 
            """
            - We store memories differently based on how they were experienced
            - We reduce events and lists to their key elements
            - We discard specifics to form generalities
            - We edit and reinforce some memories after the fact
            """,
        
        BiasCategory.TOO_MUCH_INFORMATION =>
            """
            - We notice things already primed in memory or repeated often
            - Bizarre, funny, visually-striking, or anthropomorphic things stick out more than non-bizarre/unfunny things
            - We notice when something has changed
            - We are drawn to details that confirm our own existing beliefs
            - We notice flaws in others more easily than we notice flaws in ourselves    
            """,
        
        BiasCategory.NOT_ENOUGH_MEANING =>
            """
            - We tend to find stories and patterns even when looking at sparse data
            - We fill in characteristics from stereotypes, generalities, and prior histories
            - We imagine things and people we’re familiar with or fond of as better
            - We simplify probabilities and numbers to make them easier to think about
            - We project our current mindset and assumptions onto the past and future
            - We think we know what other people are thinking
            """,
        
        BiasCategory.NEED_TO_ACT_FAST => 
            """
            - We favor simple-looking options and complete information over complex, ambiguous options
            - To avoid mistakes, we aim to preserve autonomy and group status, and avoid irreversible decisions
            - To get things done, we tend to complete things we’ve invested time & energy in
            - To stay focused, we favor the immediate, relatable thing in front of us
            - To act, we must be confident we can make an impact and feel what we do is important
            """,
        
        _ => string.Empty,
    };
}