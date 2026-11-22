using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// [MODCONTENT] Swarmlord's minion synergy. Attached to each of his followers (orbs); every
// half second it re-derives the orb's numbers from his current stats and bolt mods:
//   damage     -> shooter / contact / laser / toxin damage
//   fire rate  -> shooter cadence, turret timer, Lasorb charge, contact DPS
//   shot speed -> orb bullet speed
//   shot size  -> orb scale, bullet size, laser width
//   move speed -> orbit speed
//   max health -> Health Orb drop rate, Health Vorb appetite
//   max energy -> Energy / Scrap Vorb appetite
//   bolt mods  -> statuses, elements, pierce, bounce, explosive... on orb bullets and lasers
// Leaving Swarmlord restores every base value and removes the link.
public class MinionLink : MonoBehaviour
{
    private Follower _follower;
    private Player _player;
    private bool _captured;
    private float _next;

    // base values
    private Vector3 _baseScale;
    private readonly Dictionary<Shooter, ProjectileStats> _shooterStats = new Dictionary<Shooter, ProjectileStats>();
    private readonly Dictionary<ShootWithPlayer, float> _shootDelay = new Dictionary<ShootWithPlayer, float>();
    private readonly Dictionary<TimedShooter, float> _timedDelay = new Dictionary<TimedShooter, float>();
    private readonly Dictionary<DamageCreatureTrigger, float> _triggerDamage = new Dictionary<DamageCreatureTrigger, float>();
    private readonly Dictionary<DamageCreatureTrigger, DamageType> _triggerType = new Dictionary<DamageCreatureTrigger, DamageType>();
    private readonly Dictionary<DamageCreatureTrigger, List<StatusEffect>> _triggerEffects = new Dictionary<DamageCreatureTrigger, List<StatusEffect>>();
    private float _healthOrbDelay, _lasorbDamage, _lasorbCharge, _lasorbWidth, _orbitTime;
    private int _vorbGibs;

    private static readonly FieldInfo OrbitTimeField = typeof(OrbitalFollower).GetField("_rotationTime", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo LasorbChargeField = typeof(Lasorb).GetField("_chargeTime", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void Ensure(Follower f)
    {
        if (f && !f.GetComponent<MinionLink>()) { f.gameObject.AddComponent<MinionLink>(); }
    }

    private void Start()
    {
        _follower = GetComponent<Follower>();
    }

    private static bool Active { get { return CharacterRoster.Current.minionSynergy; } }

    private void Update()
    {
        if (!_follower) { _follower = GetComponent<Follower>(); }
        if (!_follower || !_follower.player) { return; }
        _player = _follower.player;

        if (!Active)
        {
            if (_captured) { Restore(); }
            Destroy(this);
            return;
        }

        if (Time.time < _next) { return; }
        _next = Time.time + 0.5f;
        if (!_captured) { Capture(); }
        Apply();
    }

    private void Capture()
    {
        _captured = true;
        _baseScale = transform.localScale;

        foreach (var s in GetComponentsInChildren<Shooter>(true))
        {
            _shooterStats[s] = new ProjectileStats(s.projectileStats);
            var swp = s as ShootWithPlayer;
            if (swp) { _shootDelay[swp] = swp.attackDelay; }
            var ts = s as TimedShooter;
            if (ts) { _timedDelay[ts] = ts.shootTime; }
        }
        foreach (var t in GetComponentsInChildren<DamageCreatureTrigger>(true))
        {
            _triggerDamage[t] = t.damage;
            _triggerType[t] = t.damageType;
            _triggerEffects[t] = t.statusEffects != null ? new List<StatusEffect>(t.statusEffects) : null;
        }

        var ho = GetComponent<HealthOrb>(); if (ho) { _healthOrbDelay = ho.delay; }
        var lo = GetComponent<Lasorb>();
        if (lo)
        {
            _lasorbDamage = lo.baseDamage;
            _lasorbWidth = lo.laserStats != null ? lo.laserStats.width : 1f;
            if (LasorbChargeField != null) { _lasorbCharge = (float)LasorbChargeField.GetValue(lo); }
        }
        var vo = GetComponent<Vorb>(); if (vo) { _vorbGibs = vo.gibsNeeded; }
        var of = GetComponent<OrbitalFollower>();
        if (of && OrbitTimeField != null) { _orbitTime = (float)OrbitTimeField.GetValue(of); }
    }

    private void Apply()
    {
        var p = _player;
        var dmg = WeaponSynergy.Damage(p);
        var rate = WeaponSynergy.FireRate(p);
        var size = WeaponSynergy.Size(p);
        var shotSpeed = WeaponSynergy.ShotSpeed(p);
        var moveScale = Mathf.Clamp(p.maxSpeed / Constants.startingMaxSpeed, 0.5f, 2.5f);
        var healthScale = Mathf.Clamp(p.maxHealth / Constants.startingHealth, 0.5f, 4f);
        var energyScale = Mathf.Clamp(p.maxEnergy / Constants.startingEnergy, 0.5f, 4f);

        transform.localScale = _baseScale * Mathf.Clamp(size, 1f, 2f);

        foreach (var kv in _shooterStats)
        {
            if (!kv.Key) continue;
            var s = new ProjectileStats(kv.Value);
            s.team = p.team;
            s.damage = kv.Value.damage * dmg;
            s.speed = kv.Value.speed * shotSpeed;
            s.size = kv.Value.size * Mathf.Clamp(size, 1f, 2.5f);
            WeaponSynergy.ApplyShotMods(s, p, WeaponSynergy.Mods.All & ~WeaponSynergy.Mods.Fragment);
            kv.Key.projectileStats = s;
        }
        foreach (var kv in _shootDelay) { if (kv.Key) kv.Key.attackDelay = kv.Value / rate; }
        foreach (var kv in _timedDelay) { if (kv.Key) kv.Key.shootTime = kv.Value / rate; }

        foreach (var kv in _triggerDamage)
        {
            var t = kv.Key;
            if (!t) continue;
            t.damage = kv.Value * dmg * rate;
            WeaponSynergy.ConfigureTrigger(t, p, _triggerType[t], _triggerEffects[t], 900 + GetInstanceID() % 64, rate);
        }

        var ho = GetComponent<HealthOrb>(); if (ho) { ho.delay = _healthOrbDelay / healthScale; }
        var lo = GetComponent<Lasorb>();
        if (lo)
        {
            lo.baseDamage = _lasorbDamage * rate;   // laserStats.damage already multiplies by the damage stat
            if (LasorbChargeField != null) { LasorbChargeField.SetValue(lo, _lasorbCharge / rate); }
            if (lo.laserStats != null)
            {
                lo.laserStats.width = _lasorbWidth * Mathf.Clamp(size, 1f, 2.5f);
                lo.laserStats.damageType = WeaponSynergy.MergeDamageType(DamageType.Generic, p);
                lo.laserStats.statusEffects = WeaponSynergy.MergeStatusEffects(null, p);
                lo.laserStats.onHit = WeaponSynergy.HitHandler(p, 121, rate);
            }
        }
        var vo = GetComponent<Vorb>();
        if (vo)
        {
            var appetite = vo.dropType.ToString().Contains("Health") ? healthScale : energyScale;
            vo.gibsNeeded = Mathf.Max(5, Mathf.RoundToInt(_vorbGibs / appetite));
        }
        var of = GetComponent<OrbitalFollower>();
        if (of && OrbitTimeField != null && _orbitTime > 0) { OrbitTimeField.SetValue(of, _orbitTime / moveScale); }
    }

    private void Restore()
    {
        transform.localScale = _baseScale;
        foreach (var kv in _shooterStats) { if (kv.Key) kv.Key.projectileStats = new ProjectileStats(kv.Value); }
        foreach (var kv in _shootDelay) { if (kv.Key) kv.Key.attackDelay = kv.Value; }
        foreach (var kv in _timedDelay) { if (kv.Key) kv.Key.shootTime = kv.Value; }
        foreach (var kv in _triggerDamage)
        {
            var t = kv.Key;
            if (!t) continue;
            t.damage = kv.Value;
            t.damageType = _triggerType[t];
            t.statusEffects = _triggerEffects[t];
            t.onDamageParam = null;
        }
        var ho = GetComponent<HealthOrb>(); if (ho) { ho.delay = _healthOrbDelay; }
        var lo = GetComponent<Lasorb>();
        if (lo)
        {
            lo.baseDamage = _lasorbDamage;
            if (LasorbChargeField != null) { LasorbChargeField.SetValue(lo, _lasorbCharge); }
            if (lo.laserStats != null) { lo.laserStats.width = _lasorbWidth; lo.laserStats.onHit = null; lo.laserStats.statusEffects = new List<StatusEffect>(); lo.laserStats.damageType = DamageType.Generic; }
        }
        var vo = GetComponent<Vorb>(); if (vo) { vo.gibsNeeded = _vorbGibs; }
        var of = GetComponent<OrbitalFollower>();
        if (of && OrbitTimeField != null && _orbitTime > 0) { OrbitTimeField.SetValue(of, _orbitTime); }
        _captured = false;
    }
}
