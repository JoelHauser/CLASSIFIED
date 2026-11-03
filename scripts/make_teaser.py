"""
A short teaser video for CLASSIFIED, silent, for a music bed to go under.

It says what the mod is at once: the game's plain loading text tears away to the terminal, then
quick cuts of what the terminal does (both factions, the 3D weapons, the real load steps, the
twelve units) and the title. Everything is drawn from the mod's own art, models and colours; it
is a trailer made of them, not a recording of the game. For that, use the Showreel in game.

    python -I scripts/make_teaser.py            dist/CLASSIFIED-teaser.mp4
    python -I scripts/make_teaser.py --still    one frame per scene as PNGs in the system temp folder

Needs numpy, pillow and ffmpeg on the path.
"""
import math
import os
import shutil
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import art_kit as kit  # noqa: E402
import make_showcase as show  # noqa: E402
import make_title as title  # noqa: E402
import make_units as units  # noqa: E402

OUT = os.path.join(kit.ROOT, "dist", "CLASSIFIED-teaser.mp4")
W, H = 1920, 1080
FPS = 30

TEXT = (236, 240, 245)
BEAR, USEC = title.BEAR, title.USEC

# The scenes, in order: name and length in seconds.
SCENES = [("plain", 2.4), ("factions", 3.0), ("weapons", 5.4), ("readout", 3.2), ("unit_cards", 4.4), ("end", 5.6)]

# The weapon each cut shows, and the unit whose colours it is in.
WEAPONS = [("ak74m_5", "BearCommand"), ("hk416_3", "UsecOperations"), ("svds_2", "BearReconnaissance"),
           ("mpx_4", "UsecIntelligence"), ("m60_3", "UsecLogistics"), ("val_1", "BearSignals")]

STEPS = ["VERIFYING GAME FILES", "MOUNTING ASSET BUNDLES", "BUILDING ITEM DATABASE", "LOADING PROFILES",
         "LOADING HANDBOOK", "LOADING WEAPON BUILDS", "LOADING WEATHER AND MAPS", "OPENING MAIN MENU"]


def smooth(p):
    p = min(1.0, max(0.0, p))
    return p * p * (3.0 - 2.0 * p)


def ease_out(p):
    p = min(1.0, max(0.0, p))
    return 1.0 - (1.0 - p) ** 3


class Canvas:
    """The size and word positions make_title's drawing asks of a layout."""
    w, h = W, H
    word_y, word_size, track = 470, 150, 44
    sub_y, sub_size = 590, 30


class Teaser:
    def __init__(self):
        show.load_themes()
        self.themes = kit.load_themes()
        self.specs = show.load_specs()
        self.base = title.background(Canvas)
        self.rng = np.random.default_rng(8)
        self.pieces = {"bear": title.piece_images("bear", 215, BEAR), "usec": title.piece_images("usec", 215, USEC)}
        self.split = self.split_ground()
        self.holos = []
        for iid, unit in WEAPONS:
            theme = self.themes[unit]
            colour = tuple(min(255, int(c * 0.65 + a * 0.35)) for c, a in zip(theme["primary"], theme["accent"]))
            self.holos.append((title.Hologram(iid, colour, 1200), iid.rsplit("_", 1)[0], unit, theme))
        # The unit cards, large, in the order of the mod page's sheet, and the sheet itself.
        old_ss = units.SS
        units.SS = 4
        self.cards = []
        for faction, entries in (("bear", units.BEAR_UNITS), ("usec", units.USEC_UNITS)):
            insig = kit.insignia(faction, 140 * units.SS)
            for entry in entries:
                self.cards.append(units.tile(faction, entry, self.themes[entry[0]], insig, final=(1250, 625)))
        units.SS = old_ss
        # BEAR and USEC in turn, so each cut changes side.
        self.cards = [c for pair in zip(self.cards[:6], self.cards[6:]) for c in pair]
        self.sheet = Image.open(os.path.join(kit.ROOT, "docs", "images", "units.png")).convert("RGBA")

    def split_ground(self):
        xx = np.linspace(0, 1, W)[None, :]
        yy = np.linspace(0, 1, H)[:, None]
        k = np.clip((xx - 0.5) * 40 + 0.5, 0, 1)[..., None]
        arr = np.array([60, 10, 8], np.float32) * (1 - k) + np.array([6, 22, 52], np.float32) * k
        d = np.sqrt((xx - 0.5) ** 2 + (yy - 0.5) ** 2)
        arr = arr * np.clip(1.25 - d * 1.1, 0.3, 1)[..., None]
        arr *= (0.94 + 0.06 * (np.arange(H) % 4 != 0))[:, None, None]
        return kit.to_img(arr)

    # ---- shared -------------------------------------------------------------------------------

    def caption(self, img, text, t, y=930, size=44):
        """A line on a black bar that wipes open from the left, like a redaction coming off."""
        d = ImageDraw.Draw(img)
        f = kit.font(size, "Bold")
        tw = d.textlength(text, font=f)
        k = ease_out(t / 0.22)
        bar = (tw + 70) * k
        x0 = (W - tw - 70) / 2
        d.rectangle([x0, y - size * 0.95, x0 + bar, y + size * 0.95], fill=(0, 0, 0))
        if k > 0.6:
            d.text((x0 + 35, y), text, font=f, fill=TEXT, anchor="lm")

    def tear(self, img, amount):
        """The signal breaking: bands slide and the colours split."""
        if amount <= 0:
            return img
        arr = np.asarray(img.convert("RGB")).copy()
        for _ in range(int(6 + amount * 24)):
            y = self.rng.integers(0, H)
            h = int(self.rng.integers(6, 60) * (0.4 + amount))
            arr[y:y + h] = np.roll(arr[y:y + h], int(self.rng.integers(-140, 140) * amount), axis=1)
        split = int(3 + amount * 18)
        arr[..., 0] = np.roll(arr[..., 0], split, axis=1)
        arr[..., 2] = np.roll(arr[..., 2], -split, axis=1)
        return Image.fromarray(arr)

    # ---- the scenes ---------------------------------------------------------------------------

    def plain(self, t, length):
        """The game's own wait: small grey text on black, then it breaks."""
        img = Image.new("RGB", (W, H), (0, 0, 0))
        d = ImageDraw.Draw(img)
        d.text((W / 2, H / 2), "Profile data loading...", font=kit.font(30), fill=(150, 150, 150), anchor="mm")
        a = t * 5
        cx, cy, r = W / 2, H / 2 + 60, 14
        d.arc([cx - r, cy - r, cx + r, cy + r], math.degrees(a), math.degrees(a) + 260, fill=(120, 120, 120), width=3)
        return self.tear(img, smooth((t - (length - 0.55)) / 0.55) * 1.3)

    def factions(self, t, length):
        """Both insignia fly together from their pieces on their own halves of the screen."""
        img = self.split.copy().convert("RGBA")
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        for faction, cx, delay in (("bear", 480, 0.0), ("usec", 1440, 0.3)):
            pieces = self.pieces[faction]
            for i, p in enumerate(pieces):
                k = (t - delay - 0.35 * i / max(1, len(pieces) - 1)) / 0.45
                if k <= 0:
                    continue
                dist = (1 - ease_out(k)) * 3.0 * 215
                px = cx + p["centre"][0] * 215 + p["dir"][0] * dist
                py = 470 - p["centre"][1] * 215 - p["dir"][1] * dist
                sprite = p["img"]
                layer.alpha_composite(sprite, (int(round(px - sprite.width / 2)), int(round(py - sprite.height / 2))))
        glow = layer.filter(ImageFilter.GaussianBlur(26))
        glow.putalpha(glow.split()[3].point(lambda v: int(v * 0.8)))
        img.alpha_composite(glow)
        img.alpha_composite(layer)
        if t > 0.95:
            self.caption(img, "YOUR LOADING SCREEN BECOMES YOUR FACTION'S FIELD TERMINAL", t - 0.95, y=900, size=42)
        out = img.convert("RGB")
        return self.tear(out, max(smooth((0.25 - t) / 0.25) * 0.9, smooth((t - (length - 0.2)) / 0.2) * 0.8))

    def weapons(self, t, length):
        """A cut per weapon, each turning on its table in its unit's colours."""
        each = length / len(self.holos)
        i = min(len(self.holos) - 1, int(t / each))
        local = t - i * each
        holo, base, unit, theme = self.holos[i]
        ink = np.array(theme["ink"], np.float32)
        glow = np.array(theme["primary"], np.float32)
        xx = np.linspace(-1, 1, W)[None, :]
        yy = np.linspace(-1, 1, H)[:, None]
        lift = np.clip(1 - np.sqrt(xx ** 2 + (yy + 0.05) ** 2), 0, 1) ** 2
        arr = ink + lift[..., None] * glow * 0.2
        arr = arr * (0.95 + 0.05 * (np.arange(H) % 4 != 0))[:, None, None]
        img = kit.to_img(arr).convert("RGBA")
        d = ImageDraw.Draw(img)
        title.turntable(d, W / 2, 660, 1300, theme["primary"], 1.0)
        # Swaying near side-on: a full turn would pass end-on, where a rifle is a sliver.
        side = 0.0 if i % 2 == 0 else math.pi
        sprite = holo.render(side - 0.42 + 0.84 * local / each, pitch=0.2)
        k = smooth((local + 1.0 / FPS) / 0.1)
        if k < 1:
            sprite.putalpha(sprite.split()[3].point(lambda v, s=k: int(v * s)))
        img.alpha_composite(sprite, (int(W / 2 - sprite.width / 2), int(470 - sprite.height / 2)))
        spec = self.specs[base]
        d = ImageDraw.Draw(img)
        d.text((110, 110), spec["short"], font=kit.font(58, "Bold"), fill=theme["text"], anchor="lm")
        d.text((110, 165), spec["line"], font=kit.font(24, "SemiBold"), fill=theme["dim"], anchor="lm")
        self.caption(img, "NEARLY 80 WEAPONS IN 3D, BUILT FROM THE GAME'S OWN MODELS", t, y=930, size=42)
        return img.convert("RGB")

    def readout(self, t, length):
        """The load: real step names ticking through on a bar."""
        theme = self.themes["UsecOperations"]
        img = self.base.copy().convert("RGBA")
        d = ImageDraw.Draw(img)
        p = smooth(t / (length - 0.4))
        n = len(STEPS)
        current = min(n - 1, int(p * n))
        x0, x1 = 400, 1520
        d.text((x0, 215), f"STEP {1 + int(p * 14.99):02d} / 15", font=kit.font(34), fill=theme["dim"], anchor="lm")
        d.text((x1, 215), f"{int(round(p * 100))}%", font=kit.font(34), fill=theme["text"], anchor="rm")
        segs, gap = 15, 8
        sw = (x1 - x0 - gap * (segs - 1)) / segs
        for i in range(segs):
            x = x0 + i * (sw + gap)
            on = i < p * segs
            d.rectangle([x, 250, x + sw, 272], fill=theme["primary"] if on else (24, 34, 48))
        d.text((x0, 345), "> " + STEPS[current], font=kit.font(62, "SemiBold"), fill=theme["text"], anchor="lm")
        d.text((x1, 345), f"{(p * n - current) * 3.1:0.1f}s", font=kit.font(46), fill=theme["text"], anchor="rm")
        y = 445
        times = ["0.4s", "5.8s", "8.9s", "0.6s", "2.7s", "0.2s", "1.1s"]
        for i in range(current - 1, max(-1, current - 6), -1):
            d.text((x0, y), "COMPLETE", font=kit.font(32), fill=theme["accent"], anchor="lm")
            d.text((x0 + 230, y), STEPS[i], font=kit.font(32), fill=theme["dim"], anchor="lm")
            d.text((x1, y), times[i], font=kit.font(32), fill=theme["dim"], anchor="rm")
            y += 58
        self.caption(img, "SEE WHAT THE GAME IS ACTUALLY LOADING, TIMED AGAINST YOUR OWN PC", t, y=900, size=42)
        return img.convert("RGB")

    def unit_cards(self, t, length):
        """Twelve units in quick cuts, BEAR and USEC in turn, ending on all of them."""
        sheet_at = length - 1.3
        img = Image.new("RGBA", (W, H), (4, 5, 8, 255))
        if t < sheet_at:
            i = min(len(self.cards) - 1, int(t / (sheet_at / len(self.cards))))
            card = self.cards[i]
            img.alpha_composite(card, (int(W / 2 - card.width / 2), int(440 - card.height / 2)))
        else:
            k = ease_out((t - sheet_at) / 0.4)
            scale = 0.8 + 0.03 * (1 - k)
            s = self.sheet.resize((int(self.sheet.width * scale), int(self.sheet.height * scale)), Image.LANCZOS)
            img.alpha_composite(s, (int(W / 2 - s.width / 2), int(450 - s.height / 2)))
        self.caption(img, "12 UNITS, SIX A SIDE. A DIFFERENT ONE EVERY LAUNCH", t, y=950, size=42)
        return img.convert("RGB")

    def end(self, t, length):
        """The name comes out from under its redaction bars, and what it is."""
        img = self.base.copy().convert("RGBA")
        words = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        # make_title's wordmark runs on its own clock; this starts it just before the bars move.
        holder = type("Holder", (), {"lay": Canvas})
        title.Scene.wordmark(holder, words, title.WORD_FROM - 0.3 + t * 1.15)
        img.alpha_composite(words)
        d = ImageDraw.Draw(img)
        k = smooth((t - 2.3) / 0.4)
        if k > 0:
            d.text((W / 2, 690), "A LOADING SCREEN MOD FOR SPT 4.1", font=kit.font(46, "SemiBold"), fill=TEXT + (int(255 * k),), anchor="mm")
            d.text((W / 2, 752), "NO GAMEPLAY CHANGES", font=kit.font(30), fill=(150, 160, 176, int(255 * k)), anchor="mm")
        out = img.convert("RGB")
        fade = smooth((t - (length - 0.5)) / 0.5)
        return Image.blend(out, Image.new("RGB", (W, H), (0, 0, 0)), fade) if fade > 0 else out

    def frame(self, t):
        at = 0.0
        for name, length in SCENES:
            if t < at + length:
                return getattr(self, name)(t - at, length)
            at += length
        return Image.new("RGB", (W, H), (0, 0, 0))


def main():
    teaser = Teaser()
    total = sum(length for _, length in SCENES)
    if "--still" in sys.argv:
        tmp = tempfile.gettempdir()
        at = 0.0
        for name, length in SCENES:
            for k in (0.3, 0.8):
                path = os.path.join(tmp, f"classified_teaser_{name}_{int(k * 10)}.png")
                teaser.frame(at + length * k).save(path)
                print(path)
            at += length
        return
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    ffmpeg = shutil.which("ffmpeg")
    proc = subprocess.Popen([ffmpeg, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
                             "-i", "-", "-c:v", "libx264", "-preset", "slow", "-crf", "16", "-pix_fmt", "yuv420p", "-movflags", "+faststart", OUT],
                            stdin=subprocess.PIPE)
    for i in range(int(total * FPS)):
        proc.stdin.write(teaser.frame(i / FPS).tobytes())
    proc.stdin.close()
    proc.wait()
    print(OUT, f"{os.path.getsize(OUT) / 1e6:.1f} MB, {total:.1f} s")


if __name__ == "__main__":
    main()
