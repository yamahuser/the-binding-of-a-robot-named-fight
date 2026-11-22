using UnityEngine;

// [MODCONTENT] Swarmlord's Brood: a lagging orb that every one of his shots fires from
// (bolt, charge shot, every energy weapon) via the Player.GetAimingInfo hook. It inherits
// all his stats and mods because it is literally his gun. It trails behind him, so aiming
// is awkward, and when it drifts into a wall the shot is blocked there.
public class SwarmBrood : MonoBehaviour
{
    private static SwarmBrood _instance;

    public Player player;
    private Vector3 _velocity;
    private SpriteRenderer _renderer;
    private float _t;
    private float _nextLink;

    public const float Lag = 0.22f;
    public static readonly Vector2 Offset = new Vector2(-1.1f, 1.3f); // behind and above, relative to facing

    /// <summary>The Brood for this player, created on demand. Null when the character doesn't use one.</summary>
    public static SwarmBrood For(Player p)
    {
        if (!p || !CharacterRoster.Current.firesFromFamiliar || p.GetType() != typeof(Player))
        {
            if (_instance) { Destroy(_instance.gameObject); }
            return null;
        }

        if (!_instance)
        {
            var go = new GameObject("SwarmBrood");
            _instance = go.AddComponent<SwarmBrood>();
            _instance.player = p;
            _instance.transform.position = p.transform.position + (Vector3)Offset;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("ModContent/SwarmBrood");
            var shader = Shader.Find("Sprites/DefaultFlash");
            if (shader) { sr.material = new Material(shader); }
            var pr = p.mainRenderer;
            if (pr) { sr.sortingLayerID = pr.sortingLayerID; sr.sortingOrder = pr.sortingOrder + 2; }
            _instance._renderer = sr;
        }
        _instance.player = p;
        return _instance;
    }

    /// <summary>Where shots leave the Brood.</summary>
    public Vector3 Muzzle(Vector3 direction)
    {
        return transform.position + direction.normalized * 0.35f;
    }

    private void Update()
    {
        if (!player || !CharacterRoster.Current.firesFromFamiliar) { Destroy(gameObject); return; }

        _t += Time.deltaTime;
        var facing = Mathf.Sign(player.transform.right.x);
        var target = player.transform.position + new Vector3(Offset.x * facing, Offset.y + Mathf.Sin(_t * 3f) * 0.15f, 0);
        target.z = player.transform.position.z;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, Lag);

        if (_renderer) { _renderer.flipX = facing < 0; }

        // minion synergy: every orb he owns scales with his stats
        if (Time.time >= _nextLink && CharacterRoster.Current.minionSynergy)
        {
            _nextLink = Time.time + 0.5f;
            foreach (var f in player.followers) { MinionLink.Ensure(f); }
        }

        // keep it close if the player teleports / changes rooms
        if ((transform.position - target).sqrMagnitude > 100f) { transform.position = target; _velocity = Vector3.zero; }
    }

    private void OnDestroy() { if (_instance == this) _instance = null; }
}
