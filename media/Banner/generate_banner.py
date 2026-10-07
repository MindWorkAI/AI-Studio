#!/usr/bin/env python3
"""
Generates the banner of the start page and of the README:
app/MindWork AI Studio/wwwroot/svg/banner.svg

The app shows the banner through an <img> element, where neither the app's CSS nor its web fonts
apply. Therefore, every text is converted into outlines, so the banner looks the same on Windows,
macOS, Linux, and GitHub. The text uses Inter (SIL Open Font License 1.1); the icons are the
Material Icons the app itself uses, read from the MudBlazor package (Apache License 2.0).

See README.md in this folder for the setup.
"""

import argparse
import hashlib
import os
import re
import sys
from pathlib import Path

import dnfile
import uharfbuzz as hb
from fontTools.pens.basePen import BasePen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

HERE = Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
APP_PROJECT = REPO_ROOT / "app" / "MindWork AI Studio" / "MindWork AI Studio.csproj"
DEFAULT_OUTPUT = REPO_ROOT / "app" / "MindWork AI Studio" / "wwwroot" / "svg" / "banner.svg"

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
# length in a column. Each entry: (id, label, name of the icon in Icons.Material.Filled).
CHIPS = [
    ("chat", "Chat", "Chat"),
    ("assistants", "Assistants", "Apps"),
    ("builder", "No-code builder", "AutoMode"),
    ("tools", "Agentic tools", "Build"),
    ("research", "Research", "Language"),
    ("transcribe", "Transcribe", "Mic"),
    ("documents", "Your documents", "Description"),
    ("emails", "Your e-mails", "Email"),
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


def read_project_value(pattern, what):
    match = re.search(pattern, APP_PROJECT.read_text(encoding="utf-8-sig"))
    if match is None:
        sys.exit(f"Could not find {what} in {APP_PROJECT}.")

    return match.group(1)


def load_material_icons(names):
    """
    Reads the SVG paths of Icons.Material.Filled.<name> from the MudBlazor assembly in the NuGet
    cache, in the version and for the framework the app project uses.
    """
    version = read_project_value(r'<PackageReference\s+Include="MudBlazor"\s+Version="([^"]+)"', "the MudBlazor version")
    framework = read_project_value(r"<TargetFramework>([^<]+)</TargetFramework>", "the target framework")
    packages = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget" / "packages"))
    assembly = packages / "mudblazor" / version / "lib" / framework / "MudBlazor.dll"
    if not assembly.exists():
        sys.exit(f"Could not find {assembly}. Build the app once, so NuGet restores MudBlazor {version}.")

    tables = dnfile.dnPE(str(assembly)).net.mdtables
    types = tables.TypeDef.rows

    # Which type declares which field, and which type encloses which nested type:
    field_owner = {}
    for index, type_def in enumerate(types, start=1):
        for field in type_def.FieldList:
            field_owner[field.row_index] = index

    enclosing = {n.NestedClass.row_index: str(n.EnclosingClass.row.TypeName) for n in tables.NestedClass.rows}

    # The icons are string constants of the nested class Icons.Material.Filled:
    icons = {}
    for constant in tables.Constant.rows:
        parent = constant.Parent
        if parent.table.name != "Field":
            continue

        owner = field_owner.get(parent.row_index)
        if owner is None or str(types[owner - 1].TypeName) != "Filled" or enclosing.get(owner) != "Material":
            continue

        name = str(tables.Field.rows[parent.row_index - 1].Name)
        if name not in names:
            continue

        value = constant.Value
        raw = value.value if hasattr(value, "value") else value
        svg = raw.decode("utf-16le") if isinstance(raw, (bytes, bytearray)) else str(raw)

        # Skip the invisible 24 x 24 frames some icons carry:
        tags = re.findall(r"<path\b[^>]*>", svg)
        icons[name] = [re.search(r'\bd="([^"]+)"', tag).group(1) for tag in tags if 'fill="none"' not in tag]

    missing = sorted(set(names) - icons.keys())
    if missing:
        sys.exit(f"Icons.Material.Filled has no icon named {', '.join(missing)} in MudBlazor {version}.")

    return icons


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
    a('<!-- Text outlined from Inter (SIL Open Font License 1.1), icons from Material Icons via MudBlazor (Apache License 2.0). -->')
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
            for d in icons[icon]:
                a(f'<path transform="translate({num(content_x)} {num(y + (chip_h - icon_size) / 2)}) scale({icon_size / 24:g})" d="{d}"/>')

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

    svg = build_banner(load_font(), load_material_icons({icon for (_, _, icon) in CHIPS}))
    args.output.write_text(svg, encoding="utf-8")
    print(f"Wrote {args.output} ({len(svg.encode())} bytes).")


if __name__ == "__main__":
    main()
