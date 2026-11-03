"""
Shared pieces for the mod-page art (make_title.py, make_units.py): the font, the units' colours
from Theme.cs, and the faction insignia assembled from the game's own pieces.
"""
import os
import re

import numpy as np
from PIL import Image, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "src", "CLASSIFIED")
RES = os.path.join(SRC, "Resources")
FONT = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "bahnschrift.ttf")


def font(size, weight=None, path=FONT):
    f = ImageFont.truetype(path, size)
    if weight:
        try:
            f.set_variation_by_name(weight)
        except (OSError, ValueError):
            pass
    return f


def hexcol(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def load_themes():
    """Every unit's colours, from Theme.cs."""
    src = open(os.path.join(SRC, "Theme.cs"), encoding="utf-8").read()
    themes = {}
    pat = r'new Theme\(ThemeChoice\.(\w+), Faction\.\w+, "(\w+)", "(\w+)", "(\w+)", "(\w+)", "(\w+)", "(\w+)"'
    for m in re.finditer(pat, src):
        themes[m.group(1)] = {k: hexcol(v) for k, v in zip(("ink", "glow", "primary", "accent", "text", "dim"), m.groups()[1:])}
    return themes


def insignia(faction, radius):
    """The faction's insignia (bear or usec) assembled from the game's own pieces, as a 0..1 mask, cropped close."""
    field = "BearPieces" if faction == "bear" else "UsecPieces"
    src = open(os.path.join(SRC, "Insignia.Pieces.cs"), encoding="utf-8").read()
    block = src[src.index(field):]
    block = block[:block.index("};")]
    num = r"(-?[\d.]+)f"
    pat = (r'new Piece\("(\w+)", new Rect\(' + ", ".join([num] * 4) + r"\), new Vector2\(" + num + ", " + num
           + r"\), new Vector2\(" + num + ", " + num + r"\)")
    atlas = Image.open(os.path.join(RES, f"insignia_{faction}.png")).convert("RGBA")
    aw, ah = atlas.size
    alpha = atlas.split()[3]
    size = int(radius * 2.2)
    out = Image.new("L", (size, size), 0)
    for m in re.finditer(pat, block):
        u, v, uw, vh, cx, cy, sx, sy = (float(g) for g in m.groups()[1:])
        box = (round(u * aw), round((1 - v - vh) * ah), round((u + uw) * aw), round((1 - v) * ah))
        w, h = max(1, round(sx * radius)), max(1, round(sy * radius))
        a = alpha.crop(box).resize((w, h), Image.LANCZOS)
        out.paste(Image.new("L", (w, h), 255), (round(size / 2 + cx * radius - w / 2), round(size / 2 - cy * radius - h / 2)), a)
    a = np.asarray(out).astype(np.float32) / 255.0
    ys, xs = np.where(a > 0.05)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def to_img(rgb):
    return Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8))
