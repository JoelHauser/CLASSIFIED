"""
Draws the faction backdrops: the large, faint picture behind each terminal, three for each side.

    BEAR  map       a Soviet General Staff topographic sheet: kilometre grid, contours, forest
                    and marsh symbols, the sheet's margin notes in Russian.
          imagery   aerial reconnaissance film: the frame strip, sprocket holes, data block,
                    targets ringed in grease pencil with notes in Russian.
          document  typewritten orders on three sheets, stamped and signed.
    USEC  map       a digital MGRS map: grid squares and their designators, range rings, a
                    bearing scale round the edge.
          imagery   satellite imagery with analysts' boxes, measurements and metadata.
          document  a five-paragraph operation order under classification banners.

Each is white with the picture in alpha, tinted where it is drawn, 2880 x 1080: wide enough for a
21:9 screen, its sides fading out for anything wider. Drawn at twice the size and averaged down,
so every line is smooth. Nothing on them names a real place.

    python -I scripts/make_backdrops.py            (needs numpy, pillow)
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "CLASSIFIED", "Resources")
FONTS = r"C:\Windows\Fonts"

W, H = 2880, 1080
S = 2  # drawn at this multiple, then averaged down


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), int(size * S))


def canvas():
    img = Image.new("L", (W * S, H * S), 0)
    return img, ImageDraw.Draw(img)


def finish(img, name, fade=True):
    small = img.resize((W, H), Image.LANCZOS)
    a = np.asarray(small).astype(np.float32)
    if fade:
        # The sides fade out, for screens wider than the picture.
        x = np.linspace(0, 1, W)
        edge = np.clip(np.minimum(x, 1 - x) / 0.06, 0, 1)
        a *= (edge * edge * (3 - 2 * edge))[None, :]
    out = np.zeros((H, W, 4), dtype=np.uint8)
    out[:, :, :3] = 255
    out[:, :, 3] = np.clip(a, 0, 255).astype(np.uint8)
    path = os.path.join(OUT, f"backdrop_{name}.png")
    Image.fromarray(out, "RGBA").save(path, optimize=True)
    print(f"{name}: {os.path.getsize(path) // 1024} KB")
    return out


def noise(w, h, scale, octaves, seed):
    rng = np.random.default_rng(seed)
    total = np.zeros((h, w), np.float32)
    amp = 1.0
    for o in range(octaves):
        gw = max(2, int(w / scale * 2 ** o))
        gh = max(2, int(h / scale * 2 ** o))
        g = rng.random((gh, gw)).astype(np.float32)
        layer = np.asarray(Image.fromarray(g, "F").resize((w, h), Image.BICUBIC))
        total += layer * amp
        amp *= 0.5
    total -= total.min()
    return total / total.max()


def contours(d, field, levels, ox, oy, cell, width, value, every=5, bold=None):
    """Marching squares over field (rows y, cols x), drawn with d.line at cell size, origin ox, oy."""
    h, w = field.shape
    for li, iso in enumerate(levels):
        above = field > iso
        code = (above[:-1, :-1].astype(np.int8) | (above[:-1, 1:] << 1) | (above[1:, 1:] << 2) | (above[1:, :-1] << 3))
        ys, xs = np.nonzero((code != 0) & (code != 15))
        major = (li + 1) % every == 0
        lw = (bold if (bold and major) else width) * S
        val = min(255, int(value * (1.7 if major else 1.0)))
        for y, x in zip(ys, xs):
            a, b, c, e = field[y, x], field[y, x + 1], field[y + 1, x + 1], field[y + 1, x]

            def t(p, q):
                return min(1.0, max(0.0, (iso - p) / (q - p))) if q != p else 0.5
            X0, Y0 = ox + x * cell, oy + y * cell
            top = (X0 + cell * t(a, b), Y0)
            right = (X0 + cell, Y0 + cell * t(b, c))
            bottom = (X0 + cell * t(e, c), Y0 + cell)
            left = (X0, Y0 + cell * t(a, e))
            k = code[y, x]
            segs = {1: [(left, top)], 14: [(left, top)], 2: [(top, right)], 13: [(top, right)],
                    3: [(left, right)], 12: [(left, right)], 4: [(right, bottom)], 11: [(right, bottom)],
                    6: [(top, bottom)], 9: [(top, bottom)], 7: [(left, bottom)], 8: [(left, bottom)],
                    5: [(left, top), (right, bottom)], 10: [(top, right), (left, bottom)]}[k]
            for p, q in segs:
                d.line([(p[0] * S, p[1] * S), (q[0] * S, q[1] * S)], fill=val, width=max(1, int(lw)))


def wavy(rng, a, b, amount, n=60):
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    L = math.hypot(dx, dy)
    nx, ny = -dy / L, dx / L
    ph, k, ph2 = rng.uniform(0, 6.3), rng.uniform(1.5, 3.0), rng.uniform(0, 6.3)
    pts = []
    for i in range(n + 1):
        f = i / n
        off = (math.sin(f * math.pi * k + ph) + 0.4 * math.sin(f * math.pi * k * 3.1 + ph2)) * math.sin(f * math.pi) * amount * L
        pts.append((ax + dx * f + nx * off, ay + dy * f + ny * off))
    return pts


def poly(d, pts, width, value, dash=None):
    p = [(x * S, y * S) for x, y in pts]
    if not dash:
        d.line(p, fill=value, width=max(1, int(width * S)), joint="curve")
        return
    on, off = dash
    acc, draw = 0.0, True
    for (x0, y0), (x1, y1) in zip(p, p[1:]):
        seg = math.hypot(x1 - x0, y1 - y0)
        pos = 0.0
        while pos < seg:
            step = min((on if draw else off) * S - acc, seg - pos)
            if draw:
                f0, f1 = pos / seg, (pos + step) / seg
                d.line([(x0 + (x1 - x0) * f0, y0 + (y1 - y0) * f0), (x0 + (x1 - x0) * f1, y0 + (y1 - y0) * f1)], fill=value, width=max(1, int(width * S)))
            pos += step
            acc += step
            if acc >= (on if draw else off) * S - 1e-6:
                acc, draw = 0.0, not draw


def text(d, xy, s, f, value, anchor="la"):
    d.text((xy[0] * S, xy[1] * S), s, font=f, fill=value, anchor=anchor)


def stamp(img, centre, radius, ring_text, middle, f_ring, f_mid, value, angle=0.0):
    """A round rubber stamp: two rings, text round the band, a line or two across the middle."""
    size = int(radius * 2.4 * S)
    tile = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(tile)
    c = size / 2
    r = radius * S
    d.ellipse((c - r, c - r, c + r, c + r), outline=value, width=int(3 * S))
    d.ellipse((c - r * 0.72, c - r * 0.72, c + r * 0.72, c + r * 0.72), outline=value, width=int(2 * S))
    n = len(ring_text)
    for i, ch in enumerate(ring_text):
        a = -math.pi / 2 + 2 * math.pi * i / n
        x, y = c + math.cos(a) * r * 0.86, c + math.sin(a) * r * 0.86
        g = Image.new("L", (int(f_ring.size * 1.6), int(f_ring.size * 1.6)), 0)
        ImageDraw.Draw(g).text((g.width / 2, g.height / 2), ch, font=f_ring, fill=value, anchor="mm")
        g = g.rotate(-math.degrees(a) - 90, resample=Image.BICUBIC)
        tile.paste(Image.fromarray(np.maximum(np.asarray(tile.crop((int(x - g.width / 2), int(y - g.height / 2), int(x - g.width / 2) + g.width, int(y - g.height / 2) + g.height))), np.asarray(g))),
                   (int(x - g.width / 2), int(y - g.height / 2)))
    for k, line in enumerate(middle):
        d.text((c, c + (k - (len(middle) - 1) / 2) * f_mid.size * 1.15), line, font=f_mid, fill=value, anchor="mm")
    # Worn ink: speckle some of it away.
    a = np.asarray(tile).astype(np.float32)
    speck = np.asarray(Image.fromarray((np.random.default_rng(int(centre[0])).random(a.shape) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255.0
    a *= np.clip((speck - 0.25) * 3.0, 0.35, 1.0)
    tile = Image.fromarray(a.astype(np.uint8)).rotate(angle, resample=Image.BICUBIC)
    x0, y0 = int(centre[0] * S - tile.width / 2), int(centre[1] * S - tile.height / 2)
    region = img.crop((x0, y0, x0 + tile.width, y0 + tile.height))
    img.paste(Image.fromarray(np.maximum(np.asarray(region), np.asarray(tile))), (x0, y0))


# ==== BEAR ===============================================================================

def bear_map():
    rng = random.Random(1937)
    img, d = canvas()
    m = 60  # sheet margin
    x0, y0, x1, y1 = m, m + 30, W - m, H - m - 40
    # The terrain.
    cell = 6
    gw, gh = (x1 - x0) // cell + 1, (y1 - y0) // cell + 1
    field = noise(gw, gh, 90, 4, 7)
    contours(d, field, [i / 26 for i in range(3, 25)], x0, y0, cell, 1.0, 70, every=5, bold=1.8)
    # Forest: Soviet sheets mark woodland with small rings, scattered.
    forest = noise(gw, gh, 140, 3, 11)
    f_small = font("arial.ttf", 11)
    for _ in range(4200):
        gx, gy = rng.randrange(gw - 1), rng.randrange(gh - 1)
        if forest[gy, gx] > 0.62:
            cx, cy = x0 + gx * cell, y0 + gy * cell
            r = 2.6
            d.ellipse(((cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S), outline=95, width=S)
    # Marsh: short horizontal dashes.
    for _ in range(2600):
        gx, gy = rng.randrange(gw - 1), rng.randrange(gh - 1)
        if forest[gy, gx] < 0.2 and field[gy, gx] < 0.35:
            cx, cy = x0 + gx * cell, y0 + gy * cell
            d.line(((cx - 4) * S, cy * S, (cx + 4) * S, cy * S), fill=85, width=S)
    # Rivers, a road, a railway.
    for k in range(2):
        r = wavy(rng, (x0, rng.uniform(y0 + 100, y1 - 100)), (x1, rng.uniform(y0 + 100, y1 - 100)), 0.05, 120)
        poly(d, r, 3.0 - k, 150)
    road = wavy(rng, (rng.uniform(x0 + 200, x0 + 900), y0), (rng.uniform(x1 - 900, x1 - 200), y1), 0.04, 80)
    for off in (-3, 3):
        poly(d, [(x + off, y) for x, y in road], 1.4, 150)
    rail = wavy(rng, (x0, rng.uniform(y0 + 200, y1 - 200)), (x1, rng.uniform(y0 + 200, y1 - 200)), 0.02, 60)
    poly(d, rail, 2.0, 150)
    for (ax, ay), (bx, by) in zip(rail, rail[1:]):
        for f in (0.25, 0.75):
            cx, cy = ax + (bx - ax) * f, ay + (by - ay) * f
            L = math.hypot(bx - ax, by - ay) or 1
            nx, ny = -(by - ay) / L * 5, (bx - ax) / L * 5
            d.line(((cx - nx) * S, (cy - ny) * S, (cx + nx) * S, (cy + ny) * S), fill=150, width=S)
    # The kilometre grid, numbered round the edge.
    step = 120
    f_grid = font("arial.ttf", 14)
    xs = list(range(x0 + 40, x1, step))
    ys = list(range(y0 + 50, y1, step))
    for i, x in enumerate(xs):
        d.line((x * S, y0 * S, x * S, y1 * S), fill=110, width=S)
        text(d, (x, y0 - 6), f"{43 + i:02d}", f_grid, 200, "md")
        text(d, (x, y1 + 6), f"{43 + i:02d}", f_grid, 200, "ma")
    for i, y in enumerate(ys):
        d.line((x0 * S, y * S, x1 * S, y * S), fill=110, width=S)
        text(d, (x0 - 8, y), f"{61 + len(ys) - i:02d}", f_grid, 200, "rm")
        text(d, (x1 + 8, y), f"{61 + len(ys) - i:02d}", f_grid, 200, "lm")
    # Spot heights, river notes, forest notes: the sheet's own shorthand, no names.
    f_note = font("ariali.ttf", 13)
    for _ in range(26):
        x, y = rng.uniform(x0 + 60, x1 - 60), rng.uniform(y0 + 40, y1 - 40)
        kind = rng.random()
        if kind < 0.5:
            d.ellipse(((x - 2.5) * S, (y - 2.5) * S, (x + 2.5) * S, (y + 2.5) * S), fill=200)
            text(d, (x + 6, y), f"{rng.randint(140, 290)},{rng.randint(0, 9)}", f_note, 190, "lm")
        elif kind < 0.75:
            text(d, (x, y), f"сосн {rng.randint(14, 26)}/0,{rng.randint(18, 35)}  {rng.randint(3, 6)}", f_note, 170)
        else:
            text(d, (x, y), f"{rng.randint(6, 40)}/{rng.randint(1, 3)},{rng.randint(0, 9)} П", f_note, 170)
    # The sheet's border: a scale of alternating bars, and its corner figures.
    d.rectangle((x0 * S, y0 * S, x1 * S, y1 * S), outline=220, width=2 * S)
    d.rectangle(((x0 - 14) * S, (y0 - 14) * S, (x1 + 14) * S, (y1 + 14) * S), outline=220, width=S)
    for i, x in enumerate(range(x0, x1, 40)):
        if i % 2 == 0:
            d.rectangle((x * S, (y0 - 14) * S, min(x + 40, x1) * S, (y0 - 8) * S), fill=200)
            d.rectangle((x * S, (y1 + 8) * S, min(x + 40, x1) * S, (y1 + 14) * S), fill=200)
    for i, y in enumerate(range(y0, y1, 40)):
        if i % 2 == 0:
            d.rectangle(((x0 - 14) * S, y * S, (x0 - 8) * S, min(y + 40, y1) * S), fill=200)
            d.rectangle(((x1 + 8) * S, y * S, (x1 + 14) * S, min(y + 40, y1) * S), fill=200)
    f_head = font("arialbd.ttf", 22)
    f_mid = font("arial.ttf", 15)
    text(d, (W / 2, 26), "СЕКРЕТНО", f_head, 240, "mm")
    text(d, (x0, 26), "ГЕНЕРАЛЬНЫЙ ШТАБ", f_mid, 210, "lm")
    text(d, (x1, 26), "О-37-112-В   (ЛИСТ 4)", f_mid, 210, "rm")
    text(d, (W / 2, H - 34), "МАСШТАБ 1:50 000      В 1 САНТИМЕТРЕ 500 МЕТРОВ      СПЛОШНЫЕ ГОРИЗОНТАЛИ ПРОВЕДЕНЫ ЧЕРЕЗ 10 МЕТРОВ", f_mid, 210, "mm")
    text(d, (W / 2, H - 14), "СИСТЕМА ВЫСОТ БАЛТИЙСКАЯ      СОСТАВЛЕНО ПО МАТЕРИАЛАМ АЭРОФОТОСЪЁМКИ", f_mid, 170, "mm")
    text(d, (x0, H - 34), "54°20′", f_mid, 200, "lm")
    text(d, (x1, H - 34), "37°30′", f_mid, 200, "rm")
    return finish(img, "bear_map")


def bear_imagery():
    rng = random.Random(1961)
    img, d = canvas()
    # Five frames of film across the width, each a patch of ground under a grain.
    frame_w, gap = 520, 48
    total = frame_w * 5 + gap * 4
    left = (W - total) // 2
    top, bottom = 150, H - 150
    f_data = font("cour.ttf", 16)
    f_mark = font("couri.ttf", 20)
    for k in range(5):
        fx = left + k * (frame_w + gap)
        ground = noise(frame_w // 2, (bottom - top) // 2, 40, 5, 100 + k)
        fields = noise(frame_w // 2, (bottom - top) // 2, 120, 2, 200 + k)
        tone = (0.35 * ground + 0.65 * (np.floor(fields * 6) / 6)) * 120 + 20
        tile = Image.fromarray(tone.astype(np.uint8)).resize((frame_w * S, (bottom - top) * S), Image.BICUBIC)
        img.paste(tile, (fx * S, top * S))
        # Roads and tree lines across the patch.
        for _ in range(3):
            pts = wavy(rng, (fx, rng.uniform(top, bottom)), (fx + frame_w, rng.uniform(top, bottom)), 0.06, 30)
            poly(d, pts, rng.choice([2.0, 3.5]), 190)
        for _ in range(rng.randint(4, 9)):
            bx, by = rng.uniform(fx + 30, fx + frame_w - 60), rng.uniform(top + 30, bottom - 60)
            d.rectangle((bx * S, by * S, (bx + rng.uniform(10, 30)) * S, (by + rng.uniform(8, 22)) * S), fill=215)
        d.rectangle((fx * S, top * S, (fx + frame_w) * S, bottom * S), outline=230, width=2 * S)
        # The data block burned into the frame's corner.
        text(d, (fx + 10, bottom - 26), f"КАДР {417 + k:04d}  ВЫС {rng.choice([800, 1200, 1500])} М  {14 + k // 3:02d}:{rng.randint(10, 59):02d}  АФА-42", f_data, 230)
        # Targets ringed in grease pencil, with a note.
        if k in (1, 3, 4):
            cx, cy = rng.uniform(fx + 120, fx + frame_w - 120), rng.uniform(top + 120, bottom - 140)
            r = rng.uniform(34, 55)
            pts = [(cx + math.cos(a) * r * (1 + 0.08 * math.sin(a * 3)), cy + math.sin(a) * r * 0.8) for a in np.linspace(0, 2 * math.pi * 1.08, 60)]
            poly(d, pts, 3.0, 255)
            text(d, (cx + r + 8, cy - r), rng.choice(["цель №3", "огневая позиция?", "склад", "техника 2 ед.", "КПП"]), f_mark, 255)
        # Fiducial marks in the middle of each edge.
        for (mx, my) in ((fx + frame_w / 2, top + 6), (fx + frame_w / 2, bottom - 6), (fx + 6, (top + bottom) / 2), (fx + frame_w - 6, (top + bottom) / 2)):
            d.line(((mx - 8) * S, my * S, (mx + 8) * S, my * S), fill=240, width=2 * S)
            d.line((mx * S, (my - 8) * S, mx * S, (my + 8) * S), fill=240, width=2 * S)
    # The film's edges: sprocket holes and edge print.
    for y in (top - 60, bottom + 30):
        for x in range(left - 40, left + total + 40, 34):
            d.rounded_rectangle((x * S, y * S, (x + 18) * S, (y + 28) * S), radius=4 * S, outline=200, width=2 * S)
    text(d, (left, top - 82), "АЭРОФОТОСНИМКИ  //  МАРШРУТ 2  //  ПЛЁНКА 0417–0421  //  СЕКРЕТНО", f_data, 220)
    text(d, (left + total, bottom + 84), "ДЕШИФРИРОВАЛ: СТ. ЛЕЙТЕНАНТ ________", f_data, 200, "ra")
    return finish(img, "bear_imagery")


ORDER_WORDS = [
    "ПРИКАЗ", "командира", "отряда", "о", "проведении", "разведки", "в", "районе", "высоты", "сосредоточения",
    "к", "исходу", "дня", "занять", "рубеж", "обеспечить", "скрытность", "выдвижения", "силами", "группы", "ведения",
    "наблюдения", "за", "дорогой", "докладывать", "каждые", "два", "часа", "боеприпасы", "пополнить", "до", "нормы",
    "связь", "по", "радио", "только", "на", "приём", "запасной", "пункт", "сбора", "ответственный", "начальник",
    "штаба", "противник", "силами", "до", "взвода", "обороняет", "перекрёсток", "огневые", "средства", "уточнить",
    "раненых", "эвакуировать", "на", "медпункт", "готовность", "к", "выполнению", "задачи",
]


def bear_document():
    rng = random.Random(1941)
    img, d = canvas()
    f_body = font("cour.ttf", 19)
    f_head = font("courbd.ttf", 24)
    f_small = font("cour.ttf", 16)
    pages = [(180, -1.6), (W / 2, 0.8), (W - 180, -0.6)]
    page_w, page_h = 760, 980
    for k, (cx, angle) in enumerate(pages):
        cx = (k + 0.5) * W / 3
        page = Image.new("L", (page_w * S, page_h * S), 0)
        pd = ImageDraw.Draw(page)
        pd.rectangle((0, 0, page_w * S - 1, page_h * S - 1), outline=150, width=2 * S)
        y = 50
        pd.text(((page_w - 40) * S, y * S), "СОВЕРШЕННО СЕКРЕТНО", font=f_small, fill=230, anchor="ra")
        pd.text(((page_w - 40) * S, (y + 22) * S), f"Экз. № {rng.randint(1, 4)}", font=f_small, fill=200, anchor="ra")
        y += 90
        pd.text((page_w / 2 * S, y * S), f"ПРИКАЗ № {rng.randint(100, 999):03d}", font=f_head, fill=240, anchor="ma")
        y += 50
        pd.text((page_w / 2 * S, y * S), f"{rng.randint(1, 28)} октября 20__ г.", font=f_small, fill=200, anchor="ma")
        y += 60
        para = 1
        while y < page_h - 220:
            line = []
            width = 0
            if rng.random() < 0.12:
                line = [f"{para}."]
                para += 1
            while True:
                w = rng.choice(ORDER_WORDS)
                tw = f_body.getlength(" ".join(line + [w])) / S
                if tw > page_w - 120:
                    break
                line.append(w)
            # Typewriter: each line sits a little crooked, some letters struck harder.
            jitter = rng.uniform(-1.5, 1.5)
            pd.text((60 * S, (y + jitter) * S), " ".join(line), font=f_body, fill=rng.randint(170, 230))
            y += 30
            if rng.random() < 0.1:
                y += 18
        pd.text((60 * S, (page_h - 160) * S), "Командир отряда", font=f_body, fill=220)
        pd.line((360 * S, (page_h - 140) * S, 600 * S, (page_h - 140) * S), fill=160, width=S)
        # A signature: a quick loop.
        sig = [(380 + t * 200 + 12 * math.sin(t * 25), page_h - 150 - 18 * math.sin(t * 9) * (1 - t)) for t in np.linspace(0, 1, 80)]
        pd.line([(x * S, y * S) for x, y in sig], fill=230, width=2 * S, joint="curve")
        page = page.rotate(angle, expand=True, resample=Image.BICUBIC)
        x0, y0 = int(cx * S - page.width / 2), int(H / 2 * S - page.height / 2)
        region = img.crop((x0, y0, x0 + page.width, y0 + page.height))
        img.paste(Image.fromarray(np.maximum(np.asarray(region), np.asarray(page))), (x0, y0))
        stamp(img, (cx + rng.uniform(80, 220), H - 230 + rng.uniform(-20, 20)), 78,
              " ВОЙСКОВАЯ ЧАСТЬ  *  ДЛЯ ПАКЕТОВ  * ", ["№ " + str(rng.randint(10000, 99999))],
              font("arialbd.ttf", 13), font("arialbd.ttf", 17), 230, rng.uniform(-25, 25))
    return finish(img, "bear_document")


# ==== USEC ===============================================================================

def usec_map():
    rng = random.Random(2004)
    img, d = canvas()
    cell = 6
    gw, gh = W // cell + 1, H // cell + 1
    field = noise(gw, gh, 110, 4, 21)
    contours(d, field, [i / 20 for i in range(2, 19)], 0, 0, cell, 0.8, 60, every=4, bold=1.4)
    for k in range(2):
        r = wavy(rng, (0, rng.uniform(150, H - 150)), (W, rng.uniform(150, H - 150)), 0.05, 120)
        poly(d, r, 2.2, 130)
    for k in range(3):
        road = wavy(rng, (rng.uniform(0, W), 0), (rng.uniform(0, W), H), 0.05, 60)
        poly(d, road, 1.6, 150)
    # MGRS grid: 1 km squares, labelled with their easting and northing.
    step = 150
    f_grid = font("bahnschrift.ttf", 14)
    f_big = font("bahnschrift.ttf", 22)
    for i, x in enumerate(range(60, W, step)):
        d.line((x * S, 0, x * S, H * S), fill=95, width=S)
        text(d, (x + 4, 18), f"{40 + i:02d}", f_grid, 210)
        text(d, (x + 4, H - 22), f"{40 + i:02d}", f_grid, 210)
    for i, y in enumerate(range(80, H, step)):
        d.line((0, y * S, W * S, y * S), fill=95, width=S)
        text(d, (6, y - 18), f"{18 - i:02d}", f_grid, 210)
    # Range rings and a bearing scale round a fire base.
    cx, cy = W * 0.62, H * 0.52
    for r in (150, 300, 450):
        d.ellipse(((cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S), outline=120, width=S)
        text(d, (cx + r * 0.7071 + 6, cy - r * 0.7071 - 6), f"{r * 10 // 150} KM", f_grid, 180)
    for a in range(0, 360, 5):
        ra = math.radians(a)
        r0, r1 = 450, 450 + (16 if a % 30 == 0 else 7)
        d.line(((cx + math.sin(ra) * r0) * S, (cy - math.cos(ra) * r0) * S, (cx + math.sin(ra) * r1) * S, (cy - math.cos(ra) * r1) * S), fill=170, width=S)
        if a % 30 == 0:
            text(d, (cx + math.sin(ra) * 485, cy - math.cos(ra) * 485), f"{a * 6400 // 360:04d}", f_grid, 170, "mm")
    text(d, (cx, cy + 22), "FB TALON", f_grid, 210, "ma")
    d.rectangle(((cx - 6) * S, (cy - 6) * S, (cx + 6) * S, (cy + 6) * S), outline=230, width=2 * S)
    text(d, (W / 2, 50), "37U DQ  //  GRID ZONE DESIGNATOR  //  1:50,000  //  WGS 84", f_big, 200, "mm")
    # North arrow.
    nx, ny = W - 140, 160
    d.polygon([(nx * S, (ny - 50) * S), ((nx + 14) * S, (ny + 10) * S), (nx * S, ny * S), ((nx - 14) * S, (ny + 10) * S)], outline=230, fill=120)
    text(d, (nx, ny - 70), "GN", f_big, 220, "mm")
    return finish(img, "usec_map")


def usec_imagery():
    rng = random.Random(2011)
    img, d = canvas()
    base = noise(W // 2, H // 2, 60, 6, 31)
    fields = noise(W // 2, H // 2, 240, 2, 32)
    tone = (0.45 * base + 0.55 * (np.floor(fields * 5) / 5)) * 110 + 15
    img.paste(Image.fromarray(tone.astype(np.uint8)).resize((W * S, H * S), Image.BICUBIC), (0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(4):
        poly(d, wavy(rng, (rng.uniform(0, W), 0), (rng.uniform(0, W), H), 0.05, 40), 3.0, 175)
    # A compound or two: buildings in rows.
    for _ in range(5):
        bx, by = rng.uniform(200, W - 300), rng.uniform(150, H - 250)
        for i in range(rng.randint(4, 10)):
            ox, oy = bx + (i % 4) * 46 + rng.uniform(-4, 4), by + (i // 4) * 40 + rng.uniform(-4, 4)
            d.rectangle((ox * S, oy * S, (ox + rng.uniform(22, 36)) * S, (oy + rng.uniform(16, 28)) * S), fill=205)
    f_tag = font("bahnschrift.ttf", 16)
    f_meta = font("consola.ttf", 15)
    # Analysts' boxes, with their measurements.
    for k in range(6):
        x, y = rng.uniform(150, W - 350), rng.uniform(140, H - 220)
        w, h = rng.uniform(80, 180), rng.uniform(60, 130)
        corner = 16
        for (px, py, sx, sy) in ((x, y, 1, 1), (x + w, y, -1, 1), (x, y + h, 1, -1), (x + w, y + h, -1, -1)):
            d.line((px * S, py * S, (px + sx * corner) * S, py * S), fill=250, width=2 * S)
            d.line((px * S, py * S, px * S, (py + sy * corner) * S), fill=250, width=2 * S)
        text(d, (x, y - 8), f"TGT {k + 1:02d}  //  {rng.choice(['BLDG', 'VEH x3', 'PAX', 'CACHE?', 'ARTY POS', 'COMMS'])}", f_tag, 250, "ld")
        poly(d, [(x, y + h + 14), (x + w, y + h + 14)], 1.2, 220)
        text(d, (x + w / 2, y + h + 18), f"{int(w * 0.31)} M", f_meta, 220, "ma")
    # Centre reticle and the metadata strip.
    cx, cy = W / 2, H / 2
    for a in range(4):
        ra = math.radians(a * 90)
        d.line(((cx + math.cos(ra) * 30) * S, (cy + math.sin(ra) * 30) * S, (cx + math.cos(ra) * 90) * S, (cy + math.sin(ra) * 90) * S), fill=230, width=2 * S)
    text(d, (40, 40), "IMG 26305-114  //  2026-11-01T14:32:07Z  //  GSD 0.31 M  //  CLOUD 4%  //  OFF-NADIR 12.4", f_meta, 230)
    text(d, (40, H - 50), "SECRET//REL TO USEC  //  IMAGERY ANALYSIS CELL  //  DERIVED FROM: MULTIPLE SOURCES", f_meta, 230)
    return finish(img, "usec_imagery")


OPORD = [
    ("1. SITUATION.", ["a. Enemy forces. Hostile elements of up to platoon strength hold the crossroads and the", "   high ground to the north. Expect small arms, RPG, and dismounted patrols at last light.",
                       "b. Friendly forces. Adjacent teams screen the eastern flank. QRF on 15 minute notice.", "c. Attachments and detachments. None."]),
    ("2. MISSION.", ["Team 2 conducts a raid NLT 0400 to seize OBJ RAVEN in order to recover sensitive items", "and deny their use by hostile forces."]),
    ("3. EXECUTION.", ["a. Concept of operations. Infiltrate on foot along AXIS IRON. Assault from the south.", "b. Tasks to subordinate units. Support element occupies SBF 1 and isolates the objective.",
                       "c. Coordinating instructions. PL DAGGER is the line of departure. Report each phase line.", "   No fires within 300 m of the objective without approval."]),
    ("4. SUSTAINMENT.", ["Basic load plus one. Casualties to the CCP at the release point; CASEVAC by ground", "from HLZ FALCON."]),
    ("5. COMMAND AND SIGNAL.", ["Command: team leader with the assault element. Signal: primary SATCOM, alternate VHF.", "Challenge and password per the SOI. Brevity: EXFIL on GRANITE."]),
]


def usec_document():
    rng = random.Random(2019)
    img, d = canvas()
    f_body = font("consola.ttf", 17)
    f_head = font("bahnschrift.ttf", 22)
    f_banner = font("bahnschrift.ttf", 18)
    page_w, page_h = 860, 1000
    for k in range(3):
        cx = (k + 0.5) * W / 3
        page = Image.new("L", (page_w * S, page_h * S), 0)
        pd = ImageDraw.Draw(page)
        pd.rectangle((0, 0, page_w * S - 1, page_h * S - 1), outline=140, width=2 * S)
        for by in (16, page_h - 44):
            pd.rectangle((0, by * S, page_w * S, (by + 28) * S), fill=70)
            pd.text((page_w / 2 * S, (by + 14) * S), "SECRET//REL TO USEC", font=f_banner, fill=250, anchor="mm")
        y = 80
        pd.text((50 * S, y * S), f"OPERATION ORDER {rng.randint(1, 30):02d}-26  (OPERATION GRANITE)", font=f_head, fill=240)
        y += 40
        pd.text((50 * S, y * S), "References: Map sheet 4 I, 1:50,000.   Time zone used throughout: ZULU.", font=f_body, fill=190)
        y += 46
        for head, lines in OPORD[k % 2:] + OPORD[:k % 2]:
            if y > page_h - 120:
                break
            pd.text((50 * S, y * S), head, font=f_body, fill=240)
            y += 28
            for line in lines:
                if y > page_h - 100:
                    break
                pd.text((50 * S, y * S), line, font=f_body, fill=200)
                # Some of it redacted.
                if rng.random() < 0.18:
                    rx = rng.uniform(120, 500)
                    pd.rectangle((rx * S, (y + 2) * S, (rx + rng.uniform(80, 220)) * S, (y + 20) * S), fill=230)
                y += 25
            y += 14
        x0, y0 = int(cx * S - page.width / 2), int(H / 2 * S - page.height / 2)
        region = img.crop((x0, y0, x0 + page.width, y0 + page.height))
        img.paste(Image.fromarray(np.maximum(np.asarray(region), np.asarray(page))), (x0, y0))
    return finish(img, "usec_document")


def preview(images):
    sheet = Image.new("RGB", (W // 3 * 2, H // 3 * 3), (6, 8, 12))
    for i, a in enumerate(images):
        rgb = Image.new("RGB", (W, H), (6, 8, 12))
        tint = Image.new("RGB", (W, H), (200, 60, 50) if i < 3 else (70, 150, 255))
        rgb.paste(tint, (0, 0), Image.fromarray(a[:, :, 3]))
        sheet.paste(rgb.resize((W // 3, H // 3)), ((i // 3) * (W // 3), (i % 3) * (H // 3)))
    sheet.save(os.path.join(ROOT, "art", "backdrops.png"))


def main():
    images = [bear_map(), bear_imagery(), bear_document(), usec_map(), usec_imagery(), usec_document()]
    preview(images)


if __name__ == "__main__":
    main()
