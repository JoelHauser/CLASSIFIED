"""
The mod page's order of battle: each unit's title card, as the terminal shows it when it signs in.

Twelve landscape tiles, BEAR's six over USEC's six, each in its unit's own colours from the game
(Theme.cs) over its own backdrop from the game: the net's header line, the faction insignia from
the game's own pieces in its frame, the unit's name (BEAR's in Russian, with the English), its
motto, what it puts on the table, and the finished load bar.

    python -I scripts/make_units.py            docs/images/units.png

Needs numpy and pillow.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import art_kit as kit  # noqa: E402

OUT = os.path.join(kit.ROOT, "docs", "images", "units.png")
SS = 2

TILE_W, TILE_H = 500, 250
GAP, MARGIN, FACTION_GAP = 14, 0, 26

# unit, name, its English (or the next name it goes by), motto, what it puts on the table, backdrop
BEAR_UNITS = [
    ("BearCommand", "ШТАБ", "COMMAND POST", "ПРИКАЗ ЕСТЬ ПРИКАЗ", "ПЛАН НАСТУПЛЕНИЯ  //  ASSAULT PLANS", "map"),
    ("BearReconnaissance", "РАЗВЕДКА", "RECONNAISSANCE", "ВИЖУ ВСЁ", "БПЛА, АЭРОСЪЁМКА  //  DRONE IMAGERY", "imagery"),
    ("BearSignals", "СВЯЗЬ", "SIGNALS", "СВЯЗЬ ЕСТЬ", "ПЕЛЕНГАЦИЯ, ПЕРЕХВАТ  //  INTERCEPTS", "map"),
    ("BearCounterintelligence", "КОНТРРАЗВЕДКА", "COUNTERINTELLIGENCE", "ДОВЕРЯЙ, НО ПРОВЕРЯЙ", "ЛИЧНЫЕ ДЕЛА  //  PERSONNEL FILES", "document"),
    ("BearSupply", "ТЫЛ", "SUPPLY", "БЕЗ ТЫЛА НЕТ ФРОНТА", "КОЛОННЫ, СНАБЖЕНИЕ  //  CONVOY ROUTES", "map"),
    ("BearMedical", "МЕДСЛУЖБА", "MEDICAL SERVICE", "СВОИХ НЕ БРОСАЕМ", "ЭВАКУАЦИЯ  //  CASUALTY EVACUATION", "map"),
]
USEC_UNITS = [
    ("UsecOperations", "TACTICAL OPERATIONS CENTER", "S3 OPERATIONS", "PLAN, BRIEF, EXECUTE", "ON THE TABLE  //  ASSAULT PLANS", "map"),
    ("UsecIntelligence", "S2 INTELLIGENCE", "ALL-SOURCE ANALYSIS", "KNOW BEFORE YOU GO", "ON THE TABLE  //  AERIAL IMAGERY", "imagery"),
    ("UsecSignals", "S6 COMMUNICATIONS", "NETWORK OPERATIONS", "SAY AGAIN, OVER", "ON THE TABLE  //  RADIO INTERCEPTS", "map"),
    ("UsecLogistics", "S4 LOGISTICS", "SUPPORT OPERATIONS", "BEANS, BULLETS, BANDAGES", "ON THE TABLE  //  CONVOY ROUTES", "map"),
    ("UsecMedical", "MEDICAL // CASEVAC", "ROLE 1 AID STATION", "SO THAT OTHERS MAY LIVE", "ON THE TABLE  //  CASUALTY EVACUATION", "map"),
    ("UsecCommand", "CORPORATE COMMAND", "CONTRACT OVERSIGHT", "DISCRETION GUARANTEED", "ON THE TABLE  //  PERSONNEL FILES", "document"),
]


# USEC's sections by their staff codes, for the header line.
CODES = {"UsecOperations": "S3 // TOC", "UsecIntelligence": "S2", "UsecSignals": "S6", "UsecLogistics": "S4",
         "UsecMedical": "CASEVAC", "UsecCommand": "COMMAND"}


def mix(a, b, k):
    return tuple(int(round(a[i] + (b[i] - a[i]) * k)) for i in range(3))


def fit(draw, text, size, width, weight=None):
    while size > 8:
        f = kit.font(size, weight)
        if draw.textlength(text, font=f) <= width:
            return f
        size -= 1
    return kit.font(8, weight)


def tile(faction, entry, theme, insignia):
    unit, name, second, motto, table, backdrop = entry
    s = SS
    w, h = TILE_W * s, TILE_H * s
    ink, primary, accent, text, dim = theme["ink"], theme["primary"], theme["accent"], theme["text"], theme["dim"]

    # The unit's ink, its own backdrop from the game tinted in its colour, lit from the insignia's side.
    tex = Image.open(os.path.join(kit.RES, f"backdrop_{faction}_{backdrop}.png")).convert("L")
    cover = max(w / tex.width, h / tex.height)
    tex = tex.resize((int(tex.width * cover) + 1, int(tex.height * cover) + 1), Image.LANCZOS).crop((0, 0, w, h))
    t = np.asarray(tex).astype(np.float32) / 255.0
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    lift = np.clip(1 - np.sqrt(((xx - 105 * s) / (w * 0.7)) ** 2 + ((yy - 130 * s) / (h * 0.9)) ** 2), 0, 1)
    arr = np.array(ink, np.float32) + (t * 0.16)[..., None] * np.array(primary, np.float32) + (lift ** 2 * 0.14)[..., None] * np.array(primary, np.float32)
    arr *= (0.95 + 0.05 * (np.arange(h) % (3 * s) >= s))[:, None, None]
    img = kit.to_img(arr).convert("RGBA")
    d = ImageDraw.Draw(img)

    # The net's header line, as at the top of the terminal.
    hf = kit.font(11 * s, "SemiBold")
    left = f"BEAR  //  {name}" if faction == "bear" else f"USEC  //  {CODES[unit]}"
    right = "СОВ. СЕКРЕТНО" if faction == "bear" else "SECRET//REL USEC"
    d.text((14 * s, 15 * s), left, font=hf, fill=dim + (255,), anchor="lm")
    d.text((w - 14 * s, 15 * s), right, font=hf, fill=mix(dim, primary, 0.5) + (255,), anchor="rm")
    d.line([(14 * s, 27 * s), (w - 14 * s, 27 * s)], fill=mix(ink, primary, 0.3) + (255,), width=s)

    # The insignia in its frame, with its glow.
    cx, cy, size = 102 * s, 136 * s, 118 * s
    m = insignia
    scale = size / m.shape[0]
    mi = Image.fromarray((m * 255).astype(np.uint8)).resize((int(m.shape[1] * scale), int(size)), Image.LANCZOS)
    glow = Image.new("RGBA", mi.size, primary + (0,))
    glow.putalpha(mi.filter(ImageFilter.GaussianBlur(12 * s)).point(lambda v: int(v * 0.4)))
    logo = Image.new("RGBA", mi.size, primary + (255,))
    logo.putalpha(mi)
    at = (int(cx - mi.width / 2), int(cy - mi.height / 2))
    img.alpha_composite(glow, at)
    img.alpha_composite(logo, at)
    half, arm = 78 * s, 22 * s
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = cx + sx * half, cy + sy * half
            d.line([(x, y), (x - sx * arm, y)], fill=primary + (210,), width=2 * s)
            d.line([(x, y), (x, y - sy * arm)], fill=primary + (210,), width=2 * s)

    # Name, the English or second name, motto, what is on the table.
    x0, col_w = 210 * s, w - 224 * s
    nf = fit(d, name, 30 * s, col_w, "Bold")
    d.text((x0, 66 * s), name, font=nf, fill=text + (255,), anchor="lm")
    d.text((x0, 92 * s), second, font=fit(d, second, 13 * s, col_w, "SemiBold"), fill=dim + (255,), anchor="lm")
    quoted = f"«{motto}»" if faction == "bear" else f"“{motto}”"
    d.text((x0, 122 * s), quoted, font=fit(d, quoted, 15 * s, col_w, "SemiBold"), fill=accent + (255,), anchor="lm")
    d.text((x0, 150 * s), table, font=fit(d, table, 11 * s, col_w), fill=dim + (255,), anchor="lm")

    # The finished load bar, in the terminal's segments.
    by, seg_gap, n = 196 * s, 3 * s, 15
    seg_w = (col_w - seg_gap * (n - 1)) / n
    for i in range(n):
        x = x0 + i * (seg_w + seg_gap)
        d.rectangle([x, by, x + seg_w, by + 6 * s], fill=primary + (255,))
    sf = kit.font(10 * s, "SemiBold")
    d.text((x0, by + 20 * s), "ЭТАП 15 / 15" if faction == "bear" else "STEP 15 / 15", font=sf, fill=dim + (255,), anchor="lm")
    d.text((x0 + col_w, by + 20 * s), "ДОСТУП РАЗРЕШЁН" if faction == "bear" else "SIGNED IN", font=sf, fill=accent + (255,), anchor="rm")

    # Rounded, with a hairline in the unit's colour.
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1], radius=10 * s, fill=255)
    img.putalpha(mask)
    ImageDraw.Draw(img).rounded_rectangle([0, 0, w - 1, h - 1], radius=10 * s, outline=mix(ink, primary, 0.45) + (255,), width=s)
    return img.resize((TILE_W, TILE_H), Image.LANCZOS)


def main():
    themes = kit.load_themes()
    cols = 3
    width = cols * TILE_W + (cols - 1) * GAP
    height = 4 * TILE_H + 2 * GAP + FACTION_GAP
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    y = 0
    for faction, units in (("bear", BEAR_UNITS), ("usec", USEC_UNITS)):
        insignia = kit.insignia(faction, 140 * SS)
        for i, entry in enumerate(units):
            r, c = divmod(i, cols)
            sheet.alpha_composite(tile(faction, entry, themes[entry[0]], insignia), (c * (TILE_W + GAP), y + r * (TILE_H + GAP)))
        y += 2 * TILE_H + GAP + FACTION_GAP
    sheet.save(OUT, optimize=True)
    print(OUT, sheet.size, f"{os.path.getsize(OUT) / 1e3:.0f} KB")


if __name__ == "__main__":
    main()
