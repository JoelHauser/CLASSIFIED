"""
The mod page's animated hero: a title sequence made of CLASSIFIED's own pieces.

The tactical net comes up between its classification banners. Both factions' insignia fly together
from the game's own pieces into their frames, while the debrief table between them builds a weapon
hologram from the game's own models and turns it: BEAR's AK first, in BEAR's red, until the signal
flickers and USEC's HK comes through in blue. The name is declassified letter by letter as the
redaction bars come off it, and the signal tears away to the empty net, where the loop starts again.

    python -I scripts/make_title.py            docs/images/hero-v4.webp
    python -I scripts/make_title.py --still    a few frames as PNGs in the system temp folder, to check
    python -I scripts/make_title.py --thumb    docs/images/thumbnail.png: the torn-signal square for the listing

Needs numpy and pillow; reads the models, themes and insignia the game uses.
"""
import math
import os
import re
import sys
import tempfile

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import art_kit as kit  # noqa: E402
import make_showcase as show  # noqa: E402

OUT = os.path.join(kit.ROOT, "docs", "images", "hero-v4.webp")
THUMB = os.path.join(kit.ROOT, "docs", "images", "thumbnail.png")
FPS = 20
LENGTH = 8.0

INK = (5, 7, 10)
LABEL = (150, 160, 176)
QUIET = (84, 92, 106)
TEXT = (232, 236, 242)
BEAR = (226, 58, 44)
USEC = (82, 160, 255)
BEAR_HOLO = (236, 104, 84)
USEC_HOLO = (120, 180, 255)

# The weapon each side puts on the table: model, name, and the line under it.
WEAPONS = [("ak74m_5", "АК-74М", "BEAR  //  ОРУЖИЕ 03", BEAR_HOLO), ("hk416_3", "HK 416A5", "USEC  //  DEBRIEF 03", USEC_HOLO)]

# ---- timeline, seconds -------------------------------------------------------------------------
PIECES_FROM, PIECES_TO = 0.5, 2.1      # insignia pieces fly in
HOLO_FROM, HOLO_TO = 1.1, 2.3          # the hologram builds up from the table
SWAP_AT = 4.6                          # the signal flickers over to the other side's weapon
WORD_FROM, WORD_TO = 2.5, 3.7          # redaction bars come off the wordmark
SUB_AT = 3.7                           # subtitle types out
COUNT_FROM, COUNT_TO = 0.5, 4.0        # the step counter runs to 15/15
TEAR_FROM, TEAR_TO = 6.7, 7.45         # the signal tears away to the empty net
TURN = 0.55                            # turns of the hologram over the loop


def ease_out(p):
    p = min(1.0, max(0.0, p))
    return 1.0 - (1.0 - p) ** 3


def smooth(p):
    p = min(1.0, max(0.0, p))
    return p * p * (3.0 - 2.0 * p)


class Layout:
    """Where everything goes on the hero."""

    def __init__(self):
        self.w, self.h = 1600, 720
        self.insignia = {"bear": (290, 292), "usec": (1310, 292)}
        self.logo_r = 92
        self.holo = (800, 262, 580)          # centre and width of the turntable
        self.word_y, self.word_size, self.track = 540, 100, 30
        self.sub_y, self.sub_size = 620, 22


# ---- the hologram ------------------------------------------------------------------------------

class Hologram:
    """One weapon on the turntable, at a fixed scale so it does not breathe as it turns."""

    def __init__(self, iid, colour, width):
        self.v, self.f, _ = show.load_model(iid)
        self.n, self.edges = show.edges_of(self.v, self.f)
        self.colour = colour
        self.width = width
        self.centre = self.v.mean(axis=0)
        r = np.linalg.norm(self.v - self.centre, axis=1).max()
        self.scale = width * show.SS * 0.5 / r

    def render(self, yaw, pitch=0.28):
        SS = show.SS
        W = self.width * SS
        H = int(W * 0.62)
        cy, sy, cp, sp = math.cos(yaw), math.sin(yaw), math.cos(pitch), math.sin(pitch)
        M = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]]) @ np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
        pv = (self.v - self.centre) @ M.T
        pn = self.n @ M.T

        def P(p):
            return (W / 2 + p[0] * self.scale, H / 2 - p[1] * self.scale)

        lit, segments = show.visible(pv, pn, self.f, self.edges, P, W, H)
        faces = show.shaded(lit, self.colour, 16, 44)
        lines = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ld = ImageDraw.Draw(lines)
        for pts, outline in segments:
            ld.line(pts, fill=self.colour + (255 if outline else 165,), width=max(1, int((2.2 if outline else 1.3) * SS)))
        glow = lines.filter(ImageFilter.GaussianBlur(4 * SS))
        img = Image.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, 0)), glow)
        img = Image.alpha_composite(img, faces)
        img = Image.alpha_composite(img, lines)
        return img.resize((W // SS, H // SS), Image.LANCZOS)


def turntable(draw, cx, cy, w, colour, k):
    """The table's ring under the weapon, in perspective, with its ticks."""
    rx, ry = w * 0.42, w * 0.08
    col = tuple(int(c * 0.55) for c in colour) + (int(150 * k),)
    draw.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], outline=col, width=2)
    draw.ellipse([cx - rx * 0.8, cy - ry * 0.8, cx + rx * 0.8, cy + ry * 0.8], outline=col[:3] + (int(70 * k),), width=1)
    for i in range(48):
        a = i / 48 * math.tau
        r_in = 0.93 if i % 4 else 0.86
        draw.line([(cx + math.cos(a) * rx * r_in, cy + math.sin(a) * ry * r_in), (cx + math.cos(a) * rx, cy + math.sin(a) * ry)], fill=col, width=1)


# ---- static layers -----------------------------------------------------------------------------

def background(lay):
    """Ink, both sides' map backdrops from the game, tinted and fading to the middle, a dot grid, scanlines."""
    w, h = lay.w, lay.h
    arr = np.broadcast_to(np.array(INK, np.float32), (h, w, 3)).copy()
    xs = np.linspace(0, 1, w)[None, :]
    for name, tint in (("bear", BEAR), ("usec", USEC)):
        tex = Image.open(os.path.join(kit.RES, f"backdrop_{name}_map.png")).convert("L").resize((w, h), Image.LANCZOS)
        t = np.asarray(tex).astype(np.float32) / 255.0
        fall = np.clip(1.3 - 2.2 * xs, 0, 1) if name == "bear" else np.clip(2.2 * xs - 0.9, 0, 1)
        arr += (t * fall * 0.12)[..., None] * np.array(tint, np.float32)
        arr += (fall ** 2 * 0.05)[..., None] * np.array(tint, np.float32)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx - w / 2) / (w * 0.6)) ** 2 + ((yy - h / 2) / (h * 0.7)) ** 2)
    arr *= np.clip(1.15 - 0.6 * d, 0.4, 1.0)[..., None]
    arr *= (0.96 + 0.04 * (np.arange(h) % 3 != 0))[:, None, None]
    img = kit.to_img(arr)
    draw = ImageDraw.Draw(img)
    for y in range(18, h, 36):
        for x in range(18, w, 36):
            draw.point((x, y), fill=(20, 25, 32))
    return img


def banners(draw, lay):
    """Classification banners top and bottom, as on every page of a classified document."""
    f = kit.font(15, "SemiBold")
    text = "СЕКРЕТНО   //   SECRET   //   REL BEAR, USEC   //   NOT FOR THE OTHER SIDE"
    for y in (0, lay.h - 26):
        draw.rectangle([0, y, lay.w, y + 26], fill=(16, 18, 22))
        edge = y + 25 if y == 0 else y
        draw.line([(0, edge), (lay.w, edge)], fill=(40, 44, 52), width=1)
        draw.text((lay.w / 2, y + 13), text, font=f, fill=(150, 64, 56), anchor="mm")


def frame_brackets(draw, cx, cy, r, colour, k):
    """The in-game insignia frame: four corner brackets that close in as the pieces arrive."""
    s = r * (1.3 + 0.4 * (1 - k))
    arm = r * 0.42
    col = colour + (int(200 * k),)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = cx + sx * s, cy + sy * s
            draw.line([(x, y), (x - sx * arm, y)], fill=col, width=2)
            draw.line([(x, y), (x, y - sy * arm)], fill=col, width=2)


def piece_images(faction, radius, tint):
    """The insignia's pieces as tinted sprites, with where each sits and the way it flies in."""
    field = "BearPieces" if faction == "bear" else "UsecPieces"
    src = open(os.path.join(kit.SRC, "Insignia.Pieces.cs"), encoding="utf-8").read()
    block = src[src.index(field):]
    block = block[:block.index("};")]
    num = r"(-?[\d.]+)f"
    pat = (r'new Piece\("(\w+)", new Rect\(' + ", ".join([num] * 4) + r"\), new Vector2\(" + num + ", " + num
           + r"\), new Vector2\(" + num + ", " + num + r"\), new Vector2\(" + num + ", " + num + r"\)\)")
    atlas = Image.open(os.path.join(kit.RES, f"insignia_{faction}.png")).convert("RGBA")
    aw, ah = atlas.size
    alpha = atlas.split()[3]
    out = []
    for m in re.finditer(pat, block):
        u, v, uw, vh, cx, cy, sx, sy, dx, dy = (float(g) for g in m.groups()[1:])
        box = (round(u * aw), round((1 - v - vh) * ah), round((u + uw) * aw), round((1 - v) * ah))
        size = (max(1, round(sx * radius)), max(1, round(sy * radius)))
        sprite = Image.new("RGBA", size, tint + (255,))
        sprite.putalpha(alpha.crop(box).resize(size, Image.LANCZOS))
        out.append({"img": sprite, "centre": (cx, cy), "dir": (dx, dy)})
    return out


# ---- per frame ---------------------------------------------------------------------------------

class Scene:
    def __init__(self, lay):
        self.lay = lay
        self.base = background(lay)
        self.pieces = {"bear": piece_images("bear", lay.logo_r, BEAR), "usec": piece_images("usec", lay.logo_r, USEC)}
        self.holos = [Hologram(iid, col, lay.holo[2]) for iid, _, _, col in WEAPONS]
        self.rng = np.random.default_rng(11)

    def insignia(self, layer, faction, t):
        cx, cy = self.lay.insignia[faction]
        r = self.lay.logo_r
        pieces = self.pieces[faction]
        delay = 0.15 if faction == "usec" else 0.0
        arrived = 0.0
        for i, p in enumerate(pieces):
            start = PIECES_FROM + delay + (PIECES_TO - PIECES_FROM - 0.7) * i / max(1, len(pieces) - 1)
            k = (t - start) / 0.7
            if k <= 0:
                continue
            arrived += min(1.0, k) / len(pieces)
            e = ease_out(k)
            dist = (1 - e) * 2.4 * r
            px = cx + p["centre"][0] * r + p["dir"][0] * dist
            py = cy - p["centre"][1] * r - p["dir"][1] * dist
            img = p["img"]
            if k < 0.4:
                img = img.copy()
                img.putalpha(img.split()[3].point(lambda a, s=k / 0.4: int(a * s)))
            layer.alpha_composite(img, (int(round(px - img.width / 2)), int(round(py - img.height / 2))))
        if arrived > 0:
            d = ImageDraw.Draw(layer)
            frame_brackets(d, cx, cy, r, BEAR if faction == "bear" else USEC, smooth(arrived))
            label = "BEAR  //  ШТАБ" if faction == "bear" else "USEC  //  TOC"
            d.text((cx, cy + r * 1.3 + 30), label, font=kit.font(17, "SemiBold"), fill=LABEL + (int(230 * smooth(arrived)),), anchor="mm")

    def hologram(self, layer, t):
        cx, cy, w = self.lay.holo
        if t < HOLO_FROM:
            return
        which = 0 if t < SWAP_AT else 1
        yaw = 0.35 + t / LENGTH * TURN * math.tau
        img = self.holos[which].render(yaw)
        # It builds up from the table: revealed bottom to top.
        k = smooth((t - HOLO_FROM) / (HOLO_TO - HOLO_FROM))
        a = np.asarray(img.split()[3]).astype(np.float32)
        ys = np.linspace(1, 0, a.shape[0])[:, None]
        a = a * np.clip((k * 1.15 - ys) / 0.08, 0, 1)
        # The swap: the picture flickers and splits for a few frames either side.
        flick = max(0.0, 1 - abs(t - SWAP_AT) / 0.2)
        if flick > 0:
            a *= 0.55 + 0.45 * (self.rng.random() > 0.5)
        img.putalpha(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)))
        if flick > 0:
            r, g, b, al = img.split()
            img = Image.merge("RGBA", (ImageChops.offset(r, int(8 * flick), 0), g, ImageChops.offset(b, -int(8 * flick), 0), al))
        table_y = cy + img.height * 0.3
        turntable(ImageDraw.Draw(layer), cx, table_y, w, self.holos[which].colour, k)
        layer.alpha_composite(img, (int(cx - img.width / 2), int(cy - img.height / 2)))
        if k >= 1:
            name, line = WEAPONS[which][1], WEAPONS[which][2]
            ImageDraw.Draw(layer).text((cx, table_y + w * 0.08 + 24), f"{line}  //  {name}", font=kit.font(16, "SemiBold"),
                                       fill=QUIET + (230,), anchor="mm")

    def wordmark(self, layer, t):
        lay = self.lay
        d = ImageDraw.Draw(layer)
        f = kit.font(lay.word_size, "Bold")
        word = "CLASSIFIED"
        widths = [d.textlength(c, font=f) for c in word]
        total = sum(widths) + lay.track * (len(word) - 1)
        x = (lay.w - total) / 2
        top, bottom = lay.word_y - lay.word_size * 0.56, lay.word_y + lay.word_size * 0.5
        span = (WORD_TO - WORD_FROM) / len(word)
        for i, c in enumerate(word):
            w = widths[i]
            on_at = WORD_FROM + i * span
            if t >= on_at:
                d.text((x, lay.word_y), c, font=f, fill=TEXT, anchor="lm")
            if t < WORD_FROM - 0.3:
                bar = 0.0
            elif t < WORD_FROM:
                bar = smooth((t - (WORD_FROM - 0.3)) / 0.3)
            elif t < on_at:
                bar = 1.0
            else:
                bar = 1 - smooth((t - on_at) / (span * 1.6))
            if bar > 0:
                # The bar comes in from the left, then is pulled off to the right.
                left = x - 6 if t < on_at else x - 6 + (w + 12) * (1 - bar)
                right = x - 6 + (w + 12) * bar if t < WORD_FROM else x + w + 6
                d.rectangle([left, top, right, bottom], fill=(0, 0, 0))
                d.rectangle([left, top, right, top + 2], fill=(44, 48, 56))
            x += w + lay.track
        if t >= SUB_AT:
            sub = "BEAR  //  USEC  //  FIELD TERMINAL"
            n = min(len(sub), int((t - SUB_AT) * 70))
            sf = kit.font(lay.sub_size)
            full = "  ".join(sub)
            d.text(((lay.w - d.textlength(full, font=sf)) / 2, lay.sub_y), "  ".join(sub[:n]), font=sf, fill=(112, 120, 134), anchor="lm")

    def labels(self, draw, t):
        lay = self.lay
        f = kit.font(19)
        k = smooth((t - COUNT_FROM) / (COUNT_TO - COUNT_FROM)) if t < TEAR_TO else 0
        step = max(1, min(15, 1 + int(k * 14.999)))
        draw.text((48, 52), "NET 4  //  SIGN-IN", font=f, fill=LABEL, anchor="lm")
        draw.text((lay.w - 48, 52), f"STEP {step:02d} / 15    {int(round(k * 100))}%", font=f, fill=LABEL, anchor="rm")
        draw.text((48, lay.h - 52), "NORVINSK AO", font=f, fill=QUIET, anchor="lm")
        draw.text((lay.w - 48, lay.h - 52), "ЭКЗ. 1  //  COPY 1 OF 1", font=f, fill=QUIET, anchor="rm")

    def tear(self, img, amount):
        arr = np.asarray(img).copy()
        h = arr.shape[0]
        for _ in range(int(4 + amount * 14)):
            y = self.rng.integers(0, h)
            hgt = int(self.rng.integers(4, 40) * (0.4 + amount))
            arr[y:y + hgt] = np.roll(arr[y:y + hgt], int(self.rng.integers(-1, 2) * self.rng.integers(10, 90) * amount), axis=1)
        split = int(3 + amount * 12)
        arr[..., 0] = np.roll(arr[..., 0], split, axis=1)
        arr[..., 2] = np.roll(arr[..., 2], -split, axis=1)
        return Image.fromarray(arr)

    def frame(self, t):
        img = self.base.copy().convert("RGBA")
        marks = Image.new("RGBA", img.size, (0, 0, 0, 0))
        for faction in ("bear", "usec"):
            self.insignia(marks, faction, t)
        holo = Image.new("RGBA", img.size, (0, 0, 0, 0))
        self.hologram(holo, t)
        words = Image.new("RGBA", img.size, (0, 0, 0, 0))
        self.wordmark(words, t)
        glow = marks.filter(ImageFilter.GaussianBlur(12))
        glow.putalpha(glow.split()[3].point(lambda v: int(v * 0.8)))
        img.alpha_composite(glow)
        img.alpha_composite(marks)
        img.alpha_composite(holo)
        img.alpha_composite(words)
        draw = ImageDraw.Draw(img)
        banners(draw, self.lay)
        self.labels(draw, t)
        out = img.convert("RGB")
        if t >= TEAR_FROM:
            k = (t - TEAR_FROM) / (TEAR_TO - TEAR_FROM)
            if k < 1:
                out = self.tear(out, smooth(k) * 1.2)
            fade = 1.0 if k >= 1 else smooth((k - 0.55) / 0.45)
            if fade > 0:
                empty = self.base.copy()
                d = ImageDraw.Draw(empty)
                banners(d, self.lay)
                self.labels(d, t)
                out = Image.blend(out, empty, fade)
        return out


# ---- the thumbnail -----------------------------------------------------------------------------

def glowing(faction, height, colour):
    """The faction's insignia at height, in its colour, with a soft glow round it."""
    m = kit.insignia(faction, 300)
    scale = height / m.shape[0]
    mask = Image.fromarray((m * 255).astype(np.uint8)).resize((int(m.shape[1] * scale), int(height)), Image.LANCZOS)
    pad = int(height * 0.25)
    a = Image.new("L", (mask.width + pad * 2, mask.height + pad * 2), 0)
    a.paste(mask, (pad, pad))
    out = Image.new("RGBA", a.size, colour + (0,))
    out.putalpha(a.filter(ImageFilter.GaussianBlur(height * 0.06)).point(lambda v: int(v * 0.8)))
    solid = Image.new("RGBA", a.size, colour + (0,))
    solid.putalpha(a)
    out.alpha_composite(solid)
    return out


def thumbnail():
    """
    The listing's square: the torn signal. BEAR's insignia on its red half, USEC's on its blue half,
    huge, the signal torn down the seam between them, and the name across the bottom with its
    colours split. Made to read at the size a listing shows it.
    """
    S = 1000
    xx = np.linspace(0, 1, S)[None, :]
    yy = np.linspace(0, 1, S)[:, None]
    k = np.clip((xx - 0.5) * 40 + 0.5, 0, 1)[..., None]
    arr = np.array([60, 10, 8], np.float32) * (1 - k) + np.array([6, 22, 52], np.float32) * k
    d = np.sqrt((xx - 0.5) ** 2 + (yy - 0.5) ** 2)
    arr = arr * np.clip(1.25 - d * 1.2, 0.3, 1)[..., None]
    arr *= (0.94 + 0.06 * (np.arange(S) % 4 != 0))[:, None, None]
    img = kit.to_img(arr).convert("RGBA")
    for faction, colour, cx in (("bear", BEAR, 250), ("usec", USEC, 750)):
        sprite = glowing(faction, 520, colour)
        img.alpha_composite(sprite, (int(cx - sprite.width / 2), int(400 - sprite.height / 2)))

    # The name, its red and blue pulled apart a little.
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    f = kit.font(140, "Bold")
    widths = [draw.textlength(c, font=f) for c in "CLASSIFIED"]
    x = (S - sum(widths) - 4 * 9) / 2
    for c, w in zip("CLASSIFIED", widths):
        draw.text((x, 800), c, font=f, fill=(245, 246, 250), anchor="lm")
        x += w + 4
    r, g, b, a = layer.split()
    img.alpha_composite(Image.merge("RGBA", (ImageChops.offset(r, 6, 0), g, ImageChops.offset(b, -6, 0), a)))

    # The tear: bands down the seam slid sideways.
    out = np.asarray(img).copy()
    rng = np.random.default_rng(3)
    for _ in range(14):
        y = rng.integers(0, S)
        h = rng.integers(4, 26)
        out[y:y + h, 380:620] = np.roll(out[y:y + h, 380:620], rng.integers(-40, 40), axis=1)
    return Image.fromarray(out).convert("RGB")


def main():
    show.load_themes()
    if "--thumb" in sys.argv:
        thumbnail().save(THUMB, optimize=True)
        print(THUMB)
        return
    scene = Scene(Layout())
    if "--still" in sys.argv:
        tmp = tempfile.gettempdir()
        for t in (1.2, 1.8, 3.2, 4.2, 4.62, 6.0, 7.0):
            path = os.path.join(tmp, f"classified_v4_{t:.2f}.png")
            scene.frame(t).save(path)
            print(path)
        return
    frames = [scene.frame(i / FPS) for i in range(int(LENGTH * FPS))]
    frames[0].save(OUT, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0, quality=82, method=6)
    print(OUT, f"{os.path.getsize(OUT) / 1e6:.1f} MB, {len(frames)} frames")


if __name__ == "__main__":
    main()
