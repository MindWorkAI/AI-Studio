namespace AIStudio.Components.Blocks;

public partial class Changelog
{
    public readonly record struct Log(int Build, string Display, string Filename)
    {
        #region Overrides of ValueType

        public override string ToString() => this.Display;

        #endregion
    }
    
    public static readonly Log[] LOGS = 
    [
        new (149, "v0.5.0, build 149 (2024-06-02 18:51 UTC)", "v0.5.0.md"),
        new (138, "v0.4.0, build 138 (2024-05-26 13:26 UTC)", "v0.4.0.md"),
        new (120, "v0.3.0, build 120 (2024-05-18 21:57 UTC)", "v0.3.0.md"),
        new (90, "v0.2.0, build 90 (2024-05-04 10:50 UTC)", "v0.2.0.md"),
        new (45, "v0.1.0, build 45 (2024-04-20 21:27 UTC)", "v0.1.0.md"),

    ];
}