"""
The 1.1.0 armoury: everything the debrief table can show beyond the hand-picked items in
make_weapons.py's ITEMS, chosen from SPT's database by rule, and which units show it.

    python -I scripts/armory.py H:\\SPT4.1.X        (prints the list per unit, and the expected size)

make_weapons.py builds what this chooses; Hologram.Pools.cs (generated) carries the lists.

What is chosen:
- Every weapon the game has a default preset for, but not event or test ones, colour twins of
  one already chosen (FDE, black, tiger), flare guns, under-barrel launchers or turrets.
- Its named presets (the game's own builds, "MP5 // SD"), event ones aside, as extra builds.
- Equipment of the kinds the table already shows (helmets, armour, rigs, packs, headsets,
  night vision), and new kinds: keys, keycards, ammunition, grenades, cases, face covers,
  eyewear, optics, suppressors and barter goods. Where many items share one model (227 keys,
  22 models), one stands for them all.
- Ammunition: one round per calibre, the one with the best penetration.

Where each goes: weapons by maker (Soviet and Russian makers to BEAR, the rest to USEC), then by
kind to the unit whose work it fits. Equipment by name where its maker shows (6B, Ratnik,
PSO...), else to both sides. Boss gear (Killa, Tagilla, the Goons, Zryachiy, Sanitar, Reshala)
goes to both sides' command.
"""

import collections
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# The units, by the names Theme.cs gives them.
BEAR_UNITS = ("BearCommand", "BearReconnaissance", "BearSignals", "BearCounterintelligence", "BearSupply", "BearMedical")
USEC_UNITS = ("UsecOperations", "UsecIntelligence", "UsecSignals", "UsecLogistics", "UsecMedical", "UsecCommand")

WEAPON_CLASSES = ("AssaultRifle", "AssaultCarbine", "Smg", "Pistol", "Revolver", "Shotgun", "SniperRifle",
                  "MarksmanRifle", "MachineGun", "GrenadeLauncher", "RocketLauncher")

# Makers of Soviet and Russian weapons (as make_weapons.SOVIET, without the prefix).
SOVIET_MAKERS = ("izhmash", "kalashnikov", "molot", "toz", "zid", "zis", "tochmash", "kbp", "ckib", "zmz", "izhmeh",
                 "izhmekh", "kiba", "orsis", "lobaev", "degtyarev", "tokarev", "bazalt")
# (SAG's AK-545 is Russian-built but already on USEC's table: its short sibling follows it.)

# What each kind of weapon is for, on each side.
WEAPON_UNIT = {
    "bear": {"AssaultRifle": "BearCommand", "AssaultCarbine": "BearSignals", "Smg": "BearCounterintelligence",
             "Pistol": "BearCounterintelligence", "Revolver": "BearCommand", "Shotgun": "BearMedical",
             "SniperRifle": "BearReconnaissance", "MarksmanRifle": "BearReconnaissance", "MachineGun": "BearSupply",
             "GrenadeLauncher": "BearSupply", "RocketLauncher": "BearSupply"},
    "usec": {"AssaultRifle": "UsecOperations", "AssaultCarbine": "UsecSignals", "Smg": "UsecMedical",
             "Pistol": "UsecCommand", "Revolver": "UsecCommand", "Shotgun": "UsecLogistics",
             "SniperRifle": "UsecIntelligence", "MarksmanRifle": "UsecIntelligence", "MachineGun": "UsecLogistics",
             "GrenadeLauncher": "UsecLogistics", "RocketLauncher": "UsecLogistics"},
}

# What each kind of equipment is for: the BEAR unit and the USEC one.
GEAR_UNITS = {
    "helmet": ("BearCommand", "UsecOperations"),
    "armor": ("BearCommand", "UsecOperations"),
    "rig": ("BearSupply", "UsecLogistics"),
    "backpack": ("BearReconnaissance", "UsecLogistics"),
    "headset": ("BearSignals", "UsecSignals"),
    "nvg": ("BearReconnaissance", "UsecIntelligence"),
    "key": ("BearCounterintelligence", "UsecIntelligence"),
    "keycard": ("BearCounterintelligence", "UsecIntelligence"),
    "ammo": ("BearSupply", "UsecLogistics"),
    "grenade": ("BearCommand", "UsecOperations"),
    "container": ("BearSupply", "UsecLogistics"),
    "face": ("BearReconnaissance", "UsecOperations"),
    "eyewear": ("BearReconnaissance", "UsecOperations"),
    "optic": ("BearReconnaissance", "UsecIntelligence"),
    "suppressor": ("BearCounterintelligence", "UsecIntelligence"),
    "tech": ("BearSignals", "UsecSignals"),
    "intel": ("BearCounterintelligence", "UsecIntelligence"),
    "valuable": ("BearSupply", "UsecCommand"),
    "device": ("BearMedical", "UsecMedical"),
}

# Names that show a Soviet or Russian maker, in equipment and attachments.
RUSSIAN_GEAR = re.compile(
    r"6b\d|6sh|ratnik|ssh|zsh|altyn|kiver|lshz|vulkan|korund|gjel|gzhel|redut|zhuk|kirasa|tv1\d|tarzan|azimut|umka|"
    r"bankrobber|sso_|attack2|berkut|wartech|pilgrim|gssh|gsh01|pnv|ryst|rys_t|tor_2|maska|kora|_6b|6h5|"
    r"pso|pk06|okp|kobra|1p\d|ekp|npz|pilad|valday|belomo|posp|zenit|pbs|tgp|dtk|hexagon|rotor|tochmash|izhmash|"
    r"_ak_|_akm|_aks|_ak74|_ak103|_svd|_vss|_val_|_pp19|_sr2|_sr3|_mosin|_sks|_pkm|_kedr|_pm_9|_aps|_tt_|vog|"
    r"f1|rgd|rgo|rgn|zarya|rdg|vpo|saiga|vepr|bars|gpnvg_none|mcs|izhmeh|molot|kiba|ckib", re.I)

WESTERN_GRENADE = re.compile(r"m67|m18|m7290|m430|m406|m441|m576|m381|m386|m433", re.I)

# Boss gear, by template name: shown by both sides' command.
BOSS = re.compile(r"killa|tagilla|zryachi|blackknight|big_pipe|birdeye|sanitar|tt_762x25tt_gold|boss_xe|"
                  r"tarko_(killa|tagilla|reshala|cultist)", re.I)
BOSS_PRESET = re.compile(r"^(knight|bigpipe|birdeye)_|_knight", re.I)

# Never shown: seasonal, test and quest variants, and things that are not gear.
NEVER = re.compile(r"test|event|halloween|xmass|xmas|new[_ ]?year|christmas|2k17|_quest|quest_|_dev\b|development|developer|"
                   r"rsp30|_settings|ussr_pd|flyer|poster|stash|mob_?container|boss_container|cultist_amulet|"
                   # Skins: the hologram draws in one colour, so a skin is its base model again.
                   r"_bp\d|_bp_|arena|twitch|streamer|_r2\b|"
                   # Costume, not kit.
                   r"beard|mustache|santa|dedmoroz|pompon|jack_o_lantern|pumpkin", re.I)
COLOUR_TWIN = re.compile(r"_(fde|blk|black|tan|tiger|coyote|od|olive|khaki|multicam)$", re.I)

# Calibres no weapon on the table fires, or that are flares and turret rounds.
SKIP_CALIBRES = {"Caliber20x1mm", "Caliber725", "Caliber40mmRU", "Caliber30x29", "Caliber26x75", "Caliber127x108",
                 "Caliber127x99"}
RUSSIAN_CALIBRES = {"Caliber545x39", "Caliber762x39", "Caliber762x54R", "Caliber9x39", "Caliber9x18PM", "Caliber9x21",
                    "Caliber762x25TT", "Caliber127x55", "Caliber366TKM", "Caliber23x75"}
NATO_CALIBRES = {"Caliber556x45NATO", "Caliber762x51", "Caliber762x35", "Caliber57x28", "Caliber46x30",
                 "Caliber1143x23ACP", "Caliber86x70", "Caliber9x33R", "Caliber127x33", "Caliber68x51", "Caliber40x46"}

BARTER = {"Electronics": "tech", "Battery": "tech", "Info": "intel", "Jewelry": "valuable", "MedicalSupplies": "device"}
OPTICS = ("OpticScope", "AssaultScope", "Collimator", "CompactCollimator", "SpecialScope")


class Db:
    def __init__(self, spt):
        self.streaming = os.path.join(spt, "EscapeFromTarkov_Data", "StreamingAssets", "Windows")
        db = os.path.join(spt, "SPT_Runtime", "SPT_Data", "database")
        self.items = json.load(open(os.path.join(db, "templates", "items.json"), encoding="utf-8"))
        self.globals = json.load(open(os.path.join(db, "globals.json"), encoding="utf-8"))
        self.en = json.load(open(os.path.join(db, "locales", "global", "en.json"), encoding="utf-8"))
        self.ru = json.load(open(os.path.join(db, "locales", "global", "ru.json"), encoding="utf-8"))
        self.presets = collections.defaultdict(list)
        for p in self.globals["ItemPresets"].values():
            root = next(i["_tpl"] for i in p["_items"] if i["_id"] == p["_parent"])
            self.presets[root].append(p)

    def chain(self, key):
        out = []
        node = self.items[key].get("_parent")
        while node and node in self.items:
            out.append(self.items[node]["_name"])
            node = self.items[node].get("_parent")
        return out

    def prefab(self, key):
        return self.items[key]["_props"].get("Prefab", {}).get("path", "")

    def built(self, key):
        """Whether the game ships the item's model."""
        path = self.prefab(key)
        return bool(path) and os.path.exists(os.path.join(self.streaming, path.replace("/", os.sep)))

    def short(self, key):
        return self.en.get(key + " ShortName", self.items[key]["_name"])


def slug(text):
    return re.sub(r"[^a-z0-9]+", "", text.lower())[:18] or "item"


def existing_items():
    """make_weapons.ITEMS as (id, template, kind), read from its source so this runs without its libraries."""
    src = open(os.path.join(ROOT, "scripts", "make_weapons.py"), encoding="utf-8").read()
    block = src[src.index("ITEMS = ["):src.index("\n]\n", src.index("ITEMS = ["))]
    return re.findall(r'\("([^"]+)", "([^"]+)", "([a-z]+)"\)', block)


def gear_side(name):
    return "bear" if RUSSIAN_GEAR.search(name) else "both"


def attachment_side(name):
    return "bear" if RUSSIAN_GEAR.search(name) else "usec"


def named_presets(db, key):
    """
    The weapon's named builds in the game's presets, event ones aside, in a fixed order:
    [(preset id, English name, Russian name, whether a boss carries it)].
    """
    named = []
    for p in sorted(db.presets.get(key, []), key=lambda p: p["_id"]):
        if p.get("_encyclopedia") or NEVER.search(p["_name"]):
            continue
        en = db.en.get(p["_id"], p["_name"])
        if any(x[1] == en for x in named):
            continue  # Knight's SCAR is in there twice, as "Face" both times
        named.append((p["_id"], en, db.ru.get(p["_id"], en), bool(BOSS_PRESET.search(p["_name"]))))
    return named


def catalogue(db):
    """
    The new items: dicts of id, template, kind, side ("bear", "usec" or "both"), units (the
    units that show it as their own), and for weapons the named presets [(preset id, en, ru)].
    """
    old = existing_items()
    taken_ids = {i for i, _, _ in old}
    taken_templates = {t for _, t, _ in old}
    taken_models = set()
    # On a one-colour hologram, two items of one kind and one name are one picture.
    taken_names = set()
    by_name = {v["_name"]: k for k, v in db.items.items()}
    for _, t, k in old:
        if t in by_name:
            taken_models.add(db.prefab(by_name[t]))
            taken_names.add((k, db.short(by_name[t]).lower(), db.items[by_name[t]]["_props"].get("ammoCaliber", "")))
    out = []

    def add(key, kind, side, units, presets=()):
        name = db.items[key]["_name"]
        if name in taken_templates:
            return
        model = db.prefab(key)
        if model in taken_models:
            return
        if NEVER.search(db.short(key)):
            return
        # Weapons by name and calibre: the MDR in 5.56 and in .308 are two rifles.
        shown = (kind, db.short(key).lower(), db.items[key]["_props"].get("ammoCaliber", ""))
        if kind != "ammo" and shown in taken_names:
            return
        iid = slug(db.short(key))
        base, n = iid, 2
        while iid in taken_ids:
            iid, n = f"{base}{n}", n + 1
        taken_ids.add(iid)
        taken_templates.add(name)
        taken_models.add(model)
        taken_names.add(shown)
        out.append({"id": iid, "template": name, "key": key, "kind": kind, "side": side, "units": list(units),
                    "presets": list(presets)})

    def units_for(kind, side, name):
        if BOSS.search(name):
            return ["BearCommand", "UsecCommand"]
        bear, usec = GEAR_UNITS[kind]
        return {"bear": [bear], "usec": [usec], "both": [bear, usec]}[side]

    items = sorted(((k, v) for k, v in db.items.items() if v.get("_type") == "Item"), key=lambda kv: kv[1]["_name"])

    # Weapons first, so a colour twin finds its base already taken.
    names = {v["_name"] for _, v in items}
    for key, v in items:
        chain = db.chain(key)
        cls = next((c for c in chain if c in WEAPON_CLASSES), None)
        name = v["_name"]
        if not cls or NEVER.search(name) or not db.built(key):
            continue
        if not any(p.get("_encyclopedia") for p in db.presets.get(key, [])):
            continue  # turrets, and weapons the game has no build of
        if COLOUR_TWIN.search(name) and COLOUR_TWIN.sub("", name) in names:
            continue
        maker = name.split("_")[1] if name.startswith("weapon_") else ""
        side = "bear" if maker in SOVIET_MAKERS else "usec"
        unit = WEAPON_UNIT[side][cls]
        if v["_props"].get("ammoCaliber") == "Caliber9x39":
            unit = "BearCounterintelligence"
        add(key, "weapon", side, [unit], named_presets(db, key))

    # Equipment of the kinds already shown.
    for key, v in items:
        chain = db.chain(key)
        name = v["_name"]
        if NEVER.search(name) or not db.built(key):
            continue
        p = v["_props"]
        kind = None
        if "Headwear" in chain:
            # Armour of its own or armour parts in its slots; caps and hats aren't kit.
            armoured = int(p.get("armorClass") or 0) > 0 or any(sl["_name"].lower().startswith("helmet_") for sl in p.get("Slots", []))
            kind = "helmet" if armoured or BOSS.search(name) else None
        elif "Armor" in chain and "BuiltInInserts" not in chain and "ArmorPlate" not in chain:
            kind = "armor"
        elif "Vest" in chain:
            kind = "rig"
        elif "Backpack" in chain:
            kind = "backpack"
        elif "Headphones" in chain:
            kind = "headset"
        elif "NightVision" in chain or "ThermalVision" in chain:
            kind = "nvg"
        elif "Keycard" in chain:
            kind = "keycard"
        elif "KeyMechanical" in chain:
            kind = "key"
        elif "ThrowWeap" in chain:
            kind = "grenade"
        elif ("SimpleContainer" in chain or "LockableContainer" in chain) and p.get("Grids"):
            kind = "container"
        elif "FaceCover" in chain:
            kind = "face"
        elif "Visors" in chain:
            kind = "eyewear"
        elif chain and chain[0] in OPTICS:
            kind = "optic"
        elif "Silencer" in chain:
            kind = "suppressor"
        elif chain and chain[0] in BARTER:
            kind = BARTER[chain[0]]
        if not kind:
            continue
        if kind == "grenade":
            side = "usec" if WESTERN_GRENADE.search(name) else "bear"
        elif kind in ("optic", "suppressor"):
            side = attachment_side(name)
        elif kind in ("key", "keycard", "container", "tech", "intel", "valuable", "device"):
            side = "both"
        else:
            side = gear_side(name)
        if BOSS.search(name):
            side = "both"
        add(key, kind, side, units_for(kind, side, name))

    # Ammunition: per calibre, the round that goes through the most.
    best = {}
    for key, v in items:
        chain = db.chain(key)
        if "Ammo" not in chain or "AmmoBox" in chain or NEVER.search(v["_name"]) or not db.built(key):
            continue
        cal = v["_props"].get("Caliber")
        if not cal or cal in SKIP_CALIBRES:
            continue
        pen = v["_props"].get("PenetrationPower", 0)
        if cal not in best or pen > best[cal][0]:
            best[cal] = (pen, key)
    for cal, (_, key) in sorted(best.items()):
        side = "bear" if cal in RUSSIAN_CALIBRES else "usec" if cal in NATO_CALIBRES else "both"
        add(key, "ammo", side, units_for("ammo", side, ""))
    return out


def report(db, new):
    old = existing_items()
    weapons = [e for e in new if e["kind"] == "weapon"]
    gear = [e for e in new if e["kind"] != "weapon"]
    print(f"Today: {len(old)} items ({sum(1 for _, _, k in old if k == 'weapon')} weapons).")
    print(f"New: {len(weapons)} weapons, {len(gear)} pieces of equipment, "
          f"{sum(len(e['presets']) for e in weapons)} named builds on new weapons.")
    print()
    print("New equipment by kind:")
    for kind, n in collections.Counter(e["kind"] for e in gear).most_common():
        print(f"  {kind:11} {n}")
    print()

    # Named presets on the weapons already in the pool.
    by_name = {v["_name"]: k for k, v in db.items.items()}
    old_named = 0
    for _, t, k in old:
        if k == "weapon" and t in by_name:
            old_named += sum(1 for p in db.presets.get(by_name[t], []) if not p.get("_encyclopedia") and not NEVER.search(p["_name"]))
    print(f"Named builds on weapons already in the pool: {old_named}")
    print()

    for side, units in (("BEAR", BEAR_UNITS), ("USEC", USEC_UNITS)):
        print(f"===== {side} =====")
        lower = side.lower()
        for unit in units:
            mine = [e for e in new if unit in e["units"]]
            ws = [db.short(e["key"]) + (f" (+{len(e['presets'])} named)" if e["presets"] else "") for e in mine if e["kind"] == "weapon"]
            print(f"--- {unit}: {len(ws)} new weapons, {len(mine) - len(ws)} new equipment")
            if ws:
                print("    weapons: " + ", ".join(ws))
            kinds = collections.defaultdict(list)
            for e in mine:
                if e["kind"] != "weapon":
                    kinds[e["kind"]].append(db.short(e["key"]))
            for kind, names in sorted(kinds.items()):
                shown = ", ".join(names[:14]) + (f", ... ({len(names)} in all)" if len(names) > 14 else "")
                print(f"    {kind}: {shown}")
        rest = [e for e in new if e["side"] in (lower, "both")]
        print(f"    (the side's whole table, every unit can draw from it: {len(rest)} new items)")
        print()

    boss = [e for e in new if BOSS.search(e["template"])]
    print("Boss gear: " + ", ".join(db.short(e["key"]) for e in boss))
    boss_builds = []
    for _, t, k in old:
        if k == "weapon" and t in by_name:
            boss_builds += [db.en.get(p["_id"], p["_name"]) + " (" + db.short(by_name[t]) + ")"
                            for p in db.presets.get(by_name[t], []) if BOSS_PRESET.search(p["_name"])]
    for e in weapons:
        boss_builds += [en + " (" + db.short(e["key"]) + ")" for _, en, _, b in e["presets"] if b]
    print("Boss builds: " + ", ".join(boss_builds))
    print()

    # Size: from what today's models weigh.
    res = os.path.join(ROOT, "src", "CLASSIFIED", "Resources")
    sizes = collections.defaultdict(list)
    kinds = {i: k for i, _, k in old}
    for f in os.listdir(res):
        if f.startswith("item_") and f.endswith(".bin"):
            base = re.sub(r"_\d+$", "", f[5:-4])
            sizes["weapon" if kinds.get(base) == "weapon" else "gear"].append(os.path.getsize(os.path.join(res, f)))
    w = sum(sizes["weapon"]) / max(1, len(sizes["weapon"]))
    g = sum(sizes["gear"]) / max(1, len(sizes["gear"]))
    old_weapons = sum(1 for _, _, k in old if k == "weapon")
    builds = 12
    extra = ((len(weapons) * builds + old_weapons * (builds - 8)) * w
             + (sum(len(e["presets"]) for e in weapons) + old_named) * w + len(gear) * g)
    total = sum(os.path.getsize(os.path.join(res, f)) for f in os.listdir(res))
    print(f"Models today: {total / 1e6:.1f} MB (a weapon build {w / 1024:.0f} KB, equipment {g / 1024:.0f} KB).")
    print(f"Added: about {extra / 1e6:.0f} MB, so about {(total + extra) / 1e6:.0f} MB in the DLL.")


def main():
    spt = sys.argv[1] if len(sys.argv) > 1 else r"H:\SPT4.1.X"
    db = Db(spt)
    report(db, catalogue(db))


if __name__ == "__main__":
    main()
