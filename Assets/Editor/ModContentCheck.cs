using UnityEditor;
using UnityEngine;

// [MODCONTENT] Headless sanity check: Unity.exe -batchmode -quit -executeMethod ModContentCheck.Run
public static class ModContentCheck
{
    [MenuItem("Mod Content/Run Asset Check")]
    public static void Run()
    {
        int bad = 0;
        foreach (var item in ModContentTestKeys.Items)
        {
            MajorItemInfo info = null;
            ItemManager.items.TryGetValue(item, out info);
            var icon = Resources.Load<Sprite>("Sprites/Items/" + item);
            var pickup = Resources.Load<GameObject>("MajorItemPickUps/" + item);
            string weapon = "-";
            if (info != null && info.isEnergyWeapon)
            {
                var w = Resources.Load<PlayerEnergyWeapon>("EnergyWeapons/" + item);
                weapon = w ? w.GetType().Name + "(item " + w.item + ")" : "MISSING";
                if (!w || w.item != item) bad++;
            }
            var ok = info != null && info.type == item && icon && pickup && info.icon;
            if (!ok) bad++;
            Debug.Log(string.Format("[MODCONTENT-CHECK] {0}: info={1} icon={2} pickup={3} weapon={4}",
                item, info ? info.fullName : "MISSING", icon ? "ok" : "MISSING", pickup ? "ok" : "MISSING", weapon));
        }
        Debug.Log("[MODCONTENT-CHECK] problems=" + bad);
    }
}
