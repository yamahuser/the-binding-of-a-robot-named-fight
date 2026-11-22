using System.Collections.Generic;
using UnityEngine;

// [MODCONTENT] Unkillable training dummy that measures incoming damage.
// It is a real Enemy (Enemy layer, registered with EnemyManager) so homing,
// status effects, explosions and pierce all behave like a normal target.
public class DpsDummy : Enemy
{
    public const float RollingWindow = 5f;
    public const float ResetAfterIdle = 3f;

    private struct Hit { public float time; public float damage; }
    private struct Floater { public float time; public float damage; public Vector3 offset; }

    private readonly List<Hit> _hits = new List<Hit>();
    private readonly List<Floater> _floaters = new List<Floater>();
    private float _total;
    private int _hitCount;
    private float _firstHit = -1;
    private float _lastHit = -1;
    private float _biggest;
    private float _peakDps;

    public static readonly List<DpsDummy> all = new List<DpsDummy>();

    public static DpsDummy Spawn(Vector3 groundPosition, Transform parent)
    {
        var go = new GameObject("DpsDummy");
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.SetParent(parent, true);
        go.transform.position = groundPosition;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Resources.Load<Sprite>("ModContent/DpsDummy");
        var shader = Shader.Find("Sprites/DefaultFlash");
        if (shader) { sr.material = new Material(shader); }
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 5;

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(0.85f, 1.7f);
        box.offset = new Vector2(0, 0.85f);

        var dummy = go.AddComponent<DpsDummy>();
        dummy.health = 1000000000f;
        dummy.maxHealth = dummy.health;
        return dummy;
    }

    protected override void Start()
    {
        base.Start();
        all.Add(this);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        all.Remove(this);
    }

    public override void HandleDamage(float damageAmount)
    {
        var now = Time.time;
        if (_firstHit < 0 || now - _lastHit > ResetAfterIdle) { ResetStats(); _firstHit = now; }

        _lastHit = now;
        _total += damageAmount;
        _hitCount++;
        _biggest = Mathf.Max(_biggest, damageAmount);
        _hits.Add(new Hit { time = now, damage = damageAmount });
        _floaters.Add(new Floater { time = now, damage = damageAmount, offset = new Vector3(Random.Range(-0.4f, 0.4f), 0, 0) });
        health = maxHealth; // never dies
    }

    public override void StartDeath() { health = maxHealth; }

    public void ResetStats()
    {
        _hits.Clear();
        _total = 0;
        _hitCount = 0;
        _firstHit = -1;
        _lastHit = -1;
        _biggest = 0;
        _peakDps = 0;
    }

    protected override void Update()
    {
        base.Update();
        var now = Time.time;
        _hits.RemoveAll(h => now - h.time > RollingWindow);
        _floaters.RemoveAll(f => now - f.time > 0.9f);
        _peakDps = Mathf.Max(_peakDps, RollingDps);
    }

    public bool active { get { return _firstHit >= 0 && Time.time - _lastHit <= ResetAfterIdle; } }

    /// <summary>Damage over the last few seconds, divided by the time actually covered.</summary>
    public float RollingDps
    {
        get
        {
            if (_hits.Count == 0) { return 0; }
            float sum = 0;
            foreach (var h in _hits) { sum += h.damage; }
            var span = Mathf.Clamp(Time.time - Mathf.Max(_firstHit, Time.time - RollingWindow), 1f, RollingWindow);
            return sum / span;
        }
    }

    public float SessionDps
    {
        get
        {
            if (_firstHit < 0) { return 0; }
            var end = active ? Time.time : _lastHit;
            return _total / Mathf.Max(end - _firstHit, 1f);
        }
    }

    public string Report()
    {
        return string.Format("DPS (last {0:0}s): {1:0.0}\nPeak DPS: {2:0.0}\nSession DPS: {3:0.0}\nTotal: {4:0.0}   Hits: {5}\nBiggest hit: {6:0.0}{7}",
            RollingWindow, RollingDps, _peakDps, SessionDps, _total, _hitCount, _biggest,
            _firstHit < 0 ? "\n(shoot me)" : active ? "" : "\n(idle - next hit resets)");
    }

    private void OnGUI()
    {
        var cam = MainCamera.instance ? MainCamera.instance.GetComponent<Camera>() : Camera.main;
        if (!cam) { return; }
        var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(14, Screen.height / 40) };
        var now = Time.time;
        foreach (var f in _floaters)
        {
            var age = now - f.time;
            var vp = cam.WorldToViewportPoint(transform.position + f.offset + Vector3.up * (1.9f + age * 1.2f));
            if (vp.z < 0) { continue; }
            var sp = new Vector2(vp.x * Screen.width, (1 - vp.y) * Screen.height);
            var c = f.damage >= _biggest * 0.99f && _hitCount > 1 ? new Color(1, 0.8f, 0.2f) : Color.white;
            c.a = 1 - age / 0.9f;
            style.normal.textColor = new Color(0, 0, 0, c.a);
            GUI.Label(new Rect(sp.x - 59, sp.y - 11, 120, 24), f.damage.ToString("0.#"), style);
            style.normal.textColor = c;
            GUI.Label(new Rect(sp.x - 60, sp.y - 12, 120, 24), f.damage.ToString("0.#"), style);
        }
    }
}
