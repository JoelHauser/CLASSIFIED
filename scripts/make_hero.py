"""
The mod page's animated hero: the terminal flipping between USEC and BEAR through a broken signal.

Each side is the terminal as the game draws it: insignia in its frame, wordmark, the load bar and
step log, an operations map with its arrow advancing, a hologram turning on its table, the
ticker. Between them the signal tears: bands slide, the colours split, blocks drop out, and bands
of the other side come through until it takes over. The loop has no seam.

    python scripts/make_hero.py            docs/images/hero.webp
    python scripts/make_hero.py --still    a few frames as PNGs in the system temp folder, to check

Needs numpy and pillow; reads the models and themes make_showcase.py reads.
"""
import math
import os
import random
import re
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_showcase as show  # noqa: E402

ROOT = show.ROOT
W, H = 1600, 720
FPS = 20
HOLD = 3.1       # seconds on a side
FLIP = 0.9       # seconds in the broken signal
FRAMES = int(round((HOLD + FLIP) * 2 * FPS))
OUT = os.path.join(ROOT, "docs", "images", "hero.webp")


def font(size):
    return ImageFont.truetype(show.FONT, size)


SIDES = {
    "usec": {
        "theme": "UsecOperations", "own": (82, 160, 255), "enemy": (240, 70, 64),
        "logo": "usec.png", "word": "U S E C", "sub": "TACTICAL OPERATIONS CENTER  //  SIGNING IN",
        "backdrop": "backdrop_usec_map.png", "header": "USEC  //  TOC  //  SECRET//REL USEC", "corner": "CLEARANCE B",
        "stepword": "STEP", "tag": "COMPLETE", "weapon": "hk416_3", "weapon_name": "HK 416A5",
        "weapon_line": "CAL 5.56x45 NATO   ROF 850 RPM", "map_title": "OPORD 05-51  //  OPERATION OVERLAY",
        "obj": "OBJ RAVEN", "pl": "PL DAGGER (LD)",
        "current": "LOADING WEATHER AND MAPS",
        "log": ["LOADING WEAPON BUILDS", "LOADING HANDBOOK", "SELECTING PROFILE", "LOADING PROFILES"],
        "ticker": "WIND NE 1 M/S   |   UNKNOWN AIRCRAFT OVER SHORELINE. NOT OURS. NOT THEIRS EITHER   |   QRF 10 MIN   |   "
                  "MRE DELIVERY CONFIRMED. NO CHILLI. MORALE DOWN 59%   |   FPCON BRAVO   |   CASEVAC AVAILABLE   |   ",
        "lost": "SIGNAL LOST",
    },
    "bear": {
        "theme": "BearCommand", "own": (226, 58, 44), "enemy": (64, 140, 255),
        "logo": "bear.png", "word": "B E A R", "sub": "ШТАБ ГРУППИРОВКИ  //  ВХОД В СЕТЬ",
        "backdrop": "backdrop_bear_map.png", "header": "BEAR  //  ШТАБ ГРУППИРОВКИ  //  СОВЕРШЕННО СЕКРЕТНО", "corner": "ФОРМА ДОПУСКА 1",
        "stepword": "ЭТАП", "tag": "ВЫП", "weapon": "ak74m_5", "weapon_name": "АК-74М",
        "weapon_line": "КАЛИБР 5,45x39   ТЕМП 650 ВЫСТР/МИН", "map_title": "РАБОЧАЯ КАРТА КОМАНДИРА  //  ЛИСТ N-37-81",
        "obj": "ЦЕЛЬ 1", "pl": "РУБЕЖ АТАКИ",
        "current": "ЗАГРУЗКА ПОГОДЫ И КАРТ",
        "log": ["ЗАГРУЗКА СБОРОК ОРУЖИЯ", "ЗАГРУЗКА СПРАВОЧНИКА", "ВЫБОР ПРОФИЛЯ", "ЗАГРУЗКА ПРОФИЛЕЙ"],
        "ticker": "ВЕТЕР С-В 1 М/С   |   НЕОПОЗНАННЫЙ БОРТ НАД БЕРЕГОМ. НЕ НАШ. И НЕ ИХ   |   РЕЗЕРВ 10 МИН   |   "
                  "ТУШЁНКА ДОСТАВЛЕНА. ХЛЕБА НЕТ   |   ПОВЕРКА 41 В СТРОЮ   |   ЭВАКУАЦИЯ ГОТОВА   |   ",
        "lost": "НЕТ СИГНАЛА",
    },
}

MAP = (44, 150, 500, 572)     # left panel
HOLO = (1100, 150, 1556, 572) # right panel


def frame_corners(d, box, colour, tick=14, width=3):
    x0, y0, x1, y1 = box
    for cx, cy, sx, sy in ((x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)):
        d.line([(cx, cy), (cx + sx * tick, cy)], fill=colour, width=width)
        d.line([(cx, cy), (cx, cy + sy * tick)], fill=colour, width=width)


def road(rng, box):
    """A wandering line across a panel: a road or a contour."""
    x0, y0, x1, y1 = box
    y = rng.uniform(y0, y1)
    pts = []
    n = 14
    phase = rng.uniform(0, 6.28)
    amp = rng.uniform(14, 60)
    for k in range(n + 1):
        f = k / n
        pts.append((x0 + (x1 - x0) * f, y + math.sin(f * 4 + phase) * amp + (rng.uniform(-1, 1) * 6)))
    return pts


def build_base(side):
    """Everything on a side that does not move."""
    s = SIDES[side]
    t = show.THEMES[s["theme"]]
    ink, accent, text, dim = t["ink"], t["accent"], t["text"], t["dim"]
    rng = random.Random(side)

    # Ground: ink, the unit's glow, the backdrop's map lines in the accent colour.
    base = Image.new("RGB", (W, H), ink)
    glow = Image.new("RGB", (W, H), (0, 0, 0))
    ImageDraw.Draw(glow).ellipse([W * 0.2, -H * 0.2, W * 0.8, H * 1.1], fill=tuple(int(c * 0.6) for c in t["glow"]))
    glow = glow.filter(ImageFilter.GaussianBlur(170))
    base = Image.fromarray(np.clip(np.asarray(base, np.int32) + np.asarray(glow, np.int32), 0, 255).astype(np.uint8)).convert("RGBA")
    art = Image.open(os.path.join(ROOT, "src", "CLASSIFIED", "Resources", s["backdrop"])).convert("RGBA")
    art = art.resize((int(art.width * H / art.height), H), Image.LANCZOS).crop((0, 0, W, H))
    tint = Image.new("RGBA", (W, H), accent + (0,))
    tint.putalpha(art.getchannel("A").point(lambda a: int(a * 0.28)))
    base = Image.alpha_composite(base, tint).convert("RGB")

    # (Drawing in RGBA mode onto an RGB image is what blends; onto an RGBA one it overwrites.)
    d = ImageDraw.Draw(base, "RGBA")
    frame_corners(d, (22, 22, W - 22, H - 22), accent + (220,), tick=30)
    d.text((48, 48), s["header"], font=font(20), fill=dim + (255,))
    d.text((W - 48, 48), s["corner"], font=font(20), fill=dim + (255,), anchor="ra")

    # The map panel: dark ground, grid, contours, a road, its title.
    x0, y0, x1, y1 = MAP
    d.rectangle(MAP, fill=tuple(int(c * 0.55) for c in ink) + (200,))
    for k in range(1, 8):
        x = x0 + (x1 - x0) * k / 8
        d.line([(x, y0), (x, y1)], fill=accent + (34,), width=1)
    for k in range(1, 8):
        y = y0 + (y1 - y0) * k / 8
        d.line([(x0, y), (x1, y)], fill=accent + (34,), width=1)
    for _ in range(7):
        d.line(road(rng, (x0, y0 + 10, x1, y1 - 10)), fill=accent + (75,), width=1)
    d.line(road(rng, (x0, y0 + 40, x1, y1 - 40)), fill=accent + (110,), width=3)
    frame_corners(d, MAP, accent + (220,))
    d.rectangle(MAP, outline=accent + (70,), width=1)
    d.text((x0 + 2, y0 - 26), s["map_title"], font=font(16), fill=dim + (255,))
    # Their positions and the objective.
    obj = (x0 + (x1 - x0) * 0.78, y0 + (y1 - y0) * 0.38)
    for k in range(3):
        ex, ey = obj[0] + rng.uniform(-50, 60), obj[1] + rng.uniform(-70, 120)
        d.polygon([(ex, ey - 11), (ex + 11, ey), (ex, ey + 11), (ex - 11, ey)], outline=s["enemy"] + (235,), width=2)
    d.ellipse([obj[0] - 34, obj[1] - 28, obj[0] + 34, obj[1] + 28], outline=s["own"] + (230,), width=2)
    d.text((obj[0], obj[1] - 48), s["obj"], font=font(15), fill=s["own"] + (255,), anchor="ma")
    plx = x0 + (x1 - x0) * 0.34
    for k in range(0, 12, 2):
        d.line([(plx + math.sin(k) * 5, y0 + (y1 - y0) * k / 12), (plx + math.sin(k + 1) * 5, y0 + (y1 - y0) * (k + 1) / 12)], fill=s["own"] + (170,), width=2)
    d.text((plx + 8, y0 + 8), s["pl"], font=font(13), fill=s["own"] + (220,))
    for k in range(3):
        ux, uy = x0 + 44 + rng.uniform(0, 60), y0 + 100 + k * 96 + rng.uniform(-10, 10)
        d.rectangle([ux - 15, uy - 10, ux + 15, uy + 10], outline=s["own"] + (235,), width=2)
        d.line([(ux - 15, uy - 10), (ux + 15, uy + 10)], fill=s["own"] + (235,), width=2)
        d.line([(ux - 15, uy + 10), (ux + 15, uy - 10)], fill=s["own"] + (235,), width=2)

    # The insignia and the wordmark, in the middle.
    logo = Image.open(os.path.join(ROOT, "art", s["logo"])).convert("RGBA")
    h = 158
    logo = logo.resize((int(logo.width * h / logo.height), h), Image.LANCZOS)
    painted = Image.new("RGBA", logo.size, accent + (255,))
    painted.putalpha(logo.getchannel("A"))
    base.paste(painted, (W // 2 - logo.width // 2, 112), painted)
    d = ImageDraw.Draw(base, "RGBA")
    d.text((W / 2, 344), s["word"], font=font(60), fill=text + (255,), anchor="mm")
    d.text((W / 2, 396), s["sub"], font=font(22), fill=accent + (255,), anchor="mm")

    # The log's finished steps.
    for k, line in enumerate(s["log"]):
        y = 548 + k * 24
        d.text((560, y), s["tag"], font=font(18), fill=accent + (235 - k * 40,))
        d.text((690, y), line, font=font(18), fill=dim + (255 - k * 40,))

    # The hologram's panel: its header and its table.
    hx0, hy0, hx1, hy1 = HOLO
    d.text((hx0, hy0 - 28), s["weapon_name"], font=font(30), fill=text + (255,), anchor="ls")
    d.text((hx0, hy0 - 6), s["weapon_line"], font=font(15), fill=dim + (255,), anchor="ls")

    # The ticker's rules.
    d.line([(44, H - 72), (W - 44, H - 72)], fill=accent + (50,), width=1)
    d.line([(44, H - 36), (W - 44, H - 36)], fill=accent + (50,), width=1)
    return base


class Model:
    """A weapon ready to draw at any turn."""

    def __init__(self, iid):
        self.v, self.f, _ = show.load_model(iid)
        self.n, self.edges = show.edges_of(self.v, self.f)


def draw_hologram(img, model, yaw, accent):
    x0, y0, x1, y1 = HOLO
    w, h = (x1 - x0) * 2, (y1 - y0) * 2
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cp, sp = math.cos(0.3), math.sin(0.3)
    cy, sy = math.cos(yaw), math.sin(yaw)
    tilt = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]])
    M = tilt @ np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
    pv = model.v @ M.T
    pn = model.n @ M.T
    scale = w * 0.45

    def P(p):
        return (w * 0.5 + p[0] * scale, h * 0.44 - p[1] * scale)

    d = ImageDraw.Draw(layer)
    # The table.
    ring = []
    for k in range(61):
        a = k * math.pi * 2 / 60
        ring.append(P(tilt @ np.array([math.cos(a) * 0.95, -0.62, math.sin(a) * 0.95])))
    d.line(ring, fill=accent + (70,), width=2)
    for k in range(0, 60, 3):
        a = k * math.pi * 2 / 60 - yaw * 0.7
        d.line([P(tilt @ np.array([math.cos(a) * 0.88, -0.62, math.sin(a) * 0.88])),
                P(tilt @ np.array([math.cos(a) * 0.95, -0.62, math.sin(a) * 0.95]))], fill=accent + (70,), width=2)
    light = np.array([0.25, 0.75, 0.6])
    light /= np.linalg.norm(light)
    for i in np.argsort(pv[model.f].mean(axis=1)[:, 2]):
        if pn[i][2] > 0:
            lit = max(0.0, float(pn[i] @ light))
            d.polygon([P(pv[j]) for j in model.f[i]], fill=accent + (int(12 + 36 * lit),))
    front = pn[:, 2] > 0
    for a, b, f0, f1, crease in model.edges:
        outline = f1 < 0 or front[f0] != front[f1]
        if outline or (crease and (front[f0] or (f1 >= 0 and front[f1]))):
            d.line([P(pv[a]), P(pv[b])], fill=accent + (235 if outline else 130,), width=3 if outline else 2)
    glow = layer.filter(ImageFilter.GaussianBlur(5))
    small = Image.alpha_composite(glow, layer).resize((x1 - x0, y1 - y0), Image.LANCZOS)
    img.paste(small, (x0, y0), small)


def side_frame(side, base, model, f):
    """A side at f (0 to 1) through its time on screen."""
    s = SIDES[side]
    t = show.THEMES[s["theme"]]
    accent, text, dim, warn = t["accent"], t["text"], t["dim"], t["warn"]
    img = base.copy()
    d = ImageDraw.Draw(img, "RGBA")

    # The insignia's frame: a turning arc and ticks for BEAR's gauge; brackets for USEC's sight.
    cx, cy, r = W // 2, 191, 104
    spin = f * 360
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=accent + (70,), width=2)
    d.arc([cx - r, cy - r, cx + r, cy + r], spin - 60, spin + 60, fill=accent + (240,), width=4)
    if side == "bear":
        for k in range(0, 360, 15):
            a = math.radians(k - spin * 0.4)
            d.line([(cx + math.cos(a) * (r + 5), cy + math.sin(a) * (r + 5)),
                    (cx + math.cos(a) * (r + (15 if k % 45 == 0 else 10)), cy + math.sin(a) * (r + (15 if k % 45 == 0 else 10)))], fill=accent + (150,), width=2)
    else:
        q = r + 10
        frame_corners(d, (cx - q, cy - q, cx + q, cy + q), accent + (220,), tick=26)

    # The load: the bar, its counter, the step that is running.
    progress = 0.42 + 0.5 * f
    steps = 15
    done = int(progress * steps)
    bx0, bx1, by = 560, 1040, 462
    d.text((bx0, by - 26), f"{s['stepword']} {done + 1:02d} / {steps}", font=font(18), fill=dim + (255,))
    d.text((bx1, by - 26), f"{int(progress * 100)}%", font=font(18), fill=text + (255,), anchor="ra")
    seg = (bx1 - bx0) / steps
    for k in range(steps):
        fill = accent + (235,) if k < done else accent + (40,)
        d.rectangle([bx0 + k * seg + 2, by, bx0 + (k + 1) * seg - 2, by + 6], fill=fill)
    if done < steps:
        part = progress * steps - done
        d.rectangle([bx0 + done * seg + 2, by, bx0 + done * seg + 2 + (seg - 4) * part, by + 6], fill=warn + (255,))
    d.text((bx0, by + 24), "> " + s["current"], font=font(26), fill=text + (255,))
    d.text((bx1, by + 28), f"{f * 3.1:0.1f}s", font=font(22), fill=text + (255,), anchor="ra")

    # The map's broad arrow, advancing on the objective.
    x0, y0, x1, y1 = MAP
    reach = 0.25 + 0.55 * f
    a0 = (x0 + (x1 - x0) * 0.1, y0 + (y1 - y0) * 0.72)
    a1 = (x0 + (x1 - x0) * 0.78, y0 + (y1 - y0) * 0.38)
    head = (a0[0] + (a1[0] - a0[0]) * reach, a0[1] + (a1[1] - a0[1]) * reach)
    dx, dy = head[0] - a0[0], head[1] - a0[1]
    ln = math.hypot(dx, dy)
    ux, uy = dx / ln, dy / ln
    nx, ny = -uy, ux
    own = s["own"]
    d.polygon([(a0[0] + nx * 8, a0[1] + ny * 8), (head[0] - ux * 20 + nx * 8, head[1] - uy * 20 + ny * 8),
               (head[0] - ux * 20 + nx * 18, head[1] - uy * 20 + ny * 18), head,
               (head[0] - ux * 20 - nx * 18, head[1] - uy * 20 - ny * 18),
               (head[0] - ux * 20 - nx * 8, head[1] - uy * 20 - ny * 8), (a0[0] - nx * 8, a0[1] - ny * 8)], fill=own + (120,), outline=own + (255,))

    # The hologram, turning slowly through side on.
    draw_hologram(img, model, -0.5 + f * 1.0, accent)

    # The ticker, scrolling; two copies, so it never runs out.
    text_font = font(20)
    width = d.textlength(s["ticker"], font=text_font)
    x = 44 - (f * 340) % width
    clip = Image.new("RGBA", (W - 88, 34), (0, 0, 0, 0))
    cd = ImageDraw.Draw(clip)
    cd.text((x - 44, 5), s["ticker"], font=text_font, fill=dim + (255,))
    cd.text((x - 44 + width, 5), s["ticker"], font=text_font, fill=dim + (255,))
    img.paste(clip, (44, H - 70), clip)
    return np.asarray(img, dtype=np.uint8)


def broken(a, b, amount, mix, seed):
    """
    The signal breaking: bands of a slide sideways, bands of b come through in proportion to mix,
    the colour channels split, blocks drop out, and scan lines and noise rise with amount.
    """
    rng = np.random.RandomState(seed)
    out = a.copy()
    h, w = out.shape[:2]
    y = 0
    while y < h:
        band = int(rng.randint(4, 12 + int(70 * amount)))
        source = b if rng.rand() < mix else a
        shift = int(rng.randint(-1, 2) * rng.rand() * 170 * amount) if rng.rand() < 0.3 + 0.7 * amount else 0
        out[y:y + band] = np.roll(source[y:y + band], shift, axis=1)
        y += band
    # The colours pulled apart.
    split = int(4 + 22 * amount)
    out[:, :, 0] = np.roll(out[:, :, 0], split, axis=1)
    out[:, :, 2] = np.roll(out[:, :, 2], -split, axis=1)
    # Blocks of the signal gone, or come from somewhere else.
    for _ in range(int(26 * amount)):
        bw, bh = rng.randint(40, 340), rng.randint(6, 44)
        bx, by = rng.randint(0, w - bw), rng.randint(0, h - bh)
        if rng.rand() < 0.5:
            out[by:by + bh, bx:bx + bw] = (out[by:by + bh, bx:bx + bw] * 0.25).astype(np.uint8)
        else:
            sx, sy = rng.randint(0, w - bw), rng.randint(0, h - bh)
            out[by:by + bh, bx:bx + bw] = (b if rng.rand() < mix else a)[sy:sy + bh, sx:sx + bw]
    # Scan lines.
    dark = 1.0 - 0.28 * amount
    out[::3] = (out[::3] * dark).astype(np.uint8)
    return out


def flash_text(frame, text, colour, strength):
    img = Image.fromarray(frame).convert("RGBA")
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rectangle([0, H / 2 - 54, W, H / 2 + 54], fill=(0, 0, 0, int(190 * strength)))
    d.text((W / 2, H / 2), text, font=font(72), fill=colour + (int(255 * strength),), anchor="mm")
    return np.asarray(Image.alpha_composite(img, layer).convert("RGB"), dtype=np.uint8)


def render(k, bases, models):
    """Frame k of the loop: USEC, the flip to BEAR, BEAR, the flip back."""
    t = k / FPS
    cycle = HOLD + FLIP
    first = t < cycle
    a_side, b_side = ("usec", "bear") if first else ("bear", "usec")
    local = t if first else t - cycle
    a = side_frame(a_side, bases[a_side], models[a_side], min(local, HOLD) / HOLD)
    if local < HOLD:
        # A flicker now and then, so it never quite settles; and the tail of the last flip.
        if local < 0.25:
            settle = 1 - local / 0.25
            return broken(a, a, 0.35 * settle, 0.0, k)
        if k % 37 == 0:
            return broken(a, a, 0.12, 0.0, k)
        return a
    p = (local - HOLD) / FLIP
    b = side_frame(b_side, bases[b_side], models[b_side], 0.0)
    amount = math.sin(p * math.pi)
    frame = broken(a, b, 0.25 + 0.75 * amount, p * p * (3 - 2 * p), k)
    # The worst of it: a white hit, then the words.
    if 0.42 < p < 0.5:
        frame = np.clip(frame.astype(np.int32) + 120, 0, 255).astype(np.uint8)
    if 0.46 < p < 0.62:
        theme = show.THEMES[SIDES[a_side]["theme"]]
        frame = flash_text(frame, SIDES[a_side]["lost"], theme["warn"], 1.0)
    return frame


def main():
    show.load_themes()
    bases = {side: build_base(side) for side in SIDES}
    models = {side: Model(SIDES[side]["weapon"]) for side in SIDES}
    if "--still" in sys.argv:
        for k in (10, 60, 68, 72, 76, 100, 150):
            path = os.path.join(tempfile.gettempdir(), f"hero_{k:03d}.png")
            Image.fromarray(render(k, bases, models)).save(path)
            print("wrote", path)
        return
    frames = []
    for k in range(FRAMES):
        # Drawn at 1600 x 720, kept at 1280 x 576: a page never shows it wider, and it is half the file.
        frames.append(Image.fromarray(render(k, bases, models)).resize((1280, 576), Image.LANCZOS))
        if k % 20 == 0:
            print(f"frame {k}/{FRAMES}", flush=True)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    frames[0].save(OUT, save_all=True, append_images=frames[1:], duration=int(1000 / FPS), loop=0, quality=72, method=5)
    print("wrote", OUT, os.path.getsize(OUT) // 1024, "KB,", len(frames), "frames")


if __name__ == "__main__":
    main()
