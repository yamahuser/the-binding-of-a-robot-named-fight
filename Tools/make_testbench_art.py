"""[MODCONTENT] Pixel art for the item pedestal and the DPS training dummy.
Writes Assets/Resources/ModContent/{ItemPedestal,DpsDummy}.png + metas (sprite, PPU 16, point).
"""
import os, re, uuid
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources")
OUT = os.path.join(ROOT, "ModContent")
os.makedirs(OUT, exist_ok=True)
TEMPLATE_META = os.path.join(ROOT, "Sprites", "Items", "MachineGun.png.meta")

PAL = {
    ".": (0, 0, 0, 0),
    "k": (20, 16, 28, 255),     # outline
    "d": (52, 56, 74, 255),     # dark steel
    "m": (92, 100, 122, 255),   # mid steel
    "l": (150, 160, 182, 255),  # light steel
    "w": (222, 230, 240, 255),  # highlight
    "c": (60, 220, 255, 255),   # cyan glow
    "C": (170, 245, 255, 255),  # cyan core
    "r": (210, 40, 48, 255),    # target red
    "R": (130, 20, 30, 255),    # dark red
    "y": (255, 200, 60, 255),   # warning yellow
}

PEDESTAL = [  # 16 x 12, pivot bottom-center
    "kkkkkkkkkkkkkkkk",
    "kwllllllllllllmk",
    "kcCCCCCCCCCCCCck",
    "kkmmmmmmmmmmmmkk",
    "..kdllmmmmmldk..",
    "..kdlmmmmmmmdk..",
    "..kdlmyddymmdk..",
    "..kdlmmmmmmmdk..",
    ".kkdlmmmmmmmdkk.",
    "kwlllmmmmmmmmddk",
    "kmmmmmddddddddkk",
    "kkkkkkkkkkkkkkkk",
]

DUMMY = [  # 16 x 28, pivot bottom-center
    "......kkkk......",
    ".....kllllk.....",
    "....kllwwllk....",
    "....klcCCclk....",
    "....kllmmllk....",
    ".....kkmmkk.....",
    "...kkkkddkkkk...",
    "..kmmmmmmmmmmk..",
    ".kmlwwwwwwwwlmk.",
    ".kmwrrrrrrrrwmk.",
    ".kmwrwwwwwwrwmk.",
    ".kmwrwrrrrwrwmk.",
    ".kmwrwrCCrwrwmk.",
    ".kmwrwrCCrwrwmk.",
    ".kmwrwrrrrwrwmk.",
    ".kmwrwwwwwwrwmk.",
    ".kmwrrrrrrrrwmk.",
    ".kmlwwwwwwwwlmk.",
    "..kmmmmmmmmmmk..",
    "...kkkkddkkkk...",
    "......kdmk......",
    "......kdmk......",
    "......kdmk......",
    "......kdmk......",
    "....kkkdmkkk....",
    "..kkylyllylykk..",
    ".kmmmmmmmmmmmmk.",
    "kkkkkkkkkkkkkkkk",
]

def write(name, rows, bottom_pivot=True):
    h, w = len(rows), len(rows[0])
    im = Image.new("RGBA", (w, h))
    for y, row in enumerate(rows):
        assert len(row) == w, (name, y, len(row))
        for x, ch in enumerate(row):
            im.putpixel((x, y), PAL[ch])
    path = os.path.join(OUT, name + ".png")
    im.save(path)
    meta = path + ".meta"
    g = re.search(r"guid: (\w+)", open(meta).read()).group(1) if os.path.exists(meta) else uuid.uuid4().hex
    txt = open(TEMPLATE_META, encoding="utf-8").read()
    txt = re.sub(r"guid: [0-9a-f]{32}", "guid: " + g, txt, count=1)
    # bottom-center pivot so the sprite stands on the ground
    if bottom_pivot:
        txt = re.sub(r"  alignment: \d+", "  alignment: 7", txt, count=1)
        txt = re.sub(r"  spritePivot: \{x: [\d.]+, y: [\d.]+\}", "  spritePivot: {x: 0.5, y: 0}", txt, count=1)
    open(meta, "w", encoding="utf-8", newline="\n").write(txt)

# folder meta
fm = OUT + ".meta"
if not os.path.exists(fm):
    open(fm, "w", newline="\n").write("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n"
                                      "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                                      % uuid.uuid4().hex)
write("ItemPedestal", PEDESTAL)
write("DpsDummy", DUMMY)

PAL["g"] = (26, 30, 44, 255)   # backdrop base
PAL["h"] = (34, 40, 58, 255)   # backdrop grid
PAL["b"] = (44, 120, 150, 255) # backdrop accent

ARENA_TILE = [  # 16 x 16 riveted steel panel
    "kkkkkkkkkkkkkkkk",
    "kwlllllllllllllk",
    "klwmmmmmmmmmmmdk",
    "klmlmmmmmmmmlmdk",
    "klmmmmmmmmmmmmdk",
    "klmmmmmmmmmmmmdk",
    "klmmmmddddmmmmdk",
    "klmmmmdmmdmmmmdk",
    "klmmmmdmmdmmmmdk",
    "klmmmmddddmmmmdk",
    "klmmmmmmmmmmmmdk",
    "klmmmmmmmmmmmmdk",
    "klmlmmmmmmmmlmdk",
    "klmmmmmmmmmmmmdk",
    "kddddddddddddddk",
    "kkkkkkkkkkkkkkkk",
]

ARENA_BACKDROP = [  # 32 x 32 dark grid
    "h" * 32,
] + ["h" + "g" * 15 + "h" + "g" * 15 for _ in range(15)] + [
    "h" * 32,
] + ["h" + "g" * 15 + "h" + "g" * 15 for _ in range(15)]
ARENA_BACKDROP[16] = "h" * 15 + "b" + "h" * 16
ARENA_BACKDROP[0] = "h" * 15 + "b" + "h" * 16

write("ArenaTile", ARENA_TILE, bottom_pivot=False)
write("ArenaBackdrop", ARENA_BACKDROP, bottom_pivot=False)

PAL["p"] = (70, 30, 110, 255)    # brood dark purple
PAL["P"] = (140, 70, 200, 255)   # brood purple
PAL["q"] = (210, 150, 255, 255)  # brood highlight
PAL["e"] = (255, 240, 120, 255)  # eye
PAL["E"] = (20, 10, 10, 255)     # pupil

SWARM_BROOD = [  # 12 x 12 familiar orb
    "....kkkk....",
    "..kkpPPpkk..",
    ".kpPqqPPPpk.",
    ".kPqPPPPPPk.",
    "kpPPkkkkPPpk",
    "kPPkeeeekPPk",
    "kPPkeEEekPPk",
    "kpPPkkkkPPpk",
    ".kPPPPPPPPk.",
    ".kpPPPPPPpk.",
    "..kkpppPkk..",
    "....kkkk....",
]
write("SwarmBrood", SWARM_BROOD, bottom_pivot=False)
print("testbench art written")
