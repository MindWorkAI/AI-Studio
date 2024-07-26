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
        new (166, "v0.8.4, build 166 (2024-07-26 06:53 UTC)", "v0.8.4.md"),
        new (165, "v0.8.3, build 165 (2024-07-25 13:25 UTC)", "v0.8.3.md"),
        new (164, "v0.8.2, build 164 (2024-07-16 18:03 UTC)", "v0.8.2.md"),
        new (163, "v0.8.1, build 163 (2024-07-16 08:32 UTC)", "v0.8.1.md"),
        new (162, "v0.8.0, build 162 (2024-07-14 19:39 UTC)", "v0.8.0.md"),
        new (161, "v0.7.1, build 161 (2024-07-13 11:42 UTC)", "v0.7.1.md"),
        new (160, "v0.7.0, build 160 (2024-07-13 08:21 UTC)", "v0.7.0.md"),
        new (159, "v0.6.3, build 159 (2024-07-03 18:26 UTC)", "v0.6.3.md"),
        new (158, "v0.6.2, build 158 (2024-07-01 18:03 UTC)", "v0.6.2.md"),
        new (157, "v0.6.1, build 157 (2024-06-30 19:00 UTC)", "v0.6.1.md"),
        new (156, "v0.6.0, build 156 (2024-06-30 12:49 UTC)", "v0.6.0.md"),
        new (155, "v0.5.2, build 155 (2024-06-25 18:07 UTC)", "v0.5.2.md"),
        new (154, "v0.5.1, build 154 (2024-06-25 15:35 UTC)", "v0.5.1.md"),
        new (149, "v0.5.0, build 149 (2024-06-02 18:51 UTC)", "v0.5.0.md"),
        new (138, "v0.4.0, build 138 (2024-05-26 13:26 UTC)", "v0.4.0.md"),
        new (120, "v0.3.0, build 120 (2024-05-18 21:57 UTC)", "v0.3.0.md"),
        new (90, "v0.2.0, build 90 (2024-05-04 10:50 UTC)", "v0.2.0.md"),
        new (45, "v0.1.0, build 45 (2024-04-20 21:27 UTC)", "v0.1.0.md"),

    ];
}