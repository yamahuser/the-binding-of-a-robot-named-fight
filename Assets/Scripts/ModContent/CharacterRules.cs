using UnityEngine;

// [MODCONTENT] Per-frame character rules that need a watcher rather than a hook.
// Wraith: his hits refill every time he enters a new room (door, teleporter, respawn).
public class CharacterRules : MonoBehaviour
{
    private Room _lastRoom;

    [RuntimeInitializeOnLoadMethod]
    private static void Install()
    {
        var go = new GameObject("CharacterRules");
        DontDestroyOnLoad(go);
        go.AddComponent<CharacterRules>();
    }

    private float _nextDefault;

    private void Update()
    {
        var c = CharacterRoster.Current;

        // Volt: with no bolt, his default weapon is always the one in hand when nothing else is
        if (c.noBolt && Time.time >= _nextDefault)
        {
            _nextDefault = Time.time + 0.25f;
            var pl = Player.instance;
            if (pl && pl.state == DamageableState.Alive && pl.selectedEnergyWeapon == null) { CharacterRoster.SelectDefaultWeapon(pl); }
        }

        if (c.hitsPerRoom <= 0) { _lastRoom = null; return; }

        var room = LayoutManager.CurrentRoom;
        if (room == null || room == _lastRoom) { return; }
        _lastRoom = room;

        var p = Player.instance;
        if (p && p.state == DamageableState.Alive)
        {
            p.health = p.maxHealth;
        }
    }
}
