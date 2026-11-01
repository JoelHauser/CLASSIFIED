"""
Builds the insignia textures from the faction logos in art/.

Each logo is split into the pieces that fly together when the terminal comes on (whole
connected shapes only, so no seam can show once they meet), re-antialiased, and packed into
one atlas per faction: white, with the shape in alpha, tinted where it is drawn. The piece
table (where each piece sits in the atlas and in the logo, and the way it flies in from) is
written to src/CLASSIFIED/Insignia.Pieces.cs.

    python -I scripts/make_insignia.py        (needs numpy, scipy, pillow)

Also writes art/preview.png: every faction's pieces, tinted and assembled, to check by eye.
"""

import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "art")
OUT = os.path.join(ROOT, "src", "CLASSIFIED", "Resources")
TABLE = os.path.join(ROOT, "src", "CLASSIFIED", "Insignia.Pieces.cs")

# Pixels of clear space round each piece in the atlas, so the smaller mipmaps of one piece
# never pick up a neighbour.
PAD = 24

# Which piece each of a logo's connected shapes belongs to, from its centre and box
# (y0, y1, x0, x1) in the source image's pixels.
def bear_piece(cx, cy, box):
    y0, y1, x0, x1 = box
    if y1 - y0 > 400:
        return "ShieldLeft" if cx < 389 else "ShieldRight"
    if y0 >= 700:
        if x1 - x0 < 120:
            return "Fang"
        return "ShieldLeft" if cx < 389 else "ShieldRight"
    if y1 <= 110:
        return "Pin"
    if y0 >= 510:
        return "Name"
    if y0 >= 370:
        return "Mountains"
    return "Paw"


def usec_piece(cx, cy, box):
    y0, y1, x0, x1 = box
    if y1 <= 300:
        return "Eagle"
    if y0 >= 300 and y1 <= 560:
        return "Letter" + "USEC"[min(3, int(cx / 180))]
    if x1 - x0 > 600:
        return "Chevron"
    return "Bars"


FACTIONS = [
    # Name, source, the pieces in the order they arrive with the way each flies in from
    # (x, y with y up), and the classifier above.
    ("Bear", "bear.png", [
        ("ShieldLeft", (-1.0, 0.1)),
        ("ShieldRight", (1.0, 0.1)),
        ("Paw", (0.25, 1.0)),
        ("Mountains", (-0.25, -1.0)),
        ("Name", (0.0, -1.0)),
        ("Pin", (0.0, 1.0)),
        ("Fang", (0.0, -1.0)),
    ], bear_piece),
    ("Usec", "usec.png", [
        ("Eagle", (0.0, 1.0)),
        ("LetterU", (-1.0, 0.0)),
        ("LetterS", (-1.0, 0.0)),
        ("LetterE", (1.0, 0.0)),
        ("LetterC", (1.0, 0.0)),
        ("Chevron", (0.0, -1.0)),
        ("Bars", (0.0, -1.0)),
    ], usec_piece),
]


def reantialias(alpha, factor=8):
    """
    The source logos are antialiased in 17 alpha steps. Upsampled smoothly, cut at half and
    averaged back down, the edges come out with a full ramp, so a large logo shows no steps.
    """
    h, w = alpha.shape
    img = Image.fromarray((alpha * 255).astype(np.uint8))
    big = np.asarray(img.resize((w * factor, h * factor), Image.LANCZOS)).astype(np.float32) / 255.0
    solid = (big >= 0.5).astype(np.float32)
    return solid.reshape(h, factor, w, factor).mean(axis=(1, 3))


def split(alpha, classify, names):
    """Each pixel's piece: the piece of the nearest solid shape, so the soft edge goes with its shape."""
    solid = alpha >= 0.5
    labels, count = ndimage.label(solid)
    owner = np.zeros(count + 1, dtype=np.int32)
    for i, sl in enumerate(ndimage.find_objects(labels), 1):
        cy, cx = ndimage.center_of_mass(labels == i)
        box = (sl[0].start, sl[0].stop, sl[1].start, sl[1].stop)
        name = classify(cx, cy, box)
        owner[i] = names.index(name)
    _, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
    nearest = labels[iy, ix]
    return owner[nearest], alpha


def pack(rects, width):
    """Shelf packing, tallest first. Returns positions and the atlas height."""
    order = sorted(range(len(rects)), key=lambda i: -rects[i][1])
    pos = [None] * len(rects)
    x = y = shelf = 0
    for i in order:
        w, h = rects[i]
        if x + w > width:
            x = 0
            y += shelf
            shelf = 0
        pos[i] = (x, y)
        x += w
        shelf = max(shelf, h)
    return pos, y + shelf


def build(name, source, pieces, classify, preview):
    names = [p[0] for p in pieces]
    rgba = np.asarray(Image.open(os.path.join(ART, source)).convert("RGBA")).astype(np.float32) / 255.0
    alpha = reantialias(rgba[:, :, 3])
    piece_of, alpha = split(alpha, classify, names)
    h, w = alpha.shape

    # Centred on the logo's box; sized so its farthest corner touches the unit circle, so it
    # sits inside every frame whatever its shape.
    ys, xs = np.nonzero(alpha >= 0.5)
    cx = (xs.min() + xs.max() + 1) / 2.0
    cy = (ys.min() + ys.max() + 1) / 2.0
    extent = math.sqrt(((xs + 0.5 - cx) ** 2 + (ys + 0.5 - cy) ** 2).max())

    crops = []
    for i in range(len(names)):
        mask = np.where(piece_of == i, alpha, 0.0)
        py, px = np.nonzero(mask > 0.0)
        if len(px) == 0:
            raise SystemExit(f"{name}: piece {names[i]} is empty")
        x0, x1, y0, y1 = px.min(), px.max() + 1, py.min(), py.max() + 1
        crops.append((x0, y0, mask[y0:y1, x0:x1]))

    sizes = [(c[2].shape[1] + 2 * PAD, c[2].shape[0] + 2 * PAD) for c in crops]
    # The narrowest of a few widths that wastes least.
    widest = max(s[0] for s in sizes)
    best = None
    for atlas_w in range(max(512, (widest + 63) // 64 * 64), 2049, 64):
        pos, atlas_h = pack(sizes, atlas_w)
        if best is None or atlas_w * atlas_h < best[0] * best[1]:
            best = (atlas_w, atlas_h, pos)
    atlas_w, atlas_h, pos = best
    atlas = np.zeros((atlas_h, atlas_w), dtype=np.float32)
    entries = []
    for i, (x0, y0, mask) in enumerate(crops):
        ax, ay = pos[i][0] + PAD, pos[i][1] + PAD
        ph, pw = mask.shape
        atlas[ay:ay + ph, ax:ax + pw] = mask
        # Atlas rect in UV space (Unity's v runs up); centre and size in the logo in units of the radius, y up.
        u0, v0 = ax / atlas_w, 1.0 - (ay + ph) / atlas_h
        lx = (x0 + pw / 2.0 - cx) / extent
        ly = -(y0 + ph / 2.0 - cy) / extent
        entries.append((names[i], (u0, v0, pw / atlas_w, ph / atlas_h), (lx, ly, pw / extent, ph / extent), pieces[i][1]))

    out = np.zeros((atlas_h, atlas_w, 4), dtype=np.uint8)
    out[:, :, :3] = 255
    out[:, :, 3] = np.round(np.clip(atlas, 0, 1) * 255).astype(np.uint8)
    path = os.path.join(OUT, f"insignia_{name.lower()}.png")
    Image.fromarray(out, "RGBA").save(path, optimize=True)
    print(f"{name}: {len(names)} pieces, atlas {atlas_w}x{atlas_h}, {os.path.getsize(path) // 1024} KB, logo {w}x{h}, extent {extent:.0f}px")
    preview.append((name, entries, out))
    return entries


def cs_float(v):
    return f"{v:.5f}f"


def write_table(all_entries):
    lines = [
        "// Generated by scripts/make_insignia.py from the logos in art/. Do not edit by hand: run the script.",
        "using UnityEngine;",
        "",
        "namespace CLASSIFIED",
        "{",
        "    internal static partial class Insignia",
        "    {",
    ]
    for name, entries in all_entries:
        lines.append(f"        private static readonly Piece[] {name}Pieces =")
        lines.append("        {")
        for piece, uv, logo, direction in entries:
            d = np.array(direction, dtype=np.float64)
            d = d / np.linalg.norm(d)
            lines.append(
                f"            new Piece(\"{piece}\", new Rect({', '.join(cs_float(v) for v in uv)}), "
                f"new Vector2({cs_float(logo[0])}, {cs_float(logo[1])}), new Vector2({cs_float(logo[2])}, {cs_float(logo[3])}), "
                f"new Vector2({cs_float(d[0])}, {cs_float(d[1])})),")
        lines.append("        };")
        lines.append("")
    lines[-1:] = ["    }", "}", ""]
    with open(TABLE, "w", newline="\n", encoding="utf-8") as f:
        f.write("\n".join(lines))


def write_preview(previews, colours):
    size = 520
    sheet = Image.new("RGB", (size * len(previews), size), (0, 0, 0))
    for k, (name, entries, atlas) in enumerate(previews):
        ink, main = colours[name]
        tile = Image.new("RGBA", (size, size), ink + (255,))
        r = size * 0.45
        ah, aw = atlas.shape[:2]
        src = Image.fromarray(atlas, "RGBA")
        for piece, uv, logo, _ in entries:
            u0, v0, uw, vh = uv
            box = (round(u0 * aw), round((1 - v0 - vh) * ah), round((u0 + uw) * aw), round((1 - v0) * ah))
            crop = src.crop(box)
            pw, ph = max(1, round(logo[2] * r)), max(1, round(logo[3] * r))
            crop = crop.resize((pw, ph), Image.LANCZOS)
            colour = Image.new("RGBA", crop.size, main + (255,))
            colour.putalpha(crop.split()[3])
            x = round(size / 2 + logo[0] * r - pw / 2)
            y = round(size / 2 - logo[1] * r - ph / 2)
            tile.alpha_composite(colour, (x, y))
        ImageDraw.Draw(tile).ellipse((size / 2 - r, size / 2 - r, size / 2 + r, size / 2 + r), outline=main + (90,))
        sheet.paste(tile.convert("RGB"), (k * size, 0))
    sheet.save(os.path.join(ART, "preview.png"))


def main():
    previews = []
    all_entries = []
    for name, source, pieces, classify in FACTIONS:
        all_entries.append((name, build(name, source, pieces, classify, previews)))
    write_table(all_entries)
    write_preview(previews, {
        "Bear": ((10, 3, 3), (196, 38, 42)),
        "Usec": ((3, 6, 12), (46, 134, 222)),
    })
    print("Wrote", os.path.relpath(TABLE, ROOT), "and art/preview.png")


if __name__ == "__main__":
    sys.exit(main())
