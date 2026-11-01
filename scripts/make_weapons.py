"""
Builds the 3D weapons and equipment the debrief hologram turns, from the game's own models.

For each item, its default preset in SPT's database says which parts it is built from and which
slot each sits in (an item without one is its prefab alone). Every part's prefab is read from
the game's bundles (UnityPy), its meshes placed through the slot transforms the way the game
assembles it, and the whole merged, welded and cut down to a few thousand triangles. Written to
src/CLASSIFIED/Resources/item_<id>.bin (see Hologram.cs for the format), with a side view of each
in art/weapons/ to check by eye, and the names and figures to src/CLASSIFIED/Kit.Data.cs.

    python -I scripts/make_weapons.py H:\\SPT4.1.X        (needs numpy, pillow, UnityPy)
"""

import glob
import json
import math
import os
import struct
import sys

import numpy as np
import UnityPy
from PIL import Image, ImageDraw
from UnityPy.helpers.MeshHelper import MeshHandler

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "src", "CLASSIFIED", "Resources")
PREVIEW = os.path.join(ROOT, "art", "weapons")

# id, the item (by template name), and what kind of thing it is. Weapons are built from their
# default preset; equipment from its own when it has one (armour, with its plates), else from
# its prefab alone.
ITEMS = [
    ("ak12", "weapon_izhmash_ak12_545x39", "weapon"),
    ("svds", "weapon_izhmash_svd_s_762x54", "weapon"),
    ("aks74u", "weapon_izhmash_aks74u_545x39", "weapon"),
    ("val", "weapon_tochmash_val_9x39", "weapon"),
    ("pkm", "weapon_zid_pkm_762x54r", "weapon"),
    ("ak74m", "weapon_izhmash_ak74m_545x39", "weapon"),
    ("m4a1", "weapon_colt_m4a1_556x45", "weapon"),
    ("sr25", "weapon_kac_sr25_762x51", "weapon"),
    ("mcx", "weapon_sig_mcx_gen1_762x35", "weapon"),
    ("m60", "weapon_usord_m60e4_762x51", "weapon"),
    ("mp5", "weapon_hk_mp5_navy3_9x19", "weapon"),
    ("hk416", "weapon_hk_416a5_556x45", "weapon"),
    ("6b47", "helmet_ratnik_6B47", "helmet"),
    ("altyn", "helmet_altyn", "helmet"),
    ("ssh68", "item_equipment_helmet_ssh-68", "helmet"),
    ("fast", "helmet_ops_core_fast", "helmet"),
    ("airframe", "item_equipment_helmet_crye_airframe_tan", "helmet"),
    ("exfil", "item_equipment_helmet_team_wendy_exfil_coyote", "helmet"),
    ("6b43", "heavy", "armor"),
    ("6b13", "item_equipment_armor_6b13_digital", "armor"),
    ("avs", "standartRig", "armor"),
    ("slick", "item_equipment_armor_slick_tan", "armor"),
    ("pnv10t", "nvg_alfa_pnv-10t", "nvg"),
    ("gpnvg", "nvg_l3_gpnvg-18_anvis", "nvg"),
    ("pvs14", "nvg_pvs_14", "nvg"),
    ("gssh01", "item_equipment_headset_gsh01", "headset"),
    ("comtac", "item_equipment_headset_comtac_IV", "headset"),
    ("6sh118", "backpack_Raid_6SH118", "backpack"),
    ("blackjack", "item_equipment_backpack_blackjack", "backpack"),
    ("nicecomm", "item_equipment_backpack_boss_birdeye", "backpack"),
    ("salewa", "salewa", "med"),
    ("grizzly", "AMK_Grizzly", "med"),
    ("surv12", "survival_first_aid_rollup_kit", "med"),
    ("ledx", "Item_barter_medical_transilluminator", "device"),
    ("tushonka", "beefstew", "food"),
    ("iskra", "item_food_ration", "food"),
    ("intel", "item_info_intelligence", "intel"),
    ("cofdm", "Item_barter_electr_wirelesstranmitter", "radio"),
]

# Parts that are never seen from outside, or are not part of the weapon.
SKIP = ("patron", "shell", "bullet", "_hide", "collider", "camera", "fp_", "human")

# The most triangles a weapon keeps; more are merged away.
BUDGET = 3200


def quat_matrix(q):
    x, y, z, w = q
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])


def trs(t):
    m = np.eye(4)
    p = t.m_LocalPosition
    r = t.m_LocalRotation
    s = t.m_LocalScale
    m[:3, :3] = quat_matrix((r.x, r.y, r.z, r.w)) @ np.diag([s.x, s.y, s.z])
    m[:3, 3] = [p.x, p.y, p.z]
    return m


def imported_turn(t):
    """Whether a node carries Unity's import turn: a quarter about x, standing a z-up model up."""
    r = t.m_LocalRotation
    return abs(abs(r.x) - 0.70710678) < 0.02 and abs(r.y) < 0.02 and abs(r.z) < 0.02 and abs(abs(r.w) - 0.70710678) < 0.02


# A weapon's slots are z-up like its model; a part imported y-up sits in one turned back by this.
SEAT = np.array([[1.0, 0, 0, 0], [0, 0, -1.0, 0], [0, 1.0, 0, 0], [0, 0, 0, 1.0]])


_MANIFEST = {}


def dependencies(streaming, prefab, depth=2):
    """The bundles the manifest says prefab needs that can hold meshes (not shaders, sounds, physics)."""
    if not _MANIFEST:
        _MANIFEST.update(json.load(open(os.path.join(streaming, "Windows.json"), encoding="utf-8")))
    found = []
    for dep in _MANIFEST.get(prefab, {}).get("Dependencies", []):
        if not dep.startswith("assets/content/") or "/audio/" in dep or "textures" in dep:
            continue
        found.append(dep)
        if depth > 1:
            found.extend(d for d in dependencies(streaming, dep, depth - 1) if d not in found)
    return found


class Part:
    """One item's prefab: its transforms by name, and its visible meshes in its own space."""

    def __init__(self, streaming, prefab, template, weapon=True):
        self.weapon = weapon
        path = os.path.join(streaming, prefab.replace("/", os.sep))
        folder, name = os.path.split(path)
        stem = name[:-len(".bundle")] if name.endswith(".bundle") else name
        files = [f for f in glob.glob(os.path.join(folder, glob.escape(stem) + "*")) if not f.endswith(".manifest")]
        client = os.path.join(folder, "client_assets.bundle")
        if os.path.exists(client):
            files.append(client)
        # And whatever the game's manifest says it needs that holds models: a variant (a colour
        # of a body armour, say) keeps its meshes in its base model's bundle or a shared one.
        for dep in ([] if weapon else dependencies(streaming, prefab)):
            path_dep = os.path.join(streaming, dep.replace("/", os.sep))
            if path_dep not in files and os.path.exists(path_dep):
                files.append(path_dep)
        self.env = UnityPy.load(*files)

        # Transforms by (file, id): a mod's mesh file repeats its prefab's hierarchy, and a
        # weapon's container holds the hands rig beside the model in client_assets.
        transforms = {}
        for obj in self.env.objects:
            if obj.type.name == "Transform":
                transforms[(self.file(obj), obj.path_id)] = obj.read()
        self.transforms = transforms
        roots = [k for k, t in transforms.items() if t.m_Father.path_id == 0]
        # The item's own model: the tree named after its template if there is one (a weapon's,
        # in client_assets), else the biggest tree in the prefab's own bundle.
        named = [k for k in roots if self.name(transforms[k]).lower() == template.lower()]
        own = [k for k in roots if k[0] == name]
        self.root = max(named or own or roots, key=self.count)
        self.world = {}
        self.walk(self.root, np.eye(4), top=True)
        # Imported y-up (Unity's turn on a node below the root that is not a slot): such a part
        # sits on a z-up slot turned back a quarter. One modelled z-up sits on it as it is.
        self.y_up = any(imported_turn(self.transforms[k]) and k != self.root
                        and not self.name(self.transforms[k]).startswith("mod_") for k in self.world)

    @staticmethod
    def file(obj):
        parent = getattr(obj.assets_file, "parent", None)
        return os.path.basename(getattr(parent, "name", "") or "") if parent is not None else ""

    def count(self, key):
        f = key[0]
        return 1 + sum(self.count((f, c.path_id)) for c in self.transforms[key].m_Children if (f, c.path_id) in self.transforms)

    def walk(self, key, parent, top=False):
        # The root sits on its slot; its own offset is not used.
        t = self.transforms[key]
        m = parent if top else parent @ trs(t)
        self.world[key] = m
        for c in t.m_Children:
            if (key[0], c.path_id) in self.transforms:
                self.walk((key[0], c.path_id), m)

    def name(self, t):
        return t.m_GameObject.read().m_Name

    def slot(self, slot):
        """The world matrix of the transform named slot in the model, or None."""
        for key in self.world:
            if self.name(self.transforms[key]) == slot:
                return self.world[key]
        return None

    def meshes(self):
        """(name, triangles as an n x 3 x 3 array) for each visible mesh, at the preferred LOD."""
        by_object = {(k[0], t.m_GameObject.path_id): k for k, t in self.transforms.items() if k in self.world}
        found = {}
        for obj in self.env.objects:
            if obj.type.name != "MeshFilter":
                continue
            mf = obj.read()
            key = by_object.get((self.file(obj), mf.m_GameObject.path_id))
            if key is None:
                continue
            go = mf.m_GameObject.read()
            if not go.m_IsActive:
                continue
            name = go.m_Name.lower()
            if any(s in name for s in SKIP):
                continue
            try:
                mesh = mf.m_Mesh.read()
            except Exception:
                continue
            base, lod = name, 0
            if "_lod" in name:
                base, _, level = name.rpartition("_lod")
                lod = int(level) if level.isdigit() else 0
            found.setdefault(base, {})[lod] = (mesh, key)

        out = []
        for base, lods in found.items():
            # LOD1 where there is one: the game's own simplification, fine at hologram size.
            lod = 1 if 1 in lods else min(lods)
            mesh, key = lods[lod]
            handler = MeshHandler(mesh)
            handler.process()
            if not handler.m_Vertices:
                continue
            v = np.array(handler.m_Vertices, dtype=np.float64)
            tris = [t for sub in handler.get_triangles() for t in sub]
            if not tris:
                continue
            if self.weapon and lod == 0 and len(tris) > 1500:
                # Only a detailed version, and a heavy one: an internal part, mostly.
                continue
            idx = np.array(tris, dtype=np.int64)
            m = self.world[key]
            w = (np.c_[v, np.ones(len(v))] @ m.T)[:, :3]
            out.append((base, w[idx]))
        return out


def assemble(streaming, items, preset, weapon):
    parts = []  # (slot, part name, triangles)

    def place(item, matrix):
        tpl = items[item["_tpl"]]
        prefab = tpl["_props"].get("Prefab", {}).get("path")
        if not prefab:
            return
        part = Part(streaming, prefab, tpl["_name"], weapon)
        if weapon and item.get("slotId") and part.y_up:
            matrix = matrix @ SEAT
        for name, tris in part.meshes():
            flat = tris.reshape(-1, 3)
            w = (np.c_[flat, np.ones(len(flat))] @ matrix.T)[:, :3]
            parts.append((item.get("slotId", "weapon"), name, w.reshape(-1, 3, 3)))
        for child in preset["_items"]:
            if child.get("parentId") == item["_id"]:
                slot = part.slot(child["slotId"])
                if slot is None:
                    print(f"   no slot {child['slotId']} on {tpl['_name']}")
                    continue
                place(child, matrix @ slot)

    root = next(it for it in preset["_items"] if it["_id"] == preset["_parent"])
    place(root, np.eye(4))
    return parts


def find_preset(globals_, items, template):
    """The item's default preset, or None."""
    for p in globals_["ItemPresets"].values():
        if p.get("_encyclopedia") and items[p["_encyclopedia"]]["_name"] == template:
            return p
    return None


def single(items, template):
    """A preset of one: the item alone."""
    key = next(k for k, v in items.items() if v["_name"] == template)
    return {"_parent": "root", "_items": [{"_id": "root", "_tpl": key}]}


def weld(tris, grid):
    """Shared vertices by position (to a grid), and the triangles as indices; degenerate ones dropped."""
    flat = tris.reshape(-1, 3)
    keys = np.round(flat / grid).astype(np.int64)
    uniq, inverse = np.unique(keys, axis=0, return_inverse=True)
    verts = np.zeros((len(uniq), 3))
    np.add.at(verts, inverse, flat)
    verts /= np.bincount(inverse)[:, None]
    faces = inverse.reshape(-1, 3)
    ok = (faces[:, 0] != faces[:, 1]) & (faces[:, 1] != faces[:, 2]) & (faces[:, 0] != faces[:, 2])
    faces = faces[ok]
    # The same triangle twice (both sides of a thin part): keep one.
    _, keep = np.unique(np.sort(faces, axis=1), axis=0, return_index=True)
    return verts, faces[np.sort(keep)]


def cut_down(tris, budget):
    """Welds on a coarser and coarser grid until the weapon is within budget: vertex clustering."""
    size = np.ptp(tris.reshape(-1, 3), axis=0).max()
    grid = size / 4000.0
    while True:
        verts, faces = weld(tris, grid)
        if len(faces) <= budget:
            return verts, faces, grid
        grid *= 1.15


def edges_of(verts, faces, crease_degrees=32.0):
    """
    Every edge with the faces either side (-1 for an open edge), and whether it is a crease: an
    open edge, or one where the surface turns by more than crease_degrees. Creases are always
    drawn; the rest only where they are the outline.
    """
    n = np.cross(verts[faces[:, 1]] - verts[faces[:, 0]], verts[faces[:, 2]] - verts[faces[:, 0]])
    length = np.linalg.norm(n, axis=1)
    n = n / np.maximum(length, 1e-12)[:, None]
    table = {}
    for f, (a, b, c) in enumerate(faces):
        for u, v in ((a, b), (b, c), (c, a)):
            key = (min(u, v), max(u, v))
            table.setdefault(key, []).append(f)
    cos = math.cos(math.radians(crease_degrees))
    out = []
    for (u, v), fs in table.items():
        if len(fs) == 1:
            out.append((u, v, fs[0], -1, True))
        else:
            f0, f1 = fs[0], fs[1]
            crease = len(fs) > 2 or float(np.dot(n[f0], n[f1])) < cos
            out.append((u, v, f0, f1, crease))
    return n, out


def orient(verts):
    """Barrel along +x, top up +y, centred, longest half-length 1."""
    c = (verts.max(axis=0) + verts.min(axis=0)) / 2.0
    v = verts - c
    ext = np.ptp(v, axis=0)
    order = np.argsort(-ext)
    long_axis, mid_axis, short_axis = order
    out = np.zeros_like(v)
    out[:, 0] = v[:, long_axis]
    out[:, 1] = v[:, mid_axis]
    out[:, 2] = v[:, short_axis]
    return out, (long_axis, mid_axis, short_axis)


def write(path, verts, faces, normals, edges, anchors):
    """
    Little endian: "CLW1"; vertex, face, edge and anchor counts (int32); vertices as int16 x, y, z
    over 32767; faces as uint16 x3; face normals as int8 x, y, z over 127; edges as uint16 a, b,
    int16 face0, face1 (-1: none), byte crease; anchors as int16 x, y, z, then the slot's name as
    a length byte and ASCII.
    """

    s = 1.0 / np.abs(verts).max()
    q = np.round(verts * s * 32767).astype(np.int16)
    with open(path, "wb") as f:
        f.write(b"CLW1")
        f.write(struct.pack("<4i", len(q), len(faces), len(edges), len(anchors)))
        f.write(q.tobytes())
        f.write(faces.astype(np.uint16).tobytes())
        f.write(np.round(normals * 127).astype(np.int8).tobytes())
        for a, b, f0, f1, crease in edges:
            f.write(struct.pack("<HHhhB", a, b, f0, f1, 1 if crease else 0))
        for slot, p in anchors:
            ap = np.round(np.clip(p * s, -1, 1) * 32767).astype(np.int16)
            f.write(struct.pack("<3h", *ap))
            name = slot.encode("ascii")[:31]
            f.write(struct.pack("<B", len(name)))
            f.write(name)


MATERIALS = {
    "Aramid": ("ARAMID", "АРАМИД"), "UHMWPE": ("UHMWPE", "СВМПЭ"), "Combined": ("COMBINED", "КОМБИНИРОВАННЫЙ"),
    "Titan": ("TITANIUM", "ТИТАН"), "Aluminium": ("ALUMINIUM", "АЛЮМИНИЙ"), "ArmoredSteel": ("ARMOURED STEEL", "БРОНЕСТАЛЬ"),
    "Ceramic": ("CERAMIC", "КЕРАМИКА"), "Glass": ("GLASS", "СТЕКЛО"),
}

KINDS = {
    "weapon": ("WEAPON SYSTEM", "ОБРАЗЕЦ ВООРУЖЕНИЯ"),
    "helmet": ("PROTECTIVE EQUIPMENT", "СРЕДСТВА БРОНЕЗАЩИТЫ"),
    "armor": ("PROTECTIVE EQUIPMENT", "СРЕДСТВА БРОНЕЗАЩИТЫ"),
    "nvg": ("NIGHT VISION", "ПРИБОРЫ НОЧНОГО ВИДЕНИЯ"),
    "headset": ("COMMUNICATIONS", "СРЕДСТВА СВЯЗИ"),
    "radio": ("COMMUNICATIONS", "СРЕДСТВА СВЯЗИ"),
    "backpack": ("LOAD CARRIAGE", "СНАРЯЖЕНИЕ"),
    "med": ("MEDICAL SUPPLY", "МЕДИЦИНСКОЕ ИМУЩЕСТВО"),
    "device": ("MEDICAL SUPPLY", "МЕДИЦИНСКОЕ ИМУЩЕСТВО"),
    "food": ("RATIONS", "ПРОДОВОЛЬСТВИЕ"),
    "intel": ("DOCUMENTS", "ДОКУМЕНТЫ"),
}


def spec_lines(kind, key, items, preset):
    """The figures under the item's name, in English and Russian, from the database."""
    p = items[key]["_props"]
    kg = p.get("Weight", 0.0)
    for it in preset["_items"][1:]:
        kg += items[it["_tpl"]]["_props"].get("Weight", 0.0)
    weight = (f"WEIGHT {kg:.1f} KG", f"МАССА {kg:.1f} КГ".replace(".", ","))
    if kind == "weapon":
        calibres = {
            "Caliber545x39": "5.45x39", "Caliber556x45NATO": "5.56x45 NATO", "Caliber762x54R": "7.62x54R",
            "Caliber762x51": "7.62x51 NATO", "Caliber762x35": ".300 BLK", "Caliber9x39": "9x39", "Caliber9x19PARA": "9x19 PARA",
        }
        calibre = calibres.get(p.get("ammoCaliber"), p.get("ammoCaliber", "").replace("Caliber", ""))
        modes = [m.upper() for m in p.get("weapFireType", [])]
        en = f"CAL {calibre}   ROF {int(p.get('bFirerate', 0))} RPM   EFF RANGE {int(p.get('bEffDist', 0))} M   MODES " + "/".join(
            {"SINGLE": "SEMI", "FULLAUTO": "AUTO"}.get(m, m) for m in modes)
        ru = f"КАЛИБР {calibre.replace('.', ',')}   ТЕМП {int(p.get('bFirerate', 0))} ВЫСТР/МИН   ДАЛЬНОСТЬ {int(p.get('bEffDist', 0))} М   РЕЖИМ " + "/".join(
            {"SINGLE": "ОД", "FULLAUTO": "АВТ", "BURST": "ОЧ"}.get(m, m) for m in modes)
        return en, ru
    if kind in ("helmet", "armor"):
        classes = [int(items[it["_tpl"]]["_props"].get("armorClass", 0) or 0) for it in preset["_items"]]
        cls = max(classes) if classes else 0
        mat = MATERIALS.get(p.get("ArmorMaterial", ""), (p.get("ArmorMaterial", "").upper(), p.get("ArmorMaterial", "")))
        en = (f"ARMOR CLASS {cls}   " if cls else "") + f"MATERIAL {mat[0]}   {weight[0]}"
        ru = (f"КЛАСС ЗАЩИТЫ {cls}   " if cls else "") + f"МАТЕРИАЛ {mat[1]}   {weight[1]}"
        cells = sum(g["_props"]["cellsH"] * g["_props"]["cellsV"] for g in p.get("Grids", []))
        if cells:
            en += f"   {cells} SLOTS"
            ru += f"   {cells} ЯЧЕЕК"
        return en, ru
    if kind == "backpack":
        cells = sum(g["_props"]["cellsH"] * g["_props"]["cellsV"] for g in p.get("Grids", []))
        return f"CAPACITY {cells} SLOTS   {weight[0]}", f"ВМЕСТИМОСТЬ {cells} ЯЧЕЕК   {weight[1]}"
    if kind == "med" and p.get("MaxHpResource"):
        return f"RESOURCE {p['MaxHpResource']} HP   {weight[0]}", f"РЕСУРС {p['MaxHpResource']} ЕД.   {weight[1]}"
    labels = {
        "nvg": ("IMAGE INTENSIFIER", "ЭЛЕКТРОННО-ОПТИЧЕСКИЙ ПРЕОБРАЗОВАТЕЛЬ"),
        "headset": ("ACTIVE HEARING PROTECTION", "АКТИВНЫЕ ПРОТИВОШУМНЫЕ НАУШНИКИ"),
        "radio": ("ENCRYPTED VIDEO DOWNLINK", "ЗАЩИЩЁННЫЙ КАНАЛ ПЕРЕДАЧИ"),
        "device": ("DIAGNOSTIC EQUIPMENT", "ДИАГНОСТИЧЕСКОЕ ОБОРУДОВАНИЕ"),
        "food": ("FIELD RATION", "ПОЛЕВОЙ ПАЁК"),
        "intel": ("HANDLE AS CLASSIFIED", "ОБРАЩАТЬСЯ КАК С СЕКРЕТНЫМИ"),
        "med": ("FIRST AID", "ПЕРВАЯ ПОМОЩЬ"),
    }
    label = labels.get(kind, ("", ""))
    return f"{label[0]}   {weight[0]}", f"{label[1]}   {weight[1]}"


def write_specs(entries):
    """The items' names and figures, in English and Russian, as Kit.Data.cs."""
    def cs(text):
        return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'
    lines = [
        "// Generated by scripts/make_weapons.py from SPT's database. Do not edit by hand: run the script.",
        "namespace CLASSIFIED",
        "{",
        "    internal static partial class Kit",
        "    {",
        "        internal static readonly Spec[] All =",
        "        {",
    ]
    for e in entries:
        lines.append("            new Spec(" + ", ".join(cs(x) for x in e) + "),")
    lines += ["        };", "    }", "}", ""]
    with open(os.path.join(ROOT, "src", "CLASSIFIED", "Kit.Data.cs"), "w", newline="\n", encoding="utf-8") as f:
        f.write("\n".join(lines))


def side_view(path, verts, faces, edges):
    w, h = 1400, 520
    img = Image.new("RGB", (w, h), (8, 10, 14))
    d = ImageDraw.Draw(img)
    s = (w * 0.47)
    def p(v):
        return (w / 2 + v[0] * s, h / 2 - v[1] * s)
    for a, b, f0, f1, crease in edges:
        if crease:
            d.line([p(verts[a]), p(verts[b])], fill=(90, 200, 255), width=1)
    img.save(path)


def main():
    spt = sys.argv[1] if len(sys.argv) > 1 else r"H:\SPT4.1.X"
    streaming = os.path.join(spt, "EscapeFromTarkov_Data", "StreamingAssets", "Windows")
    db = os.path.join(spt, "SPT_Runtime", "SPT_Data", "database")
    items = json.load(open(os.path.join(db, "templates", "items.json"), encoding="utf-8"))
    globals_ = json.load(open(os.path.join(db, "globals.json"), encoding="utf-8"))
    en = json.load(open(os.path.join(db, "locales", "global", "en.json"), encoding="utf-8"))
    ru = json.load(open(os.path.join(db, "locales", "global", "ru.json"), encoding="utf-8"))
    os.makedirs(PREVIEW, exist_ok=True)
    only = set(sys.argv[2:])

    specs = []
    for iid, template, kind in ITEMS:
        weapon = kind == "weapon"
        preset = find_preset(globals_, items, template) or single(items, template)
        key = next(it["_tpl"] for it in preset["_items"] if it["_id"] == preset["_parent"])
        line_en, line_ru = spec_lines(kind, key, items, preset)
        specs.append((iid, kind, KINDS[kind][0], KINDS[kind][1], en[key + " ShortName"], en[key + " Name"],
                      ru.get(key + " ShortName", en[key + " ShortName"]), ru.get(key + " Name", en[key + " Name"]), line_en, line_ru))
        if only and iid not in only:
            continue
        parts = assemble(streaming, items, preset, weapon)
        if not parts:
            print(f"{iid}: no meshes")
            continue
        tris = np.concatenate([p[2] for p in parts])
        raw = len(tris)
        verts, faces, grid = cut_down(tris, BUDGET if weapon else BUDGET * 3 // 4)

        # Centred on its box. A weapon turned barrel along +x, top up; equipment stays as the
        # game holds it, y up.
        c = (verts.max(axis=0) + verts.min(axis=0)) / 2.0
        if weapon:
            verts, axes = orient(verts)
        else:
            verts, axes = verts - c, (0, 1, 2)
            if kind in ("armor", "backpack"):
                # Body armour and packs are character meshes, z-up: stood up a quarter turn.
                verts = np.c_[verts[:, 0], verts[:, 2], -verts[:, 1]]

        # A callout's anchor is the middle of its part's box, in the same space as the vertices.
        boxes = {}
        for slot, name, t in parts:
            pts = (t.reshape(-1, 3) - c)[:, list(axes)]
            lo, hi = pts.min(axis=0), pts.max(axis=0)
            if slot in boxes:
                lo, hi = np.minimum(lo, boxes[slot][0]), np.maximum(hi, boxes[slot][1])
            boxes[slot] = (lo, hi)
        anchor_list = [(slot, (lo + hi) / 2.0) for slot, (lo, hi) in boxes.items()] if weapon else []

        if weapon:
            def anchor(name):
                for slot, p in anchor_list:
                    if slot == name:
                        return p
                return None
            flip_x = flip_y = False
            muzzle = anchor("mod_muzzle") if anchor("mod_muzzle") is not None else anchor("mod_barrel")
            stock = anchor("mod_stock")
            if muzzle is not None and stock is not None:
                flip_x = muzzle[0] < stock[0]
            elif muzzle is not None:
                flip_x = muzzle[0] < 0
            # Top up: the magazine and grip hang below the line of the barrel.
            below = [p for p in (anchor("mod_magazine"), anchor("mod_pistol_grip"), anchor("mod_pistolgrip")) if p is not None]
            line = muzzle if muzzle is not None else np.zeros(3)
            if below:
                flip_y = np.mean([p[1] for p in below]) > line[1]
            sign = np.array([-1.0 if flip_x else 1.0, -1.0 if flip_y else 1.0, 1.0])
            verts = verts * sign
            anchor_list = [(slot, p * sign) for slot, p in anchor_list]
            if flip_x != flip_y:
                faces = faces[:, [0, 2, 1]]

        # Into a unit box; the anchors with it.
        scale = 1.0 / np.abs(verts).max()
        verts = verts * scale
        anchor_list = [(slot, p * scale) for slot, p in anchor_list]

        # Normals out of the item, so the hologram can tell the faces turned to it: on a closed
        # surface the faces' normals point away from the middle, weighted by area.
        tri = verts[faces]
        cross = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        if float(np.sum(cross * (tri.mean(axis=1) - verts.mean(axis=0)))) < 0:
            faces = faces[:, [0, 2, 1]]
        normals, edges = edges_of(verts, faces)
        creases = sum(1 for e in edges if e[4])
        path = os.path.join(OUT, f"item_{iid}.bin")
        write(path, verts, faces, normals, edges, anchor_list)
        side_view(os.path.join(PREVIEW, f"{iid}.png"), verts, faces, edges)
        print(f"{iid}: {len(parts)} meshes, {raw} -> {len(faces)} triangles, {len(verts)} vertices, "
              f"{len(edges)} edges ({creases} creases), {os.path.getsize(path) // 1024} KB")
    write_specs(specs)


if __name__ == "__main__":
    main()
