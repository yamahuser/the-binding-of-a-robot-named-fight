using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// [MODCONTENT] Playtest test bench. Active in the editor and in development builds.
//   F7  - enter/leave the big empty test arena
//   F9  - give every mod item and refill energy (Shift+F9: clear inventory, keeps movement items)
//   F10 - open/close the spawn menu (items on pedestals, DPS dummy, clear)
//   F11 - refill energy
//   F12 - spawn a DPS dummy in front of you
public class ModContentTestKeys : MonoBehaviour
{
    // Mod items to grant with F9 (empty until new content is added).
    public static readonly MajorItem[] Items = { };

    private enum Tab { Mod, EnergyWeapons, ShotMods, Everything }

    private bool _menuOpen;
    private Tab _tab;
    private Vector2 _scroll;
    private int _slot;
    private bool _prevCursorVisible;
    private CursorLockMode _prevLock;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Player _player;

    [RuntimeInitializeOnLoadMethod]
    private static void Install()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) { return; }
        var go = new GameObject("ModContentTestKeys");
        DontDestroyOnLoad(go);
        go.AddComponent<ModContentTestKeys>();
        Debug.Log("[MODCONTENT] Test bench active: F7 test arena, F9 give mod items, F10 spawn menu, F11 refill energy, F12 DPS dummy");
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) { return; }
        _player = Player.instance ? Player.instance : FindObjectOfType<Player>();
        if (!_player) { if (_menuOpen) SetMenu(false); return; }

        TestArena.CheckAbandoned(_player);
        if (kb.f7Key.wasPressedThisFrame) { TestArena.Toggle(_player); }
        if (kb.f9Key.wasPressedThisFrame)
        {
            if (kb.shiftKey.isPressed) { ClearInventory(true); } else { GiveAll(Items); }
        }
        if (kb.f10Key.wasPressedThisFrame) { SetMenu(!_menuOpen); }
        if (kb.f11Key.wasPressedThisFrame) { _player.energy = _player.maxEnergy; }
        if (kb.f12Key.wasPressedThisFrame) { SpawnDummy(); }
        if (_menuOpen && kb.escapeKey.wasPressedThisFrame) { SetMenu(false); }

        _spawned.RemoveAll(o => o == null);
    }

    private void SetMenu(bool open)
    {
        if (open == _menuOpen) { return; }
        _menuOpen = open;
        if (open)
        {
            _prevCursorVisible = Cursor.visible;
            _prevLock = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = _prevCursorVisible;
            Cursor.lockState = _prevLock;
        }
    }

    // ---------- actions ----------

    private void GiveAll(IEnumerable<MajorItem> items)
    {
        foreach (var item in items)
        {
            if (ItemManager.items.ContainsKey(item)) { _player.CollectMajorItem(item); }
        }
        _player.energy = _player.maxEnergy;
        Debug.Log("[MODCONTENT] Granted items");
    }

    private void RemoveItems(IEnumerable<MajorItem> items)
    {
        foreach (var item in items.ToList())
        {
            if (_player.itemsPossessed.Contains(item)) { _player.RemoveItem(item); }
        }
        if (SaveGameManager.activeGame != null)
        {
            SaveGameManager.activeGame.itemsPossessed = _player.itemsPossessed.ToList();
        }
    }

    /// <summary>Remove owned items. keepTraversal keeps jump/dash/shell items so you can still get around the map.</summary>
    private void ClearInventory(bool keepTraversal)
    {
        var toRemove = _player.itemsPossessed.Where(i =>
        {
            MajorItemInfo info;
            if (!ItemManager.items.TryGetValue(i, out info)) { return true; }
            return !(keepTraversal && info.isTraversalItem && !info.isEnergyWeapon);
        }).ToList();
        RemoveItems(toRemove);
        Debug.Log("[MODCONTENT] Cleared " + toRemove.Count + " items" + (keepTraversal ? " (kept movement items)" : ""));
    }

    private Vector3 SlotPosition(float distance)
    {
        var facing = Mathf.Sign(_player.transform.right.x);
        var p = _player.transform.position + Vector3.right * facing * distance;
        p.y += 0.5f;
        return p;
    }

    private void SpawnItem(MajorItem item)
    {
        var prefab = Resources.Load<GameObject>("MajorItemPickUps/" + item);
        if (!prefab) { Debug.LogError("[MODCONTENT] No pickup prefab for " + item); return; }

        // Lay items out in a row in front of the player, 1.5 units apart, wrapping after 6.
        var pos = SlotPosition(2f + (_slot % 6) * 1.5f);
        _slot++;
        var pickup = Instantiate(prefab, pos, Quaternion.identity, BenchParent);
        var pedestal = ItemPedestal.PlaceUnder(pickup);
        _spawned.Add(pickup);
        if (pedestal) { _spawned.Add(pedestal); }
    }

    private void SpawnDummy()
    {
        var pos = SlotPosition(5f);
        pos.y = ItemPedestal.FindGround(pos + Vector3.up);
        var dummy = DpsDummy.Spawn(pos, BenchParent);
        _spawned.Add(dummy.gameObject);
    }

    private Transform BenchParent { get { return TestArena.inside ? TestArena.root : _player.transform.parent; } }

    private void SwitchCharacter(CharacterDef cd, bool giveKit)
    {
        var game = SaveGameManager.activeGame;
        if (game == null) { return; }
        game.character = cd.id;
        if (giveKit)
        {
            foreach (var item in cd.startingItems)
            {
                MajorItemInfo info;
                if (!ItemManager.items.TryGetValue(item, out info)) continue;
                if (info.isActivatedItem) { _player.CollectMajorItem(item); }
                else if (!_player.itemsPossessed.Contains(item)) { _player.CollectMajorItem(item); }
            }
            _player.grayScrap = Mathf.Min(99, _player.grayScrap + cd.startingScrap);
        }
        _player.MatchItems(true, SpriteMakerMode.Force);
        _player.energy = _player.maxEnergy;
        Debug.Log("[MODCONTENT] Character -> " + cd.id + (giveKit ? " (+kit)" : ""));
    }

    private void ClearBench()
    {
        foreach (var o in _spawned) { if (o) Destroy(o); }
        _spawned.Clear();
        _slot = 0;
    }

    // ---------- item lists ----------

    private IEnumerable<MajorItemInfo> ListFor(Tab tab)
    {
        var all = ItemManager.items.Values;
        switch (tab)
        {
            case Tab.Mod: return all.Where(i => ItemPedestal.IsModItem(i.type)).OrderBy(i => (int)i.type);
            case Tab.EnergyWeapons: return all.Where(i => i.isEnergyWeapon).OrderBy(i => i.fullName);
            case Tab.ShotMods:
                return all.Where(i => !i.isEnergyWeapon && !i.isActivatedItem && !i.follower &&
                                      (i.projectileType != ProjectileType.Generic || i.applyDamageTypeToProjectile ||
                                       i.projectileStatusEffects != null && i.projectileStatusEffects.Length > 0 ||
                                       i.penetrativeShot || i.homing > 0 || i.bounce || i.arcShots > 0 ||
                                       ((int)i.type >= 200 && (int)i.type < 300)))
                          .OrderBy(i => i.fullName);
            default: return all.OrderBy(i => i.fullName);
        }
    }

    // ---------- UI ----------

    private void OnGUI()
    {
        DrawDummyPanel();
        if (!_menuOpen || !_player) { return; }

        var scale = Mathf.Max(1f, Screen.height / 720f);
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        var w = 360f;
        var h = Screen.height / scale - 40;
        GUILayout.BeginArea(new Rect(20, 20, w, h), "Test Bench (F10 / Esc to close)", GUI.skin.window);

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(_tab == Tab.Mod, "Mod", GUI.skin.button)) _tab = Tab.Mod;
        if (GUILayout.Toggle(_tab == Tab.EnergyWeapons, "Weapons", GUI.skin.button)) _tab = Tab.EnergyWeapons;
        if (GUILayout.Toggle(_tab == Tab.ShotMods, "Shot mods", GUI.skin.button)) _tab = Tab.ShotMods;
        if (GUILayout.Toggle(_tab == Tab.Everything, "All", GUI.skin.button)) _tab = Tab.Everything;
        GUILayout.EndHorizontal();

        if (GUILayout.Button(TestArena.inside ? "Leave test arena (F7)" : "Enter big empty test arena (F7)")) { TestArena.Toggle(_player); _slot = 0; }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn DPS dummy")) SpawnDummy();
        if (GUILayout.Button("Reset dummies")) foreach (var d in DpsDummy.all) d.ResetStats();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Give whole tab")) GiveAll(ListFor(_tab).Select(i => i.type).ToList());
        if (GUILayout.Button("Refill energy")) _player.energy = _player.maxEnergy;
        if (GUILayout.Button("Clear bench")) ClearBench();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear inventory (keep movement)")) ClearInventory(true);
        if (GUILayout.Button("Clear ALL")) ClearInventory(false);
        GUILayout.EndHorizontal();

        GUILayout.Label("Character: " + CharacterRoster.Current.name + " (switch for testing)");
        GUILayout.BeginHorizontal();
        for (int ci = 0; ci < CharacterRoster.All.Count; ci++)
        {
            var cd = CharacterRoster.All[ci];
            if (GUILayout.Toggle(CharacterRoster.Current == cd, cd.name.Substring(0, Mathf.Min(4, cd.name.Length)), GUI.skin.button)
                && CharacterRoster.Current != cd)
            {
                SwitchCharacter(cd, false);
            }
            if (ci == 3) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
        }
        if (GUILayout.Button("+ kit")) SwitchCharacter(CharacterRoster.Current, true);
        GUILayout.EndHorizontal();

        GUILayout.Label("Click: drop on a pedestal.  Give: equip instantly.");
        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (var info in ListFor(_tab))
        {
            GUILayout.BeginHorizontal();
            var owned = _player.itemsPossessed != null && _player.itemsPossessed.Contains(info.type);
            GUILayout.Label(info.fullName + (owned ? "  (owned)" : ""), GUILayout.Width(200));
            if (GUILayout.Button("Pedestal")) SpawnItem(info.type);
            if (owned) { if (GUILayout.Button("Remove")) RemoveItems(new[] { info.type }); }
            else if (GUILayout.Button("Give")) GiveAll(new[] { info.type });
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = matrix;
    }

    private void DrawSynergyPanel()
    {
        if (!_player || (!_menuOpen && DpsDummy.all.Count == 0)) { return; }
        var scale = Mathf.Max(1f, Screen.height / 720f);
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        var sw = Screen.width / scale;
        var sh = Screen.height / scale;
        var syn = WeaponSynergy.ActiveSynergies(_player);
        var text = string.Format("Synergies ({0}): {1}\nEnergy cost x{2:0.00}   Dmg x{3:0.00}  Rate x{4:0.00}  Size x{5:0.00}  Speed x{6:0.00}",
            syn.Count, syn.Count == 0 ? "none" : string.Join(", ", syn.ToArray()), WeaponSynergy.CostMultiplier(_player),
            WeaponSynergy.Damage(_player), WeaponSynergy.FireRate(_player), WeaponSynergy.Size(_player), WeaponSynergy.ShotSpeed(_player));
        GUI.Box(new Rect(sw - 520, sh - 64, 500, 50), "");
        GUI.Label(new Rect(sw - 512, sh - 60, 490, 46), text);
        GUI.matrix = matrix;
    }

    private void DrawDummyPanel()
    {
        DrawSynergyPanel();
        if (DpsDummy.all.Count == 0) { return; }
        var scale = Mathf.Max(1f, Screen.height / 720f);
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        var sw = Screen.width / scale;
        float y = 20;
        for (int i = 0; i < DpsDummy.all.Count; i++)
        {
            var d = DpsDummy.all[i];
            var weapon = _player && _player.selectedEnergyWeapon ? _player.selectedEnergyWeapon.item.ToString() : "Base bolt";
            GUI.Box(new Rect(sw - 250, y, 230, 130), "");
            GUI.Label(new Rect(sw - 242, y + 4, 220, 125), "DPS Dummy " + (i + 1) + "  [" + weapon + "]\n" + d.Report());
            y += 138;
        }
        GUI.matrix = matrix;
    }
}
