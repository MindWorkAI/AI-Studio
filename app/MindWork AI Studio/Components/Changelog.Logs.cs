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
        new (213, "v0.9.38, build 213 (2025-03-17 18:18 UTC)", "v0.9.38.md"),
        new (212, "v0.9.37, build 212 (2025-03-16 20:32 UTC)", "v0.9.37.md"),
        new (211, "v0.9.36, build 211 (2025-03-15 10:42 UTC)", "v0.9.36.md"),
        new (210, "v0.9.35, build 210 (2025-03-13 08:44 UTC)", "v0.9.35.md"),
        new (209, "v0.9.34, build 209 (2025-03-11 13:02 UTC)", "v0.9.34.md"),
        new (208, "v0.9.33, build 208 (2025-03-11 08:14 UTC)", "v0.9.33.md"),
        new (207, "v0.9.32, build 207 (2025-03-08 20:15 UTC)", "v0.9.32.md"),
        new (206, "v0.9.31, build 206 (2025-03-03 15:33 UTC)", "v0.9.31.md"),
        new (205, "v0.9.30, build 205 (2025-02-24 19:55 UTC)", "v0.9.30.md"),
        new (204, "v0.9.29, build 204 (2025-02-24 13:48 UTC)", "v0.9.29.md"),
        new (203, "v0.9.28, build 203 (2025-02-09 16:33 UTC)", "v0.9.28.md"),
        new (202, "v0.9.27, build 202 (2025-01-21 18:24 UTC)", "v0.9.27.md"),
        new (201, "v0.9.26, build 201 (2025-01-13 19:11 UTC)", "v0.9.26.md"),
        new (200, "v0.9.25, build 200 (2025-01-04 18:33 UTC)", "v0.9.25.md"),
        new (199, "v0.9.24, build 199 (2025-01-04 11:40 UTC)", "v0.9.24.md"),
        new (198, "v0.9.23, build 198 (2025-01-02 19:39 UTC)", "v0.9.23.md"),
        new (197, "v0.9.22, build 197 (2024-12-04 10:58 UTC)", "v0.9.22.md"),
        new (196, "v0.9.21, build 196 (2024-11-23 12:22 UTC)", "v0.9.21.md"),
        new (195, "v0.9.20, build 195 (2024-11-16 20:44 UTC)", "v0.9.20.md"),
        new (194, "v0.9.19, build 194 (2024-11-14 05:58 UTC)", "v0.9.19.md"),
        new (193, "v0.9.18, build 193 (2024-11-09 21:10 UTC)", "v0.9.18.md"),
        new (192, "v0.9.17, build 192 (2024-11-03 11:11 UTC)", "v0.9.17.md"),
        new (191, "v0.9.16, build 191 (2024-11-02 22:04 UTC)", "v0.9.16.md"),
        new (190, "v0.9.15, build 190 (2024-10-28 15:04 UTC)", "v0.9.15.md"),
        new (189, "v0.9.14, build 189 (2024-10-18 08:48 UTC)", "v0.9.14.md"),
        new (188, "v0.9.13, build 188 (2024-10-07 11:18 UTC)", "v0.9.13.md"),
        new (187, "v0.9.12, build 187 (2024-09-15 20:49 UTC)", "v0.9.12.md"),
        new (186, "v0.9.11, build 186 (2024-09-15 10:33 UTC)", "v0.9.11.md"),
        new (185, "v0.9.10, build 185 (2024-09-12 20:52 UTC)", "v0.9.10.md"),
        new (184, "v0.9.9, build 184 (2024-09-11 21:10 UTC)", "v0.9.9.md"),
        new (183, "v0.9.8, build 183 (2024-09-09 13:10 UTC)", "v0.9.8.md"),
        new (182, "v0.9.7, build 182 (2024-09-08 20:55 UTC)", "v0.9.7.md"),
        new (181, "v0.9.6, build 181 (2024-09-08 09:30 UTC)", "v0.9.6.md"),
        new (180, "v0.9.5, build 180 (2024-09-07 16:33 UTC)", "v0.9.5.md"),
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