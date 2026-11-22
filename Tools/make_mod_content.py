"""[MODCONTENT] Generates Unity assets for the added weapons/items by cloning
existing templates with fresh GUIDs. Re-runnable: overwrites its own outputs but
keeps GUIDs stable (reuses an existing .meta if present).

Run from anywhere:  python Tools/make_mod_content.py
"""
import colorsys, os, re, shutil, uuid
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets")
RES = os.path.join(ROOT, "Resources")
SCRIPTS = os.path.join(ROOT, "Scripts", "Player")

def p(*a): return os.path.join(RES, *a)

def guid_of(meta_path):
    with open(meta_path, encoding="utf-8") as f:
        return re.search(r"guid: ([0-9a-f]{32})", f.read()).group(1)

def write_meta_from(template_meta, out_meta):
    """Copy a template .meta with a new guid (or keep the existing guid)."""
    g = guid_of(out_meta) if os.path.exists(out_meta) else uuid.uuid4().hex
    with open(template_meta, encoding="utf-8") as f:
        txt = f.read()
    txt = re.sub(r"guid: [0-9a-f]{32}", "guid: " + g, txt, count=1)
    with open(out_meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(txt)
    return g

def script_meta(cs_name):
    meta = os.path.join(SCRIPTS, cs_name + ".cs.meta")
    if not os.path.exists(meta):
        with open(meta, "w", newline="\n") as f:
            f.write("fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n"
                    "  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
                    "  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                    % uuid.uuid4().hex)
    return guid_of(meta)

def hue_png(src, dst, hue, sat=1.0, val=1.0):
    im = Image.open(src).convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0: continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s > 0.12:  # leave greys/outlines alone
                h = hue if hue is not None else h
                s = min(1, s * sat)
            v = min(1, v * val)
            r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v)
            px[x, y] = (round(r2 * 255), round(g2 * 255), round(b2 * 255), a)
    im.save(dst)

def make_icon(name, template, hue, sat=1.0, val=1.0):
    src = p("Sprites", "Items", template + ".png")
    dst = p("Sprites", "Items", name + ".png")
    hue_png(src, dst, hue, sat, val)
    return write_meta_from(src + ".meta", dst + ".meta")

def copy_arms(name, template):
    for side in ("FrontArm", "BackArm"):
        src = p("PlayerParts", template + side + ".png")
        if not os.path.exists(src): continue
        dst = p("PlayerParts", name + side + ".png")
        shutil.copyfile(src, dst)
        write_meta_from(src + ".meta", dst + ".meta")

def clone_text(src, dst, subs):
    with open(src, encoding="utf-8") as f:
        txt = f.read()
    for pat, rep in subs:
        new, n = re.subn(pat, rep, txt, flags=re.M)
        if n == 0: raise SystemExit("pattern not found in %s: %s" % (src, pat))
        txt = new
    with open(dst, "w", encoding="utf-8", newline="\n") as f:
        f.write(txt)
    write_meta_from(src + ".meta", dst + ".meta")

def info(name, template_dir, template, item_id, icon_guid, full, desc, page, extra=()):
    subs = [
        (r"^  m_Name: .*$", "  m_Name: " + name),
        (r"^  fullName: .*$", "  fullName: " + full),
        (r"^  description: .*$", "  description: " + desc),
        (r"^  itemPageDescription: .*$", "  itemPageDescription: " + page),
        (r"^  icon: .*$", "  icon: {fileID: 21300000, guid: %s, type: 3}" % icon_guid),
        (r"^  advancedDescription: .*\n(    type: 3}\n)?", "  advancedDescription: {fileID: 0}\n"),
        (r"^  type: \d+$", "  type: %d" % item_id),
    ] + list(extra)
    clone_text(p("MajorItemInfos", template_dir, template + ".asset"),
               p("MajorItemInfos", "OtherItems", name + ".asset"), subs)

def pickup(name, item_id, icon_guid):
    clone_text(p("MajorItemPickUps", "MachineGun.prefab"), p("MajorItemPickUps", name + ".prefab"), [
        (r"^  m_Name: MachineGun$", "  m_Name: " + name),
        (r"^  itemType: \d+$", "  itemType: %d" % item_id),
        (r"m_Sprite: \{fileID: 21300000, guid: 77cd0ce39657d2547a5907b71d556418",
         "m_Sprite: {fileID: 21300000, guid: " + icon_guid),
    ])

# --- projectileStats block taken from MachineGun so every field is present ---
with open(p("EnergyWeapons", "MachineGun.asset"), encoding="utf-8") as f:
    MG = f.read()
PS_BLOCK = re.search(r"^  projectileStats:\n(?:    .*\n|      .*\n)+", MG, re.M).group(0)

def ps(**kw):
    blk = PS_BLOCK
    for k, v in kw.items():
        blk, n = re.subn(r"^    %s: .*$" % k, "    %s: %s" % (k, v), blk, count=1, flags=re.M)
        if n == 0: raise SystemExit("ps field missing: " + k)
    return blk

def weapon_asset(name, script_guid, icon_guid, item_id, bar, body, min_energy=0.25, spin=0):
    head = ("%%YAML 1.1\n%%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier: \n"
            "  icon: {fileID: 21300000, guid: %s, type: 3}\n  barColor: {r: %s, g: %s, b: %s, a: 1}\n"
            "  item: %d\n  allowSpinJumping: %d\n  minEnergy: %s\n") % (
            script_guid, name, icon_guid, bar[0], bar[1], bar[2], item_id, spin, min_energy)
    dst = p("EnergyWeapons", name + ".asset")
    with open(dst, "w", encoding="utf-8", newline="\n") as f:
        f.write(head + body)
    write_meta_from(p("EnergyWeapons", "MachineGun.asset.meta"), dst + ".meta")

SND_MG = "{fileID: 8300000, guid: d40e5087319f7b0418424d23af839ab8, type: 3}"
SND_ROCKET = "{fileID: 8300000, guid: 545cc6b7823f0824ab45d449bd5e6e39, type: 3}"
SND_SCATTER = "{fileID: 8300000, guid: 9c24f30035920934dbc9366f9fa6d758, type: 3}"
SLOW = "{fileID: 11400000, guid: %s, type: 2}" % guid_of(p("StatusEffects", "NormalSlow.asset.meta"))
ACID = "{fileID: 11400000, guid: %s, type: 2}" % guid_of(p("StatusEffects", "NormalAcid.asset.meta"))
PEW = "c526a86c2e3be0940a9b4719cff29615"  # ProjectileEnergyWeapon script guid

WEAPON_FLAGS = [(r"^  deathmatch: \d$", "  deathmatch: 0")]

# ---------------- Item definitions go here ----------------
# Example shape:
#   g = make_icon("MyGun", "MachineGun", hue)      # recoloured icon
#   copy_arms("MyGun", "MachineGun")                # arm sprite
#   weapon_asset(...) or clone_text(EnergyWeapons/MachineGun.asset, ...)
#   info("MyGun", "OtherItems", "MachineGun", 240, g, "Name", "Desc", "Page desc", WEAPON_FLAGS)
#   pickup("MyGun", 240, g)

print("mod content generated")
