"""[MODCONTENT] Remove mod item IDs (240-259, 360-399) from the playtest save, swap any
layout placements for a vanilla item, and move a respawn point that ended up in the test
arena back to the run's starting room. Back up the save before running.

python Tools/strip_mod_items_from_save.py [replacement_item_id]
"""
import json, os, sys

SAVE_DIR = os.path.join(os.environ["USERPROFILE"], "AppData", "LocalLow", "OpenARNF Local Playtests", "Samus Sprite Playtest")
REPLACEMENT = int(sys.argv[1]) if len(sys.argv) > 1 else 205  # Laser Beam

def is_mod(v):
    return isinstance(v, int) and not isinstance(v, bool) and (240 <= v < 260 or 360 <= v < 400)

changes = []

def strip_lists(o, path=""):
    """Drop mod ids from player-owned lists."""
    if isinstance(o, dict):
        for k, v in o.items():
            if isinstance(v, list) and any(is_mod(x) for x in v):
                o[k] = [x for x in v if not is_mod(x)]
                changes.append("removed from %s%s" % (path, k))
            else:
                strip_lists(v, path + k + ".")
    elif isinstance(o, list):
        for x in o:
            strip_lists(x, path)

ITEM_KEYS = {"majItm", "shop", "shopItms", "nonTravs", "bonusItms", "itmOrd", "StrtPsv", "StrtAct", "StrtWep", "StrtOrb", "travItm"}

def replace_ids(o, path=""):
    """Swap mod ids for a vanilla item, only in known item fields (other int lists are object ids)."""
    if isinstance(o, dict):
        for k, v in o.items():
            if k in ITEM_KEYS and is_mod(v):
                o[k] = REPLACEMENT
                changes.append("replaced %s%s" % (path, k))
            elif k in ITEM_KEYS and isinstance(v, list) and any(is_mod(x) for x in v):
                o[k] = [REPLACEMENT if is_mod(x) else x for x in v]
                changes.append("replaced in %s%s" % (path, k))
            elif isinstance(v, (dict, list)):
                replace_ids(v, path + k + ".")
    elif isinstance(o, list):
        for x in o:
            replace_ids(x, path)

slot_path = os.path.join(SAVE_DIR, "SaveFileSlot0.dat")
layout_path = os.path.join(SAVE_DIR, "SaveFileSlot0Layout.dat")
slot = json.load(open(slot_path, encoding="utf-8"))
layout = json.load(open(layout_path, encoding="utf-8"))

agd = slot.get("agd") or {}
if is_mod(agd.get("curItem")):
    agd["curItem"] = 0
    changes.append("cleared agd.curItem")
strip_lists(slot)
replace_ids(layout)

# Respawn point stuck in the test arena (-3000,-3000 region)?
strt = agd.get("strt")
if strt and strt.get("x", 0) < -1000:
    start = next((r for r in layout["rmAbs"] if r.get("strtRm")), None)
    sp = layout["strtPos"]
    if start is None:
        start = next(r for r in layout["rmAbs"] if r["gridPosition"] == sp)
    wp = start["worldPosition"]
    off = start["rmnfo"].get("pStrt") or {"x": 0, "y": 0}
    for k in ("x", "y"):
        strt[k] = wp[k] + off.get(k, 0)
    agd["lstRm"] = {"x": start["gridPosition"]["x"], "y": start["gridPosition"]["y"]}
    changes.append("moved respawn point to start room %s -> (%.1f, %.1f)" % (start["rmnfo"].get("scnNm"), strt["x"], strt["y"]))

json.dump(slot, open(slot_path, "w", encoding="utf-8"), separators=(",", ":"))
json.dump(layout, open(layout_path, "w", encoding="utf-8"), separators=(",", ":"))
print("\n".join(changes) or "nothing to change")
