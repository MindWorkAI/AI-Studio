namespace AIStudio.Components;

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
        new (179, "v0.9.4, build 179 (2024-09-06 20:11 UTC)", "v0.9.4.md"),
        new (178, "v0.9.3, build 178 (2024-09-06 11:06 UTC)", "v0.9.3.md"),
        new (177, "v0.9.2, build 177 (2024-09-05 09:19 UTC)", "v0.9.2.md"),
        new (176, "v0.9.1, build 176 (2024-09-04 13:48 UTC)", "v0.9.1.md"),
        new (175, "v0.9.0, build 175 (2024-09-03 16:00 UTC)", "v0.9.0.md"),
        new (174, "v0.8.12, build 174 (2024-08-24 08:30 UTC)", "v0.8.12.md"),
        new (173, "v0.8.11, build 173 (2024-08-21 07:03 UTC)", "v0.8.11.md"),
        new (172, "v0.8.10, build 172 (2024-08-18 19:44 UTC)", "v0.8.10.md"),
        new (171, "v0.8.9, build 171 (2024-08-18 10:35 UTC)", "v0.8.9.md"),
        new (170, "v0.8.8, build 170 (2024-08-14 06:30 UTC)", "v0.8.8.md"),
        new (169, "v0.8.7, build 169 (2024-08-01 19:08 UTC)", "v0.8.7.md"),
        new (168, "v0.8.6, build 168 (2024-08-01 19:50 UTC)", "v0.8.6.md"),
        new (167, "v0.8.5, build 167 (2024-07-28 16:44 UTC)", "v0.8.5.md"),
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