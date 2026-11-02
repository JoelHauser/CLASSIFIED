"""
Mod-page images: weapon cards drawn the way the debrief draws them, from the same models.

Each card is one weapon build on a unit's colours: the hologram (faint lit faces, outlines and
creases, a soft glow) over its turntable, its attachments named on leaders, and the header the
debrief shows (kind, name, full name, figures). Drawn at twice the size and scaled down, so the
lines come out clean.

    python scripts/make_showcase.py                 the default set, and the collage
    python scripts/make_showcase.py hk416_3:UsecOperations ak74m_2:BearCommand ...

Writes docs/images/weapons/<id>.png and docs/images/armoury.png. Needs numpy and pillow.
"""
import gzip
import math
import os
import re
import struct
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "src", "CLASSIFIED", "Resources")
OUT = os.path.join(ROOT, "docs", "images")
FONT = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "bahnschrift.ttf")

CARD = (1200, 675)
SS = 2  # supersampling

# The units' colours, as Theme.cs has them: ink, glow, primary, accent, text, dim, warn.
THEMES = {}
UNIT_NAMES = {
    "BearCommand": "BEAR  //  COMMAND POST", "BearReconnaissance": "BEAR  //  RECONNAISSANCE",
    "BearSignals": "BEAR  //  SIGNALS", "BearCounterintelligence": "BEAR  //  COUNTERINTELLIGENCE",
    "BearSupply": "BEAR  //  SUPPLY", "BearMedical": "BEAR  //  MEDICAL SERVICE",
    "UsecOperations": "USEC  //  TACTICAL OPERATIONS CENTER", "UsecIntelligence": "USEC  //  S2 INTELLIGENCE",
    "UsecSignals": "USEC  //  S6 COMMUNICATIONS", "UsecLogistics": "USEC  //  S4 LOGISTICS",
    "UsecMedical": "USEC  //  MEDICAL // CASEVAC", "UsecCommand": "USEC  //  CORPORATE COMMAND",
}

# The callouts the debrief shows, in its order of priority.
ORDER = ["mod_scope", "mod_muzzle", "mod_magazine", "mod_stock", "mod_tactical", "mod_foregrip",
         "mod_sight_rear", "mod_reciever", "mod_gas_block", "mod_handguard", "mod_pistol_grip", "mod_pistolgrip",
         "mod_barrel", "mod_bipod"]


def hexcol(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def load_themes():
    src = open(os.path.join(ROOT, "src", "CLASSIFIED", "Theme.cs"), encoding="utf-8").read()
    for m in re.finditer(r'new Theme\(ThemeChoice\.(\w+), Faction\.\w+, "(\w+)", "(\w+)", "(\w+)", "(\w+)", "(\w+)", "(\w+)", "(\w+)"', src):
        name, ink, glow, primary, accent, text, dim, warn = m.groups()
        THEMES[name] = {k: hexcol(v) for k, v in zip(("ink", "glow", "primary", "accent", "text", "dim", "warn"),
                                                       (ink, glow, primary, accent, text, dim, warn))}


def load_specs():
    src = open(os.path.join(ROOT, "src", "CLASSIFIED", "Kit.Data.cs"), encoding="utf-8").read()
    specs = {}
    for m in re.finditer(r'new Spec\(((?:"(?:[^"\\]|\\.)*",?\s*){10})\)', src):
        fields = re.findall(r'"((?:[^"\\]|\\.)*)"', m.group(1))
        specs[fields[0]] = {"kind": fields[2], "short": fields[4], "name": fields[5], "line": fields[8]}
    return specs


def load_model(iid):
    b = gzip.open(os.path.join(RES, f"item_{iid}.bin")).read()
    assert b[:4] == b"CLW2"
    nv, nf, na = struct.unpack_from("<3i", b, 4)
    o = 16
    v = np.frombuffer(b, np.int16, nv * 3, o).reshape(-1, 3) / 32767.0
    o += nv * 6
    f = np.frombuffer(b, np.uint16, nf * 3, o).reshape(-1, 3).astype(int)
    o += nf * 6
    anchors = []
    for _ in range(na):
        p = np.array(struct.unpack_from("<3h", b, o)) / 32767.0
        o += 6
        n = b[o]
        o += 1
        fields = b[o:o + n].decode("utf-8").split("|")
        o += n
        anchors.append((fields[0], fields[1] if len(fields) > 1 else "", p))
    return v, f, anchors


def edges_of(v, f, crease_deg=32.0):
    """Face normals, and each edge with the faces either side and whether it is a crease."""
    tri = v[f]
    n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    n /= np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
    owners = {}
    for fi, (a, b, c) in enumerate(f):
        for x, y in ((a, b), (b, c), (c, a)):
            owners.setdefault((min(x, y), max(x, y)), []).append(fi)
    cos = math.cos(math.radians(crease_deg))
    edges = []
    for (a, b), fs in owners.items():
        f0 = fs[0]
        f1 = fs[1] if len(fs) > 1 else -1
        crease = f1 < 0 or len(fs) > 2 or float(n[f0] @ n[f1]) < cos
        edges.append((a, b, f0, f1, crease))
    return n, edges


def font(size):
    return ImageFont.truetype(FONT, int(size * SS))


def card(iid, unit, specs, yaw=0.42, pitch=0.30):
    t = THEMES[unit]
    W, H = CARD[0] * SS, CARD[1] * SS
    ink, accent, text, dim = t["ink"], t["accent"], t["text"], t["dim"]

    # Ground: the unit's ink, a glow behind the item, a faint grid.
    bg = Image.new("RGB", (W, H), ink)
    glow = Image.new("RGB", (W, H), (0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse([W * 0.18, H * 0.18, W * 0.82, H * 0.95], fill=tuple(int(c * 0.55) for c in t["glow"]))
    glow = glow.filter(ImageFilter.GaussianBlur(160 * SS / 2))
    bg = Image.fromarray(np.clip(np.asarray(bg, np.int32) + np.asarray(glow, np.int32), 0, 255).astype(np.uint8))
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for x in range(0, W, 60 * SS):
        d.line([(x, 0), (x, H)], fill=accent + (14,), width=SS)
    for y in range(0, H, 60 * SS):
        d.line([(0, y), (W, y)], fill=accent + (14,), width=SS)
    bg = Image.alpha_composite(bg.convert("RGBA"), layer)

    # The model, turned and tilted as the debrief shows it.
    v, f, anchors = load_model(iid)
    n, edges = edges_of(v, f)
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    M = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]) @ np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    pv = v @ M.T
    pn = n @ M.T
    # Fitted to the space under the header and between the callout rows, whatever its shape (a
    # rifle is long, a pistol tall), centred on its own outline.
    lo, hi = pv[:, :2].min(axis=0), pv[:, :2].max(axis=0)
    scale = min(W * 0.62 / max(hi[0] - lo[0], 1e-6), H * 0.40 / max(hi[1] - lo[1], 1e-6))
    mid = (lo + hi) / 2
    centre = (W * 0.52 - mid[0] * scale, H * 0.62 + mid[1] * scale)

    def P(p):
        return (centre[0] + p[0] * scale, centre[1] - p[1] * scale)

    # The turntable: a ring of ticks under the item.
    table = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(table)
    floor = float(pv[:, 1].min()) - 0.05
    ring = []
    for k in range(73):
        a = k * math.pi * 2 / 72
        q = np.array([math.cos(a) * 1.05, floor, math.sin(a) * 1.05]) @ np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]).T
        ring.append(P(q))
    td.line(ring, fill=accent + (70,), width=SS)
    for k in range(0, 72, 2):
        a = k * math.pi * 2 / 72
        inner = np.array([math.cos(a) * 0.98, floor, math.sin(a) * 0.98]) @ np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]).T
        outer = np.array([math.cos(a) * 1.05, floor, math.sin(a) * 1.05]) @ np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]).T
        td.line([P(inner), P(outer)], fill=accent + (90 if k % 6 == 0 else 45,), width=SS)
    bg = Image.alpha_composite(bg, table)

    # Faces: faint, lit from above and in front; then outlines and creases over them, with a glow.
    faces = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(faces)
    light = np.array([0.25, 0.75, 0.6])
    light /= np.linalg.norm(light)
    depth = pv[f].mean(axis=1)[:, 2]
    for i in np.argsort(depth):
        z = pn[i][2]
        if z <= 0:
            continue
        lit = max(0.0, float(pn[i] @ light))
        fd.polygon([P(pv[j]) for j in f[i]], fill=accent + (int(14 + 40 * lit),))
    lines = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lines)
    front = pn[:, 2] > 0
    for a, b, f0, f1, crease in edges:
        outline = f1 < 0 or front[f0] != front[f1]
        seen = front[f0] or (f1 >= 0 and front[f1])
        if not (outline or (crease and seen)):
            continue
        ld.line([P(pv[a]), P(pv[b])], fill=accent + (235 if outline else 130,), width=int((1.6 if outline else 1.1) * SS))
    blur = lines.filter(ImageFilter.GaussianBlur(5 * SS))
    bg = Image.alpha_composite(bg, faces)
    bg = Image.alpha_composite(bg, blur)
    bg = Image.alpha_composite(bg, blur)
    bg = Image.alpha_composite(bg, lines)

    # Callouts: the named parts, up to five, in a row above (or below) the item, never overlapping.
    over = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    od = ImageDraw.Draw(over)
    label_font = font(19)
    picked = []
    for slot in ORDER:
        # A stock is named by its outermost piece (the stock on its buffer tube, mod_stock_000), as the debrief does.
        hits = [(s, nm, p) for s, nm, p in anchors if (s == slot or s.startswith(slot + "_")) and nm]
        hit = (hits[-1] if slot == "mod_stock" else hits[0]) if hits else None
        if hit and hit[1] and len(picked) < 5:
            picked.append((hit[1].upper()[:18], P(hit[2] @ M.T), hit[2][1] < -0.08))
    for below in (False, True):
        row = sorted([c for c in picked if c[2] == below], key=lambda c: c[1][0])
        if not row:
            continue
        y = H * (0.875 if below else 0.36)
        widths = [od.textlength(c[0], font=label_font) + 18 * SS for c in row]
        xs = [c[1][0] for c in row]
        gap = 24 * SS
        for i in range(1, len(row)):
            xs[i] = max(xs[i], xs[i - 1] + (widths[i - 1] + widths[i]) / 2 + gap)
        for i in range(len(row) - 1, -1, -1):
            right = W - 40 * SS - widths[i] / 2 if i == len(row) - 1 else xs[i + 1] - (widths[i] + widths[i + 1]) / 2 - gap
            xs[i] = min(xs[i], right)
        for i in range(len(row)):
            xs[i] = max(xs[i], 40 * SS + widths[i] / 2)
        for (name, at, _), x, w in zip(row, xs, widths):
            ty = y - 14 * SS if not below else y + 14 * SS
            od.line([at, (at[0], ty + (10 if not below else -10) * SS), (x, ty + (10 if not below else -10) * SS)], fill=text + (150,), width=SS)
            od.ellipse([at[0] - 4 * SS, at[1] - 4 * SS, at[0] + 4 * SS, at[1] + 4 * SS], fill=text + (230,))
            od.line([(x - w / 2, ty + (10 if not below else -10) * SS), (x + w / 2, ty + (10 if not below else -10) * SS)], fill=accent + (200,), width=2 * SS)
            od.text((x, ty - (2 if not below else -22) * SS), name, font=label_font, fill=text + (240,), anchor="ms")
    bg = Image.alpha_composite(bg, over)

    # The header the debrief shows, and the unit's tag.
    spec = specs.get(re.sub(r"_\d$", "", iid), {})
    hd = ImageDraw.Draw(bg)
    x0, y0 = 48 * SS, 44 * SS
    hd.text((x0, y0), f"{spec.get('kind', 'WEAPON SYSTEM')}  //  RESTRICTED", font=font(17), fill=dim + (255,))
    hd.text((x0, y0 + 26 * SS), spec.get("short", iid).upper(), font=font(46), fill=text + (255,))
    hd.text((x0, y0 + 84 * SS), spec.get("name", "").upper(), font=font(18), fill=accent + (255,))
    hd.text((x0, y0 + 112 * SS), spec.get("line", ""), font=font(16), fill=dim + (255,))
    hd.line([(x0, y0 + 142 * SS), (x0 + 90 * SS, y0 + 142 * SS)], fill=accent + (255,), width=3 * SS)
    tag = UNIT_NAMES.get(unit, unit)
    tf = font(16)
    hd.text((W - 48 * SS, H - 40 * SS), tag, font=tf, fill=dim + (255,), anchor="rs")
    # A thin frame with heavier corners, as the terminal's panels have.
    for cx, cy2, sx, sy2 in ((24, 24, 1, 1), (CARD[0] - 24, 24, -1, 1), (24, CARD[1] - 24, 1, -1), (CARD[0] - 24, CARD[1] - 24, -1, -1)):
        px, py = cx * SS, cy2 * SS
        hd.line([(px, py), (px + sx * 28 * SS, py)], fill=accent + (220,), width=3 * SS)
        hd.line([(px, py), (px, py + sy2 * 28 * SS)], fill=accent + (220,), width=3 * SS)
    return bg.convert("RGB").resize(CARD, Image.LANCZOS)


DEFAULT = [
    ("hk416", "UsecOperations"), ("ak74m", "BearCommand"), ("mk16", "UsecCommand"),
    ("val", "BearCounterintelligence"), ("mpx", "UsecSignals"), ("svds", "BearReconnaissance"),
    ("m60", "UsecLogistics"), ("pkm", "BearSupply"), ("deagle", "UsecIntelligence"),
]


def best_build(weapon):
    """The weapon's build with the most named parts on show, an optic counting double."""
    best, score = f"{weapon}_1", -1
    for k in range(1, 8):
        path = os.path.join(RES, f"item_{weapon}_{k}.bin")
        if not os.path.exists(path):
            continue
        anchors = load_model(f"{weapon}_{k}")[2]
        named = {s.split("_0")[0] for s, nm, _ in anchors if nm and any(s == o or s.startswith(o + "_") for o in ORDER)}
        value = len(named) + (1 if "mod_scope" in named else 0)
        if value > score:
            best, score = f"{weapon}_{k}", value
    return best


def bare(iid, colour, width=1600, yaw=0.42, pitch=0.30):
    """
    Just the weapon: its wireframe in one colour (outlines, creases, a faint lit fill and a soft
    glow) on a transparent ground, no text, no table, cropped close.
    """
    W = width * SS
    H = int(W * 0.6)
    v, f, _ = load_model(iid)
    n, edges = edges_of(v, f)
    cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
    M = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]) @ np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    pv = v @ M.T
    pn = n @ M.T
    lo, hi = pv[:, :2].min(axis=0), pv[:, :2].max(axis=0)
    scale = min(W * 0.9 / max(hi[0] - lo[0], 1e-6), H * 0.9 / max(hi[1] - lo[1], 1e-6))
    mid = (lo + hi) / 2

    def P(p):
        return (W / 2 + (p[0] - mid[0]) * scale, H / 2 - (p[1] - mid[1]) * scale)

    faces = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(faces)
    light = np.array([0.25, 0.75, 0.6])
    light /= np.linalg.norm(light)
    for i in np.argsort(pv[f].mean(axis=1)[:, 2]):
        if pn[i][2] <= 0:
            continue
        lit = max(0.0, float(pn[i] @ light))
        fd.polygon([P(pv[j]) for j in f[i]], fill=colour + (int(16 + 44 * lit),))
    lines = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lines)
    front = pn[:, 2] > 0
    thick = W / (1600 * SS)
    for a, b, f0, f1, crease in edges:
        outline = f1 < 0 or front[f0] != front[f1]
        seen = front[f0] or (f1 >= 0 and front[f1])
        if not (outline or (crease and seen)):
            continue
        ld.line([P(pv[a]), P(pv[b])], fill=colour + (255 if outline else 175,), width=max(1, int((2.4 if outline else 1.5) * SS * thick)))
    # A tight glow, half strength: it lifts the lines on a dark page without blurring them on a light one.
    glow = lines.filter(ImageFilter.GaussianBlur(3 * SS * thick))
    glow.putalpha(glow.getchannel("A").point(lambda a: a // 2))
    img = Image.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, 0)), glow)
    img = Image.alpha_composite(img, faces)
    img = Image.alpha_composite(img, lines)
    # Cropped to what is drawn, with a margin for the glow, then down to size.
    box = img.getchannel("A").point(lambda a: 255 if a > 6 else 0).getbbox()
    if box:
        pad = int(24 * SS * thick)
        img = img.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(W, box[2] + pad), min(H, box[3] + pad)))
    return img.resize((img.width // SS, img.height // SS), Image.LANCZOS)


def main():
    load_themes()
    specs = load_specs()
    args = sys.argv[1:]
    if args and args[0] == "--bare":
        # python scripts/make_showcase.py --bare [hk416[:Unit] ...]: transparent wireframes only.
        pairs = [tuple(a.split(":")) if ":" in a else (a, "UsecOperations") for a in args[1:]] or DEFAULT
        os.makedirs(os.path.join(OUT, "wireframes"), exist_ok=True)
        for iid, unit in pairs:
            iid = iid if re.search(r"_\d$", iid) else best_build(iid)
            img = bare(iid, THEMES[unit]["accent"])
            path = os.path.join(OUT, "wireframes", f"{iid}.png")
            img.save(path, optimize=True)
            print("wrote", path, img.size)
        return
    pairs = [tuple(a.split(":")) for a in args] or DEFAULT
    # A bare weapon id ("hk416") takes its best-equipped build.
    pairs = [(iid if re.search(r"_\d$", iid) else best_build(iid), unit) for iid, unit in pairs]
    os.makedirs(os.path.join(OUT, "weapons"), exist_ok=True)
    cards = []
    for iid, unit in pairs:
        img = card(iid, unit, specs)
        path = os.path.join(OUT, "weapons", f"{iid}.png")
        img.save(path, optimize=True)
        cards.append(img)
        print("wrote", path)
    if len(cards) >= 2:
        cols = 3 if len(cards) >= 6 else 2
        rows = (len(cards) + cols - 1) // cols
        w, h = CARD[0] // 2, CARD[1] // 2
        sheet = Image.new("RGB", (cols * w + (cols + 1) * 8, rows * h + (rows + 1) * 8), (8, 8, 10))
        for k, img in enumerate(cards):
            sheet.paste(img.resize((w, h), Image.LANCZOS), (8 + (k % cols) * (w + 8), 8 + (k // cols) * (h + 8)))
        sheet.save(os.path.join(OUT, "armoury.png"), optimize=True)
        print("wrote", os.path.join(OUT, "armoury.png"))


if __name__ == "__main__":
    main()
