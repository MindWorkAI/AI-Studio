#!/usr/bin/env python3
"""
Generates the banner of the start page and of the README:
app/MindWork AI Studio/wwwroot/svg/banner.svg

The app shows the banner through an <img> element, where neither the app's CSS nor its web fonts
apply. Therefore, every text is converted into outlines, so the banner looks the same on Windows,
macOS, Linux, and GitHub. The text uses Inter (SIL Open Font License 1.1); the icons are the
Lucide icons the app itself uses, read from the Lucide font it ships (ISC License, with parts of
Feather under the MIT License).

See README.md in this folder for the setup.
"""

import argparse
import hashlib
import json
import logging
import sys
from pathlib import Path

import uharfbuzz as hb
from fontTools.pens.basePen import BasePen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
DEFAULT_OUTPUT = REPO_ROOT / "app" / "MindWork AI Studio" / "wwwroot" / "svg" / "banner.svg"

# The Lucide font the app ships and the code points of its icons, so the banner always shows the
# icons of the app in the version of the app. See app/SourceGeneratedMappings/Lucide/README.md.
LUCIDE_FONT_PATH = REPO_ROOT / "app" / "MindWork AI Studio" / "wwwroot" / "fonts" / "lucide.woff2"
LUCIDE_CODEPOINTS_PATH = REPO_ROOT / "app" / "SourceGeneratedMappings" / "Lucide" / "codepoints.json"

# The variable font Inter[opsz,wght].ttf from google/fonts, pinned to one commit. Another version
# of the font changes the outlines and therefore the generated file.
FONT_PATH = HERE / "Inter.ttf"
FONT_URL = "https://raw.githubusercontent.com/google/fonts/e1d6480102fed30739fead0faee463101f892c8f/ofl/inter/Inter%5Bopsz,wght%5D.ttf"
FONT_SHA256 = "29160a80ff49ddcab2c97711247e08b1fab27a484a329ce8b813d820dc559031"

#
# Content
#
EYEBROW = "MINDWORK"
NAME = "AI Studio"
TAGLINE = "Any model. Your data. Your rules."

# Two rows of four chips; the chips of one column share their width, so pair labels of similar
# length in a column. Each entry: (id, label, name of the Lucide icon as lucide.dev shows it).
CHIPS = [
    ("chat", "Chat", "message-square-text"),
    ("assistants", "Assistants", "layout-grid"),
    ("builder", "No-code builder", "wrench"),
    ("tools", "Agentic tools", "bot"),
    ("research", "Research", "globe"),
    ("transcribe", "Transcribe", "audio-lines"),
    ("documents", "Your documents", "file-text"),
    ("emails", "Your e-mails", "mail"),
]

#
# Look
#
WIDTH, HEIGHT, CORNER_RADIUS = 1920, 320, 24
CREAM = "#f6f0d8"

# When the window is wider than 1536 px, the start page crops up to 32 units at the top and at the
# bottom. Keep text and chips between y = 48 and y = 272.
LAYOUT = dict(
    sun=(1860, -50, 660),  # centre x, centre y, radius
    left=104, eyebrow_y=98, eyebrow_size=30, name_y=208, name_size=120, tag_y=258, tag_size=34,
    chip_font=29, chip_icon=30, chip_pad=22, chip_icon_gap=11, chip_h=62, chip_gap=14, chip_row_gap=14, chip_cy=164,
    right=104, chip_fill="#0b2e22", chip_fill_op=.35, chip_stroke_op=.3,
)

# Weights and optical sizes of the variable font:
EYEBROW_STYLE = dict(wght=600, opsz=32)
NAME_STYLE = dict(wght=800, opsz=32)
TEXT_STYLE = dict(wght=500, opsz=22)


def num(v):
    """Formats a coordinate with one decimal and without needless zeros, e.g. 0.5 -> .5, 2.0 -> 2."""
    s = f"{v:.1f}"
    if s.endswith(".0"):
        s = s[:-2]
    if s.startswith("0."):
        s = s[1:]
    elif s.startswith("-0."):
        s = "-" + s[2:]
    return "0" if s in ("-0", "") else s


class RecordingPen(BasePen):
    """Records the outline of glyphs as absolute drawing operations."""

    def __init__(self, glyph_set):
        super().__init__(glyph_set)
        self.ops = []

    def _moveTo(self, p):
        self.ops.append(("M", p))

    def _lineTo(self, p):
        self.ops.append(("L", p))

    def _qCurveToOne(self, c, p):
        self.ops.append(("Q", c, p))

    def _curveToOne(self, c1, c2, p):
        self.ops.append(("C", c1, c2, p))

    def _closePath(self):
        self.ops.append(("Z",))

    def _endPath(self):
        pass


def compact_path(ops):
    """
    Turns recorded operations into short path data: relative commands, coordinates rounded to
    tenths, and T wherever a quadratic control point mirrors the previous one, which is the case
    for every implied on-curve point of TrueType outlines.
    """
    # Rounding the absolute points first keeps the relative steps from drifting:
    to_tenths = lambda p: (round(p[0] * 10), round(p[1] * 10))
    out, last_cmd = [], None
    cur = start = (0, 0)
    prev_ctrl = None

    def emit(cmd, values):
        nonlocal last_cmd
        body = ""
        for value in values:
            text = num(value / 10)
            if body and not text.startswith("-"):
                body += " "
            body += text

        # A repeated command may omit its letter:
        if cmd == last_cmd and cmd not in ("m", "z"):
            out.append((" " if not body.startswith("-") else "") + body)
        else:
            out.append(cmd + body)

        # Further pairs after a moveto are linetos:
        last_cmd = "l" if cmd == "m" else cmd

    for op in ops:
        kind = op[0]
        if kind == "M":
            p = to_tenths(op[1])
            emit("m", (p[0] - cur[0], p[1] - cur[1]))
            cur = start = p
            prev_ctrl = None
        elif kind == "L":
            p = to_tenths(op[1])
            dx, dy = p[0] - cur[0], p[1] - cur[1]
            if dx == 0 and dy == 0:
                pass
            elif dy == 0:
                emit("h", (dx,))
            elif dx == 0:
                emit("v", (dy,))
            else:
                emit("l", (dx, dy))
            cur = p
            prev_ctrl = None
        elif kind == "Q":
            c, p = to_tenths(op[1]), to_tenths(op[2])
            mirrored = prev_ctrl is not None and abs(2 * cur[0] - prev_ctrl[0] - c[0]) <= 1 and abs(2 * cur[1] - prev_ctrl[1] - c[1]) <= 1
            if mirrored:
                emit("t", (p[0] - cur[0], p[1] - cur[1]))
                c = (2 * cur[0] - prev_ctrl[0], 2 * cur[1] - prev_ctrl[1])
            else:
                emit("q", (c[0] - cur[0], c[1] - cur[1], p[0] - cur[0], p[1] - cur[1]))
            prev_ctrl = c
            cur = p
        elif kind == "C":
            c1, c2, p = to_tenths(op[1]), to_tenths(op[2]), to_tenths(op[3])
            emit("c", (c1[0] - cur[0], c1[1] - cur[1], c2[0] - cur[0], c2[1] - cur[1], p[0] - cur[0], p[1] - cur[1]))
            cur = p
            prev_ctrl = None
        elif kind == "Z":
            emit("z", ())
            cur = start
            prev_ctrl = None

    return "".join(out)


class Font:
    """Shapes text with HarfBuzz and draws its glyphs with fontTools."""

    def __init__(self, path):
        self.tt = TTFont(path)
        self.upem = self.tt["head"].unitsPerEm
        self.face = hb.Face(hb.Blob.from_file_path(str(path)))
        self.glyph_order = self.tt.getGlyphOrder()

        # Glyphs of the chip labels, each defined once and placed with <use>:
        self.shared_glyphs = {}

    def shape(self, text, size, wght, opsz, tracking=0.0):
        """Returns the glyphs of the text as (name, x, y) and the width of the text."""
        font = hb.Font(self.face)
        font.set_variations({"wght": wght, "opsz": opsz})
        buf = hb.Buffer()
        buf.add_str(text)
        buf.guess_segment_properties()
        hb.shape(font, buf, {"kern": True, "liga": True})

        scale = size / self.upem
        glyphs, x = [], 0.0
        for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
            glyphs.append((self.glyph_order[info.codepoint], x + pos.x_offset * scale, pos.y_offset * scale))
            x += pos.x_advance * scale + tracking * size

        return glyphs, x - tracking * size

    def width(self, text, size, wght, opsz):
        return self.shape(text, size, wght, opsz)[1]

    def path(self, text, x, y, size, wght, opsz, tracking=0.0):
        """Returns the path data of the text, starting at the baseline point (x, y)."""
        glyphs, _ = self.shape(text, size, wght, opsz, tracking)
        glyph_set = self.tt.getGlyphSet(location={"wght": wght, "opsz": opsz})
        scale = size / self.upem
        pen = RecordingPen(glyph_set)
        for name, gx, gy in glyphs:
            glyph_set[name].draw(TransformPen(pen, (scale, 0, 0, -scale, x + gx, y - gy)))

        return compact_path(pen.ops)

    def uses(self, text, x, y, size, wght, opsz):
        """Returns one <use> element per glyph of the text, registering new glyphs in shared_glyphs."""
        glyphs, _ = self.shape(text, size, wght, opsz)
        glyph_set = self.tt.getGlyphSet(location={"wght": wght, "opsz": opsz})
        scale = size / self.upem
        elements = []
        for name, gx, gy in glyphs:
            key = (name, size, wght, opsz)
            if key not in self.shared_glyphs:
                pen = RecordingPen(glyph_set)
                glyph_set[name].draw(TransformPen(pen, (scale, 0, 0, -scale, 0, 0)))
                self.shared_glyphs[key] = (f"g{len(self.shared_glyphs)}", compact_path(pen.ops))

            glyph_id, d = self.shared_glyphs[key]

            # Spaces have no outline:
            if d:
                elements.append(f'<use href="#{glyph_id}" x="{num(x + gx)}" y="{num(y - gy)}"/>')

        return elements


class IconFont:
    """Draws the Lucide icons of the app from the font it ships, by their names on lucide.dev."""

    def __init__(self, font_path, codepoints_path):
        for path in (font_path, codepoints_path):
            if not path.exists():
                sys.exit(f"Could not find {path}.")

        # fontTools complains about a few spare bytes in the post table of the Lucide font, which
        # changes neither the outlines nor the names:
        logging.getLogger("fontTools").setLevel(logging.ERROR)

        self.tt = TTFont(font_path)
        self.upem = self.tt["head"].unitsPerEm
        self.ascent = self.tt["hhea"].ascent
        self.glyph_set = self.tt.getGlyphSet()
        self.cmap = self.tt.getBestCmap()
        self.codepoints = json.loads(codepoints_path.read_text(encoding="utf-8"))

    def check(self, names):
        """Stops the script when the font lacks one of the icons."""
        missing = sorted(name for name in names if self.codepoints.get(name) not in self.cmap)
        if missing:
            sys.exit(f"The Lucide font of the app has no icon named {', '.join(missing)}. See lucide.dev for the names.")

    def path(self, name, x, y, size):
        """
        Returns the path data of the icon with its top left corner at (x, y). Lucide draws every icon
        into the full em of the font, which is its 24 x 24 grid.
        """
        scale = size / self.upem
        pen = RecordingPen(self.glyph_set)
        self.glyph_set[self.cmap[self.codepoints[name]]].draw(TransformPen(pen, (scale, 0, 0, -scale, x, y + self.ascent * scale)))
        return compact_path(pen.ops)


def load_font():
    if not FONT_PATH.exists():
        sys.exit(f"Could not find {FONT_PATH}. Download it first, see README.md:\ncurl -L -o \"{FONT_PATH}\" \"{FONT_URL}\"")

    digest = hashlib.sha256(FONT_PATH.read_bytes()).hexdigest()
    if digest != FONT_SHA256:
        print(f"Warning: {FONT_PATH.name} is not the pinned version of Inter. The outlines may differ from the committed banner.", file=sys.stderr)

    return Font(FONT_PATH)


def build_banner(font, icons):
    lay = LAYOUT
    out = []
    a = out.append
    a(f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {WIDTH} {HEIGHT}" role="img" aria-label="MindWork AI Studio">')
    a('<!-- Text outlined from Inter (SIL Open Font License 1.1), icons outlined from Lucide (ISC License; parts from Feather, MIT License). -->')
    a('<title>MindWork AI Studio</title>')
    a('<defs>')
    a(f'<linearGradient id="bg" x1="0" y1="0" x2="{WIDTH}" y2="{HEIGHT}" gradientUnits="userSpaceOnUse">'
      f'<stop offset="0" stop-color="#0f3b2c"/><stop offset=".6" stop-color="#1f6b45"/><stop offset="1" stop-color="#4a9a55"/></linearGradient>')
    sun_x, sun_y, sun_r = lay["sun"]
    a(f'<radialGradient id="sun" cx="{sun_x}" cy="{sun_y}" r="{sun_r}" gradientUnits="userSpaceOnUse">'
      f'<stop offset="0" stop-color="#fdf0bf"/><stop offset=".14" stop-color="#f7d774"/>'
      f'<stop offset=".2" stop-color="#f7d774" stop-opacity=".7"/><stop offset=".45" stop-color="#f7d774" stop-opacity=".22"/>'
      f'<stop offset="1" stop-color="#f7d774" stop-opacity="0"/></radialGradient>')
    a('</defs>')
    a(f'<rect width="{WIDTH}" height="{HEIGHT}" rx="{CORNER_RADIUS}" fill="url(#bg)"/>')
    a(f'<rect width="{WIDTH}" height="{HEIGHT}" rx="{CORNER_RADIUS}" fill="url(#sun)"/>')

    # Wordmark on the left:
    left = lay["left"]
    a(f'<g id="wordmark" fill="{CREAM}">')
    a(f'<path id="eyebrow" opacity=".85" d="{font.path(EYEBROW, left + 4, lay["eyebrow_y"], lay["eyebrow_size"], tracking=0.28, **EYEBROW_STYLE)}"/>')
    a(f'<path id="name" d="{font.path(NAME, left, lay["name_y"], lay["name_size"], tracking=-0.02, **NAME_STYLE)}"/>')
    a(f'<path id="tagline" opacity=".9" d="{font.path(TAGLINE, left + 2, lay["tag_y"], lay["tag_size"], **TEXT_STYLE)}"/>')
    a('</g>')

    # Chips on the right, two rows of four:
    size, icon_size, pad, icon_gap = lay["chip_font"], lay["chip_icon"], lay["chip_pad"], lay["chip_icon_gap"]
    chip_h, gap, row_gap = lay["chip_h"], lay["chip_gap"], lay["chip_row_gap"]
    rows = [CHIPS[:4], CHIPS[4:]]
    content_widths = [[icon_size + icon_gap + font.width(label, size, **TEXT_STYLE) for (_, label, _) in row] for row in rows]
    columns = [max(content_widths[0][i], content_widths[1][i]) + 2 * pad for i in range(4)]
    x0 = WIDTH - lay["right"] - (sum(columns) + 3 * gap)
    top = lay["chip_cy"] - chip_h - row_gap / 2

    a(f'<g id="chips" fill="{CREAM}">')
    for r, row in enumerate(rows):
        y = top + r * (chip_h + row_gap)
        x = x0
        for c, (chip_id, label, icon) in enumerate(row):
            w = columns[c]
            a(f'<g id="chip-{chip_id}">')
            a(f'<rect x="{num(x)}" y="{num(y)}" width="{num(w)}" height="{chip_h}" rx="{chip_h / 2:g}" fill="{lay["chip_fill"]}" fill-opacity="{lay["chip_fill_op"]}" stroke="{CREAM}" stroke-opacity="{lay["chip_stroke_op"]}" stroke-width="2"/>')

            # Icon and label centred in the chip:
            content_x = x + (w - content_widths[r][c]) / 2
            a(f'<path d="{icons.path(icon, content_x, y + (chip_h - icon_size) / 2, icon_size)}"/>')

            # Centre the cap height of Inter (0.727 em) vertically:
            baseline = y + chip_h / 2 + 0.727 * size / 2
            out.extend(font.uses(label, content_x + icon_size + icon_gap, baseline, size, **TEXT_STYLE))
            a('</g>')
            x += w + gap

    a('</g>')
    a('</svg>')

    # The glyphs of the chip labels go into <defs>:
    i = out.index('</defs>')
    out[i:i] = [f'<path id="{glyph_id}" d="{d}"/>' for (glyph_id, d) in font.shared_glyphs.values() if d]
    return "\n".join(out)


def main():
    parser = argparse.ArgumentParser(description="Generates the banner of the start page and of the README.")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help=f"where to write the SVG file (default: {DEFAULT_OUTPUT})")
    args = parser.parse_args()

    icons = IconFont(LUCIDE_FONT_PATH, LUCIDE_CODEPOINTS_PATH)
    icons.check({icon for (_, _, icon) in CHIPS})
    svg = build_banner(load_font(), icons)
    args.output.write_text(svg, encoding="utf-8")
    print(f"Wrote {args.output} ({len(svg.encode())} bytes).")


if __name__ == "__main__":
    main()
