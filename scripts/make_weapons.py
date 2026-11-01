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

import copy
import glob
import gzip
import json
import random
import math
import os
import struct
import sys

import numpy as np
from scipy.spatial import cKDTree
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
    ("ak103", "weapon_izhmash_ak103_762x39", "weapon"),
    ("ak105", "weapon_izhmash_ak105_545x39", "weapon"),
    ("aks74", "weapon_izhmash_aks74_545x39", "weapon"),
    ("vss", "weapon_tochmash_vss_9x39", "weapon"),
    ("sv98", "weapon_izhmash_sv-98_762x54r", "weapon"),
    ("vityaz", "weapon_izhmash_pp-19-01_9x19", "weapon"),
    ("mk16", "weapon_fn_mk16_556x45", "weapon"),
    ("m1a", "weapon_springfield_m1a_762x51", "weapon"),
    ("mk47", "weapon_cmmg_mk47_762x39", "weapon"),
    ("rem700", "weapon_remington_model_700_762x51", "weapon"),
    ("mp5k", "weapon_hk_mp5_kurtz_9x19", "weapon"),
    ("kiver", "helmet", "helmet"),
    ("lshz2dtm", "item_equipment_helmet_lshz2dtm", "helmet"),
    ("vulkan5", "item_equipment_helmet_vulkan5", "helmet"),
    ("mich2001", "helmet_mich2001_od", "helmet"),
    ("bastion", "item_equipment_helmet_diamond_age_bastion", "helmet"),
    ("gzhel", "armor_gjel", "armor"),
    ("korundvm", "item_equipment_armor_korundvm", "armor"),
    ("defender2", "item_equipment_armor_defender2", "armor"),
    ("6b3", "item_equipment_armor_6b3", "armor"),
    ("kirasa", "item_equipment_armor_kirasa", "armor"),
    ("cpc", "item_equipment_armor_arscpc", "armor"),
    ("hexgrid", "item_equipment_armor_custom_hexgrid", "armor"),
    ("medpack", "item_equipment_backpack_medpack", "backpack"),
    ("betav2", "item_equipment_backpack_betav2", "backpack"),
    ("ak104", "weapon_izhmash_ak104_762x39", "weapon"),
    ("ak74n", "weapon_izhmash_ak74n_545x39", "weapon"),
    ("saiga12", "weapon_izhmash_saiga12k_10_12g", "weapon"),
    ("ash12", "weapon_ckib_ash_12_127x55", "weapon"),
    ("sks", "weapon_toz_sks_762x39", "weapon"),
    ("mosin", "weapon_izhmash_mosin_rifle_762x54", "weapon"),
    ("pkp", "weapon_zid_pkp_762x54r", "weapon"),
    ("sr2m", "weapon_tochmash_sr2m_veresk_9x21", "weapon"),
    ("vepr", "weapon_molot_vepr_km_vpo_136_762x39", "weapon"),
    ("mdr", "weapon_dt_mdr_556x45", "weapon"),
    ("mk17", "weapon_fn_mk17_762x51", "weapon"),
    ("g28", "weapon_hk_g28_762x51", "weapon"),
    ("ump", "weapon_hk_ump_1143x23", "weapon"),
    ("vector", "weapon_tdi_kriss_vector_gen_2_1143x23", "weapon"),
    ("rsass", "weapon_remington_r11_rsass_762x51", "weapon"),
    ("tx15", "weapon_lone_star_tx15_designated_marksman_556x45", "weapon"),
    ("mpx", "weapon_sig_mpx_9x19", "weapon"),
    ("aug", "weapon_steyr_aug_a3_m1_556x45", "weapon"),
    ("spear", "weapon_sig_mcx_spear_68x51", "weapon"),
    # Sidearms and the rest.
    ("pl15", "weapon_izhmash_pl15_9x19", "weapon"),
    ("saiga9", "weapon_izhmash_saiga_9_9x19", "weapon"),
    ("ks23", "weapon_toz_ks23m_23x75", "weapon"),
    ("veprhunter", "weapon_molot_vepr_hunter_vpo-101_762x51", "weapon"),
    ("m1911", "weapon_colt_m1911a1_1143x23", "weapon"),
    ("m9a3", "weapon_beretta_m9a3_9x19", "weapon"),
    ("fiveseven", "weapon_fn_five_seven_57x28", "weapon"),
    ("usp", "weapon_hk_usp_45", "weapon"),
    ("rfb", "weapon_kel_tec_rfb_762x51", "weapon"),
    # More gear.
    ("zsh12m", "helmet_zsh_1-2m_v1", "helmet"),
    ("ulach", "item_equipment_helmet_ULACH_coyote", "helmet"),
    ("redutm", "item_equipment_armor_redut_m", "armor"),
    ("zhuk6a", "item_equipment_armor_zhuk6a", "armor"),
    ("iotv", "item_equipment_armor_iotv_1", "armor"),
    ("otv", "item_equipment_armor_otv_ucp", "armor"),
    ("paca", "notHeavy", "armor"),
    ("tv110", "item_equipment_rig_tv110", "armor"),
    ("tarzan", "item_equipment_rig_tarzan", "rig"),
    ("azimut", "item_equipment_rig_azimut_black", "rig"),
    ("commando", "item_equipment_rig_commando", "rig"),
    ("thunderbolt", "item_equipment_rig_thunderbolt", "rig"),
    ("attack2", "backpack_sso_attack2", "backpack"),
    ("pilgrim", "backpack_pilgrim", "backpack"),
    ("berkut", "item_equipment_backpack_wartech", "backpack"),
    ("paratus", "item_equipment_backpack_paratus", "backpack"),
    ("sordin", "headphones_MSA", "headset"),
    ("razor", "item_equipment_headset_walkers_razor", "headset"),
    ("earmor", "item_equipment_headset_m32", "headset"),
    ("n15", "nvg_armasight_n-15", "nvg"),
    ("t7", "thermal_spi_t7", "nvg"),
    ("ifak", "item_meds_medkit_ifak", "med"),
    ("cms", "core_medical_surgical_kit", "med"),
    ("propital", "Propital", "med"),
    ("flash", "item_barter_info_militaryflash", "intel"),
    ("vpx", "Item_barter_electr_flashstorage", "tech"),
    ("virtex", "Item_barter_electr_virtexprocessor", "tech"),
    ("gpu", "video_card", "tech"),
    ("battery", "item_barter_energy_batterymilitary", "tech"),
    ("gasan", "item_electr_gasanalyzer", "tech"),
    ("tetriz", "item_barter_electr_tetriz", "tech"),
    ("lion", "item_barter_valuable_lion", "valuable"),
]

# A weapon is built several ways: its default preset, and random builds. A random build swaps
# parts for others that fit (handguard, stock, grip, muzzle device, dust cover, magazine...),
# keeping what was on them where the new part takes it, then fits attachments wherever its
# slots take them: an optic (on a mount if it needs one), lights and lasers, a foregrip, a
# suppressor. Everything from what the database says fits; nothing that conflicts.
BUILDS = 8
REPLACE = {
    "mod_handguard": 0.7, "mod_stock": 0.6, "mod_stock_000": 0.5, "mod_stock_001": 0.5, "mod_pistol_grip": 0.6,
    "mod_pistolgrip": 0.6, "mod_muzzle": 0.6, "mod_reciever": 0.35, "mod_magazine": 0.4, "mod_charge": 0.35,
}
ATTACH = [("mod_scope", 0.97), ("mod_mount", 0.9), ("mod_tactical", 0.8), ("mod_foregrip", 0.85), ("mod_muzzle", 0.6)]

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

    def __init__(self, streaming, prefab, template, weapon=True, deps=False):
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
        if deps:
            # A colour variant (_fde, _blk) keeps its meshes in its base model's bundle beside it.
            own = os.path.join(folder, stem + "_textures.bundle")
            if os.path.exists(own) and own not in files:
                files.append(own)
            base = stem
            for _ in range(3):
                if "_" not in base:
                    break
                base = base.rsplit("_", 1)[0]
                # Its "_textures" bundle too, which for some parts holds the meshes as well.
                for sibling in (os.path.join(folder, base + ".bundle"), os.path.join(folder, base + "_textures.bundle")):
                    if os.path.exists(sibling) and sibling not in files:
                        files.append(sibling)
        for dep in (dependencies(streaming, prefab) if deps or not weapon else []):
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
        heavy = []
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
            idx = np.array(tris, dtype=np.int64)
            m = self.world[key]
            w = (np.c_[v, np.ones(len(v))] @ m.T)[:, :3]
            if self.weapon and lod == 0 and len(tris) > 1500:
                # Only a detailed version, and a heavy one: an internal part, mostly. Kept
                # aside for a part that has nothing else (a handguard, a stock, an ACOG).
                heavy.append((base, w[idx]))
                continue
            out.append((base, w[idx]))
        return out or heavy


def assemble(streaming, items, preset, weapon, problems=None):
    parts = []  # (slot, part name, triangles)
    problems = [] if problems is None else problems

    def place(item, matrix):
        tpl = items[item["_tpl"]]
        prefab = tpl["_props"].get("Prefab", {}).get("path")
        if not prefab:
            return
        part = Part(streaming, prefab, tpl["_name"], weapon)
        # The callout it gets: an optic only for a real sight (not the rings or riser under it),
        # and numbered slots (mod_tactical_001) under their kind.
        label = item.get("slotId", "weapon")
        if weapon and label != "weapon":
            if sight_kind(items, item["_tpl"]):
                label = "mod_scope"
            elif label.startswith("mod_scope"):
                label = "mount"
            else:
                for kind in ("mod_tactical", "mod_muzzle", "mod_foregrip"):
                    if label.startswith(kind):
                        label = kind
        shown = part.meshes()
        if not shown and weapon:
            # Its meshes in another bundle, as some parts keep them: loaded with what the
            # manifest says it needs (only then: doing it for every part broke the PKM).
            part = Part(streaming, prefab, tpl["_name"], weapon, deps=True)
            shown = part.meshes()
        if weapon and item.get("slotId") and part.y_up:
            matrix = matrix @ SEAT
        if not shown and item.get("slotId"):
            problems.append(f"{tpl['_name']} shows nothing")
        for name, tris in shown:
            flat = tris.reshape(-1, 3)
            w = (np.c_[flat, np.ones(len(flat))] @ matrix.T)[:, :3]
            parts.append((label, name, w.reshape(-1, 3, 3)))
        for child in preset["_items"]:
            if child.get("parentId") == item["_id"]:
                slot = part.slot(child["slotId"])
                if slot is None:
                    print(f"   no slot {child['slotId']} on {tpl['_name']}")
                    problems.append(f"no slot {child['slotId']}")
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


_CHILDREN = {}


def leaves(items, ids):
    """The items a slot filter names: items themselves, or every item under a category."""
    if not _CHILDREN:
        for k, v in items.items():
            _CHILDREN.setdefault(v.get("_parent"), []).append(k)
    out = []
    stack = list(ids)
    while stack:
        k = stack.pop()
        v = items.get(k)
        if v is None:
            continue
        if v.get("_type") == "Item":
            out.append(k)
        else:
            stack.extend(_CHILDREN.get(k, []))
    return out


SIGHTS = {"Collimator": 1.0, "CompactCollimator": 1.0, "AssaultScope": 1.0, "OpticScope": 0.8, "SpecialScope": 0.25}


def kinds_of(items, tpl):
    """The names of the item's categories, nearest first."""
    out = []
    node = items.get(tpl, {}).get("_parent")
    while node and node in items:
        out.append(items[node]["_name"])
        node = items[node].get("_parent")
    return out


def sight_kind(items, tpl):
    return next((k for k in kinds_of(items, tpl) if k in SIGHTS), None)


def is_mount(items, tpl):
    return "Mount" in kinds_of(items, tpl)


def random_build(items, preset, rng, streaming):
    """The preset with parts swapped and attachments fitted at random, wherever they fit."""
    its = copy.deepcopy(preset["_items"])
    counter = [0]

    def new_id():
        counter[0] += 1
        return f"r{counter[0]}"

    def tpl_of(iid):
        return next(it["_tpl"] for it in its if it["_id"] == iid)

    def slot_def(iid, name):
        for sl in items[tpl_of(iid)]["_props"].get("Slots", []):
            if sl["_name"] == name:
                return sl
        return None

    def children(iid):
        return [it for it in its if it.get("parentId") == iid]

    def subtree(iid):
        out = [iid]
        for c in children(iid):
            out.extend(subtree(c["_id"]))
        return out

    def depth(iid):
        d = 0
        it = next(i for i in its if i["_id"] == iid)
        while it.get("parentId"):
            it = next(i for i in its if i["_id"] == it["parentId"])
            d += 1
        return d

    def usable(tpl, ignore=()):
        v = items.get(tpl)
        if v is None:
            return False
        path = v["_props"].get("Prefab", {}).get("path", "")
        name = v["_name"].lower()
        if not path or "event" in name or "test" in name or "quest" in name:
            return False
        if not os.path.exists(os.path.join(streaming, path.replace("/", os.sep))):
            return False
        mine = set(v["_props"].get("ConflictingItems", []))
        for it in its:
            if it["_id"] in ignore:
                continue
            if it["_tpl"] in mine or tpl in items[it["_tpl"]]["_props"].get("ConflictingItems", []):
                return False
        return True

    def options(sl, ignore=()):
        filters = sl["_props"].get("filters", [{}])
        pool = leaves(items, filters[0].get("Filter", []) if filters else [])
        return sorted(t for t in pool if usable(t, ignore))

    def fill_required(iid, budget=6):
        """Fills the slots a part cannot do without; False if one cannot be filled."""
        for sl in items[tpl_of(iid)]["_props"].get("Slots", []):
            # What the game requires, and what a weapon looks wrong without (a buffer tube with no
            # stock on it, a receiver with no grip).
            essential = sl.get("_required") or sl["_name"].startswith(("mod_stock", "mod_pistol_grip", "mod_pistolgrip", "mod_handguard", "mod_barrel"))
            if not essential or any(c.get("slotId") == sl["_name"] for c in children(iid)):
                continue
            pool = options(sl)
            if not pool or budget <= 0:
                return False
            child = {"_id": new_id(), "_tpl": rng.choice(pool), "parentId": iid, "slotId": sl["_name"]}
            its.append(child)
            if not fill_required(child["_id"], budget - 1):
                return False
        return True

    # Swaps.
    for it in list(its):
        slot = it.get("slotId")
        if not slot or slot not in REPLACE or it not in its or rng.random() > REPLACE[slot]:
            continue
        sl = slot_def(it["parentId"], slot)
        if sl is None:
            continue
        old_tree = subtree(it["_id"])
        pool = [t for t in options(sl, ignore=old_tree) if t != it["_tpl"]]
        if not pool:
            continue
        saved = copy.deepcopy(its)
        old_children = children(it["_id"])
        # The part and everything on it go; the new part keeps what it can take of that.
        keep = list(old_children)
        removed = set(old_tree)
        its[:] = [x for x in its if x["_id"] not in removed]
        new_tpl = rng.choice(pool)
        part = {"_id": it["_id"], "_tpl": new_tpl, "parentId": it["parentId"], "slotId": slot}
        its.append(part)
        for c in keep:
            sl2 = slot_def(part["_id"], c["slotId"])
            if sl2 is None:
                continue
            filters = sl2["_props"].get("filters", [{}])
            if c["_tpl"] in leaves(items, filters[0].get("Filter", []) if filters else []):
                # Back on, with everything that was on it.
                for x in saved:
                    if x["_id"] in subtree_of(saved, c["_id"]):
                        its.append(x)
        if not fill_required(part["_id"]):
            its[:] = saved

    def pick_sight(pool):
        """A sight from the pool: its kind first, evenly, then one of that kind."""
        by_kind = {}
        for t in pool:
            k = sight_kind(items, t)
            if k:
                by_kind.setdefault(k, []).append(t)
        if not by_kind:
            return None
        kinds = sorted(by_kind)
        k = rng.choices(kinds, weights=[SIGHTS[x] for x in kinds])[0]
        return rng.choice(by_kind[k])

    def scope_mounts(pool):
        return [t for t in pool if is_mount(items, t) and any(s2["_name"].startswith("mod_scope") for s2 in items[t]["_props"].get("Slots", []))]

    def fit_sight(it, sl):
        """A sight in the slot, straight on, or on rings or a riser that take one. True if it went on."""
        pool = options(sl)
        sight = pick_sight(pool)
        if sight is None or rng.random() < 0.3:
            mounts = scope_mounts(pool)
            if mounts:
                mount = {"_id": new_id(), "_tpl": rng.choice(mounts), "parentId": it["_id"], "slotId": sl["_name"]}
                its.append(mount)
                for sl2 in items[mount["_tpl"]]["_props"].get("Slots", []):
                    if sl2["_name"].startswith("mod_scope"):
                        inner = pick_sight(options(sl2))
                        if inner:
                            its.append({"_id": new_id(), "_tpl": inner, "parentId": mount["_id"], "slotId": sl2["_name"]})
                            if fill_required(mount["_id"], 3):
                                return True
                        break
                its[:] = [x for x in its if x["_id"] not in subtree(mount["_id"])]
        if sight is None:
            return False
        new = {"_id": new_id(), "_tpl": sight, "parentId": it["_id"], "slotId": sl["_name"]}
        its.append(new)
        if fill_required(new["_id"], 3):
            return True
        its[:] = [x for x in its if x["_id"] not in subtree(new["_id"])]
        return False

    def has_sight():
        return any(sight_kind(items, x["_tpl"]) for x in its)

    def fit_device(it, sl, prefix):
        """A light, laser or grip (by slot prefix) in the slot, or on a mount there that takes one."""
        pool = options(sl)
        direct = [t for t in pool if not is_mount(items, t)] if sl["_name"].startswith(prefix) else []
        if direct:
            new = {"_id": new_id(), "_tpl": rng.choice(direct), "parentId": it["_id"], "slotId": sl["_name"]}
            its.append(new)
            if fill_required(new["_id"], 3):
                return True
            its[:] = [x for x in its if x["_id"] not in subtree(new["_id"])]
            return False
        mounts = [t for t in pool if is_mount(items, t) and any(s2["_name"].startswith(prefix) for s2 in items[t]["_props"].get("Slots", []))]
        if not mounts:
            return False
        mount = {"_id": new_id(), "_tpl": rng.choice(mounts), "parentId": it["_id"], "slotId": sl["_name"]}
        its.append(mount)
        for sl2 in items[mount["_tpl"]]["_props"].get("Slots", []):
            if sl2["_name"].startswith(prefix):
                devices = [t for t in options(sl2) if not is_mount(items, t)]
                if devices:
                    its.append({"_id": new_id(), "_tpl": rng.choice(devices), "parentId": mount["_id"], "slotId": sl2["_name"]})
                    if fill_required(mount["_id"], 3):
                        return True
                break
        its[:] = [x for x in its if x["_id"] not in subtree(mount["_id"])]
        return False

    def reach(it, sl, prefix, pick, depth=3):
        """
        Something of a kind (slot prefix) in the slot: straight in if the slot is that kind,
        else through mounts, as deep as it takes (a Mosin's Kochetov mount takes a rail that
        takes the scope). True if it went on.
        """
        pool = options(sl)
        if sl["_name"].startswith(prefix):
            chosen = pick([t for t in pool if not is_mount(items, t)])
            if chosen:
                new = {"_id": new_id(), "_tpl": chosen, "parentId": it["_id"], "slotId": sl["_name"]}
                its.append(new)
                if fill_required(new["_id"], 3):
                    return True
                its[:] = [x for x in its if x["_id"] not in subtree(new["_id"])]
        if depth <= 0:
            return False
        mounts = [t for t in pool if is_mount(items, t) and any(s2["_name"].startswith((prefix, "mod_mount", "mod_scope"))
                                                                for s2 in items[t]["_props"].get("Slots", []))]
        rng.shuffle(mounts)
        for tpl in mounts[:4]:
            mount = {"_id": new_id(), "_tpl": tpl, "parentId": it["_id"], "slotId": sl["_name"]}
            its.append(mount)
            slots = sorted(items[tpl]["_props"].get("Slots", []), key=lambda x: 0 if x["_name"].startswith(prefix) else 1)
            for sl2 in slots:
                if sl2["_name"].startswith((prefix, "mod_mount", "mod_scope")) and reach(mount, sl2, prefix, pick, depth - 1):
                    if fill_required(mount["_id"], 3):
                        return True
                    break
            its[:] = [x for x in its if x["_id"] not in subtree(mount["_id"])]
        return False

    def swap_for(prefix, fit, slot_order):
        """
        Nothing on the weapon takes one (an AKS-74 has no dovetail): a part swapped for one that
        does (a railed dust cover or gas tube, a railed handguard), keeping what it can of what
        was on the old one, then fitted. The build as it was if none works.
        """
        for iid in [x["_id"] for x in its if x.get("parentId")]:
            it = next((x for x in its if x["_id"] == iid), None)
            if it is None:
                continue
            sl = slot_def(it["parentId"], it["slotId"])
            if sl is None:
                continue
            cands = [t for t in options(sl, ignore=subtree(iid)) if t != it["_tpl"]
                     and any(x["_name"].startswith((prefix, "mod_mount", "mod_scope")) for x in items[t]["_props"].get("Slots", []))]
            rng.shuffle(cands)
            for tpl in cands[:3]:
                saved = copy.deepcopy(its)
                it = next(x for x in its if x["_id"] == iid)
                new_slots = {x["_name"]: x for x in items[tpl]["_props"].get("Slots", [])}
                for c in children(iid):
                    sl2 = new_slots.get(c["slotId"])
                    filters = sl2["_props"].get("filters", [{}]) if sl2 else None
                    if sl2 is None or c["_tpl"] not in leaves(items, filters[0].get("Filter", []) if filters else []):
                        its[:] = [x for x in its if x["_id"] not in subtree(c["_id"])]
                it["_tpl"] = tpl
                if fill_required(iid) and ensure(prefix, fit, slot_order):
                    return True
                its[:] = saved
        # Or an empty slot given a part that does (a PKM's handguard slot, empty as issued).
        for iid in [x["_id"] for x in its]:
            it = next(x for x in its if x["_id"] == iid)
            for sl in items[it["_tpl"]]["_props"].get("Slots", []):
                if any(c.get("slotId") == sl["_name"] for c in children(iid)):
                    continue
                cands = [t for t in options(sl) if any(x["_name"].startswith((prefix, "mod_mount", "mod_scope")) for x in items[t]["_props"].get("Slots", []))]
                rng.shuffle(cands)
                for tpl in cands[:3]:
                    saved = copy.deepcopy(its)
                    part = {"_id": new_id(), "_tpl": tpl, "parentId": iid, "slotId": sl["_name"]}
                    its.append(part)
                    if fill_required(part["_id"]) and ensure(prefix, fit, slot_order):
                        return True
                    its[:] = saved
        return False

    def any_of(pool):
        return rng.choice(pool) if pool else None

    def has_kind(prefix):
        return any(x.get("slotId", "").startswith(prefix) and not is_mount(items, x["_tpl"]) for x in its)

    def ensure(prefix, fit, slot_order):
        """Fills the first free slot, in slot_order, where fit puts something on."""
        for wanted in slot_order:
            for it in list(its):
                for sl in items[it["_tpl"]]["_props"].get("Slots", []):
                    if sl["_name"].startswith(wanted) and not any(c.get("slotId") == sl["_name"] for c in children(it["_id"])):
                        if fit(it, sl):
                            return True
        return False

    # Attachments.
    has_scope = has_sight()
    queue = list(its)
    added = 0
    while queue:
        it = queue.pop(0)
        if it not in its:
            continue
        for sl in items[it["_tpl"]]["_props"].get("Slots", []):
            name = sl["_name"]
            if any(c.get("slotId") == name for c in children(it["_id"])):
                continue
            rule = next((r for r in ATTACH if name.startswith(r[0])), None)
            if rule is None or depth(it["_id"]) > 6 or added >= 9:
                continue
            kind, chance = rule
            if kind == "mod_scope":
                # The optic goes on whole: a sight, or rings with their scope on.
                if not has_scope and fit_sight(it, sl):
                    has_scope = True
                    added += 1
                    queue.extend(x for x in its if x not in queue)
                continue
            if rng.random() > chance:
                continue
            pool = options(sl)
            if kind == "mod_mount":
                # A mount is there to carry something: an optic, a light, a grip.
                pool = [t for t in pool if any(s2["_name"].startswith(("mod_scope", "mod_tactical", "mod_foregrip"))
                                               for s2 in items[t]["_props"].get("Slots", []))]
                if has_scope:
                    pool = [t for t in pool if not any(s2["_name"].startswith("mod_scope") for s2 in items[t]["_props"].get("Slots", []))]
            if not pool:
                continue
            new = {"_id": new_id(), "_tpl": rng.choice(pool), "parentId": it["_id"], "slotId": name}
            its.append(new)
            if not fill_required(new["_id"], 3):
                its[:] = [x for x in its if x["_id"] not in subtree(new["_id"])]
                continue
            queue.append(new)
            added += 1

    # Every random build shows its attachments: an optic (in a scope slot, else on a mount: the
    # dovetail of an AK, a pistol's slide plate), a light or laser, and most of the time a grip.
    if not has_sight():
        fit = lambda it, sl: reach(it, sl, "mod_scope", pick_sight)
        if not ensure("mod_scope", fit, ("mod_scope", "mod_mount")):
            swap_for("mod_scope", fit, ("mod_scope", "mod_mount"))
    if not has_kind("mod_tactical"):
        fit = lambda it, sl: reach(it, sl, "mod_tactical", any_of)
        if not ensure("mod_tactical", fit, ("mod_tactical", "mod_mount", "mod_scope")):
            swap_for("mod_tactical", fit, ("mod_tactical", "mod_mount"))
    if not has_kind("mod_foregrip") and rng.random() < 0.75:
        ensure("mod_foregrip", lambda it, sl: reach(it, sl, "mod_foregrip", any_of), ("mod_foregrip", "mod_mount"))

    # No bare mounts: one added that ended up carrying nothing comes off again.
    original = {x["_id"] for x in preset["_items"]}
    changed = True
    while changed:
        changed = False
        for x in list(its):
            if x["_id"] not in original and is_mount(items, x["_tpl"]) and not children(x["_id"]):
                its.remove(x)
                changed = True
    return {"_parent": preset["_parent"], "_items": its}


def subtree_of(items_list, iid):
    out = {iid}
    grew = True
    while grew:
        grew = False
        for x in items_list:
            if x.get("parentId") in out and x["_id"] not in out:
                out.add(x["_id"])
                grew = True
    return out


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


def write(path, verts, faces, anchors):
    """
    Gzipped, little endian: "CLW2"; vertex, face and anchor counts (int32); vertices as int16 x,
    y, z over 32767; faces as uint16 x3, wound outward; anchors as int16 x, y, z, then the
    slot's name as a length byte and ASCII. The terminal works out normals and edges itself.
    """
    s = 1.0 / np.abs(verts).max()
    q = np.round(verts * s * 32767).astype(np.int16)
    with gzip.open(path, "wb", compresslevel=9) as f:
        f.write(b"CLW2")
        f.write(struct.pack("<3i", len(q), len(faces), len(anchors)))
        f.write(q.tobytes())
        f.write(faces.astype(np.uint16).tobytes())
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
    "rig": ("LOAD CARRIAGE", "СНАРЯЖЕНИЕ"),
    "tech": ("RECOVERED HARDWARE", "ТРОФЕЙНАЯ ТЕХНИКА"),
    "valuable": ("VALUABLES", "ЦЕННОСТИ"),
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
            "Caliber9x18PM": "9x18 PM", "Caliber762x25TT": "7.62x25 TT", "Caliber9x21": "9x21", "Caliber1143x23ACP": ".45 ACP",
            "Caliber57x28": "5.7x28", "Caliber23x75": "23x75", "Caliber86x70": ".338 LM", "Caliber40x46": "40x46 MM",
            "Caliber762x39": "7.62x39", "Caliber12g": "12 GA", "Caliber127x55": "12.7x55", "Caliber9x33R": ".357",
            "Caliber68x51": "6.8x51",
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
    if kind in ("backpack", "rig"):
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
        "tech": ("SALVAGED ELECTRONICS", "ТРОФЕЙНАЯ ЭЛЕКТРОНИКА"),
        "valuable": ("BARTER GOODS", "ТОВАР ДЛЯ ОБМЕНА"),
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
    failed = []
    for iid, template, kind in ITEMS:
        weapon = kind == "weapon"
        preset = find_preset(globals_, items, template) or single(items, template)
        key = next(it["_tpl"] for it in preset["_items"] if it["_id"] == preset["_parent"])
        line_en, line_ru = spec_lines(kind, key, items, preset)
        specs.append((iid, kind, KINDS[kind][0], KINDS[kind][1], en[key + " ShortName"], en[key + " Name"],
                      ru.get(key + " ShortName", en[key + " ShortName"]), ru.get(key + " Name", en[key + " Name"]), line_en, line_ru))
        if only and iid not in only:
            continue
        for k in range(BUILDS if weapon else 1):
            name = iid if k == 0 else f"{iid}_{k}"
            try:
                if k == 0:
                    make(name, kind, preset, items, streaming, weapon, stock=True)
                    continue
                # A random build that came out with a part missing or loose is thrown away and
                # another tried; the last try stands if none comes out whole.
                for attempt in range(24):
                    check = []
                    build = random_build(items, preset, random.Random(f"{iid}-{k}-{attempt}"), streaming)
                    if sorted(x["_tpl"] for x in build["_items"]) == sorted(x["_tpl"] for x in preset["_items"]) and attempt < 23:
                        print(f"   {name} try {attempt + 1} rejected: same as the stock build")
                        continue
                    make(name, kind, build, items, streaming, weapon, check if attempt < 23 else None)
                    if not check:
                        break
                    print(f"   {name} try {attempt + 1} rejected: {'; '.join(check)}")
            except Exception as e:  # one item the game builds differently should not stop the rest
                failed.append(name)
                print(f"{name}: FAILED ({e})")
    write_specs(specs)
    if failed:
        print("FAILED: " + " ".join(failed))


def attached(parts, gap=0.015, problems=None):
    """
    Drops any part with clear air all round it: one whose box does not come within gap
    (metres) of another part's. A part seated wrong floats; a real one always touches.
    """
    boxes = [(t.reshape(-1, 3).min(axis=0) - gap, t.reshape(-1, 3).max(axis=0) + gap) for _, _, t in parts]
    keep = []
    for i, part in enumerate(parts):
        if part[0] == "weapon":
            keep.append(part)
            continue
        lo, hi = boxes[i]
        near = [j for j in range(len(parts)) if j != i and np.all(lo <= boxes[j][1]) and np.all(boxes[j][0] <= hi)]
        touches = False
        if near:
            # Boxes overlap easily (a sight's box takes in the air above a rail); the points must
            # come within a centimetre of another part's.
            mine = part[2].reshape(-1, 3)
            tree = cKDTree(np.concatenate([parts[j][2].reshape(-1, 3) for j in near]))
            touches = float(tree.query(mine, k=1)[0].min()) < 0.02
        if touches:
            keep.append(part)
        else:
            print(f"   dropped floating part {part[1]}")
            if problems is not None:
                problems.append(f"floating {part[1]}")
    return keep


def plated(items, preset):
    """Whether the carrier takes front and back plates."""
    root = next(it for it in preset["_items"] if it["_id"] == preset["_parent"])
    names = [sl["_name"].lower() for sl in items[root["_tpl"]]["_props"].get("Slots", [])]
    return "front_plate" in names and "back_plate" in names


def plates(tris):
    """
    Front and back plates inside a carrier, which the game keeps as separate items with no place
    in the model: SAPI-cut slabs (top corners cut away), in the chest and back of the vest as it
    is stored, z up and y through it.
    """
    pts = tris.reshape(-1, 3)
    lo, hi = pts.min(axis=0), pts.max(axis=0)
    size = hi - lo
    w, h, t = size[0] * 0.5, size[2] * 0.42, max(size[1] * 0.06, 0.008)
    cx, cz = (lo[0] + hi[0]) / 2, lo[2] + size[2] * 0.6
    cut = w * 0.22
    outline = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2 - cut), (w / 2 - cut, h / 2), (-w / 2 + cut, h / 2), (-w / 2, h / 2 - cut)]
    out = []
    for y in (lo[1] + size[1] * 0.12, hi[1] - size[1] * 0.12 - t):
        front = [np.array([cx + x, y, cz + z]) for x, z in outline]
        back = [p + np.array([0.0, t, 0.0]) for p in front]
        n = len(outline)
        for i in range(1, n - 1):
            out.append([front[0], front[i + 1], front[i]])
            out.append([back[0], back[i], back[i + 1]])
        for i in range(n):
            j = (i + 1) % n
            out.append([front[i], front[j], back[j]])
            out.append([front[i], back[j], back[i]])
    return np.array(out)


STOCK_PROBLEMS = set()


def make(iid, kind, preset, items, streaming, weapon, check=None, stock=False):
    """
    One model: assembled, cut down, turned the right way up, written. With check (a random
    build), what went wrong putting it together is listed there and nothing is written if
    anything did, so another build can be tried.
    """
    problems = []
    parts = assemble(streaming, items, preset, weapon, problems)
    if not parts:
        print(f"{iid}: no meshes")
        return
    if weapon:
        parts = attached(parts, problems=problems)
    if check is not None:
        # What the stock build has wrong too (a part the game models loose) is no reason to throw a build away.
        check.extend(x for x in problems if x not in STOCK_PROBLEMS)
        if check:
            return
    elif stock:
        STOCK_PROBLEMS.clear()
        STOCK_PROBLEMS.update(problems)
    tris = np.concatenate([p[2] for p in parts])
    if kind in ("armor", "rig") and plated(items, preset):
        tris = np.concatenate([tris, plates(tris)])
    raw = len(tris)
    verts, faces, grid = cut_down(tris, (BUDGET + 400) if weapon else BUDGET * 3 // 4)

    # Centred on its box. A weapon turned barrel along +x, top up; equipment stays as the
    # game holds it, y up.
    c = (verts.max(axis=0) + verts.min(axis=0)) / 2.0
    if weapon:
        verts, axes = orient(verts)
    else:
        verts, axes = verts - c, (0, 1, 2)
        if kind in ("armor", "backpack", "rig"):
            # Body armour and packs are character meshes, z-up: stood up a quarter turn.
            verts = np.c_[verts[:, 0], verts[:, 2], -verts[:, 1]]

    # A callout's anchor is the middle of its part's box, in the same space as the vertices.
    # The anchor is on the part itself: of its points, the one nearest the middle of its
    # box (the middle of a skeleton stock is air).
    by_slot = {}
    each = []
    for slot, name, t in parts:
        pts = (t.reshape(-1, 3) - c)[:, list(axes)]
        by_slot.setdefault(slot, []).append(pts)
        each.append((slot, name, pts))
    anchor_list = []
    if weapon:
        for slot, chunks in by_slot.items():
            pts = np.concatenate(chunks)
            middle = (pts.min(axis=0) + pts.max(axis=0)) / 2.0
            anchor_list.append((slot, pts[np.argmin(((pts - middle) ** 2).sum(axis=1))]))

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

        # Barrel along +x, top up now. A light or laser that stands up in front of the optic,
        # in line with it, blocks the sight picture: no one would build that.
        if check is not None and by_slot.get("mod_scope"):
            sight = np.concatenate(by_slot["mod_scope"]) * sign
            s_lo, s_hi = sight.min(axis=0), sight.max(axis=0)
            for slot, name, pts in each:
                if slot != "mod_tactical":
                    continue
                q = pts * sign
                q_lo, q_hi = q.min(axis=0), q.max(axis=0)
                ahead = (q_lo[0] + q_hi[0]) / 2 > s_lo[0]
                tall = q_hi[1] > s_lo[1] + 0.3 * (s_hi[1] - s_lo[1])
                in_line = s_lo[2] - 0.004 <= (q_lo[2] + q_hi[2]) / 2 <= s_hi[2] + 0.004
                if ahead and tall and in_line:
                    check.append(f"{name} blocks the optic")
            if check:
                return

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
    write(path, verts, faces, anchor_list)
    side_view(os.path.join(PREVIEW, f"{iid}.png"), verts, faces, edges)
    print(f"{iid}: {len(parts)} meshes, {raw} -> {len(faces)} triangles, {len(verts)} vertices, "
          f"{len(edges)} edges ({creases} creases), {os.path.getsize(path) // 1024} KB")


if __name__ == "__main__":
    main()
