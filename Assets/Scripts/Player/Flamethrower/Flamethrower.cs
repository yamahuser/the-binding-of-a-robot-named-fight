using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[CreateAssetMenu(fileName = "Flamethower", menuName = "Player Energy Weapons/Flamethrower", order = 1)]
public class Flamethrower : PlayerEnergyWeapon, IFlameOrigin
{
    public FlamethrowerFlame flamePrefab;
    public AudioClip flameStart;
    public AudioClip flameLoop;
    public bool emitting;
    public bool hitWalls;
    public float flameRange = 6f;
    public float energyPerSecond = 2;
    public float baseDamage = 15f;

    [NonSerialized]
    private Transform _origin;
    public Transform origin { get { return _origin; } }

    [NonSerialized]
    private Vector3 _target;
    public Vector3 target { get { return _target; } }

    [NonSerialized]
    private List<FlamethrowerFlame> _flames;

    private float _flameTimer;
    private float _energyTimer;
    private float _energyTime;
    private float _flameInterval;
    private float _flameSpeed = 2f;
    private const float _flameMovingLifespan = 11 / 24f;
    private const float _flameTotalLifespan = 19 / 24f;
    private int _lastSortingOrder;

    // [MODCONTENT] synergy state
    [NonSerialized] private List<FlameStream> _extraStreams;
    [NonSerialized] private bool _cachedBase;
    [NonSerialized] private DamageType _baseDamageType;
    [NonSerialized] private List<StatusEffect> _baseStatusEffects;

    /// <summary>[MODCONTENT] Extra flame stream for multishot: its own origin and target, flames owned by it.</summary>
    private class FlameStream : IFlameOrigin
    {
        public Flamethrower owner;
        public Transform originTransform;
        public Vector3 targetPoint;
        public readonly List<FlamethrowerFlame> flames = new List<FlamethrowerFlame>();
        public Transform origin { get { return originTransform; } }
        public Vector3 target { get { return targetPoint; } }
        public FlamethrowerFlame GetFlame() { return owner.GetFlameFor(this, flames, originTransform); }
    }

    public override void Initialize(Player player)
    {
        base.Initialize(player);
        _flames = new List<FlamethrowerFlame>();
        _allowTurning = true;

        var flames = new GameObject();
        _origin = flames.transform;
        _origin.parent = _player.transform;
        _origin.localPosition = Vector3.zero;
        _origin.name = "Flames";
    }

    public override void Update()
    {
        if (emitting)
        {
            if(_player.energy <= 0 || (!_player.controller.GetButton(_player.attackString)))
            {
                Stop();
            }

            var mod = Mathf.Clamp((_player.baseProjectileSpeed - Constants.startingProjectileSpeed), 0, 10);
            var range = Mathf.Clamp(flameRange + _player.shotSpeedUps * 0.75f + mod, flameRange-2,20);

            _flameSpeed = range / _flameMovingLifespan;
            _flameInterval = _flameMovingLifespan / range;
            _energyTime = 1 / (energyPerSecond * WeaponSynergy.CostMultiplier(_player)); // [MODCONTENT] synergy load

            var aimingInfo = _player.GetAimingInfo();
            _origin.position = (aimingInfo.nearWall && hitWalls) ? aimingInfo.origin : aimingInfo.origin + aimingInfo.direction * 0.5f;
            _target = _origin.position + aimingInfo.direction * range;

            // [MODCONTENT] multishot = extra streams fanned out from the nozzle
            var streams = 1 + WeaponSynergy.ExtraShots(_player);
            EnsureStreams(streams - 1);
            var arc = WeaponSynergy.Arc(_player, 35f);
            for (int s = 0; s < _extraStreams.Count; s++)
            {
                var stream = _extraStreams[s];
                stream.originTransform.position = _origin.position;
                // stream 0 is the main one (straight); extras take the other spread slots
                var slot = s + 1;
                var dir = WeaponSynergy.SpreadDirection(aimingInfo.direction, Remap(slot, streams), streams, arc);
                stream.targetPoint = _origin.position + dir * range;
            }

            _flameTimer -= Time.deltaTime;
            if (_flameTimer <= 0)
            {
                SpawnFlame(aimingInfo.direction);
                for (int s = 0; s < streams - 1 && s < _extraStreams.Count; s++)
                {
                    var flame = _extraStreams[s].GetFlame();
                    flame.Spawn(_lastSortingOrder, _flameSpeed, _flameMovingLifespan, _flameTotalLifespan, _player.team, true);
                }
                _flameTimer = _flameInterval;
            }

            _energyTimer += Time.deltaTime;
            if(_energyTimer > _energyTime)
            {
                _energyTimer = 0;
                _player.energy -= 1;
            }
        }
        else
        {
            _flameTimer = 0f;
            _lastSortingOrder = 0;
        }
    }

    public void SpawnFlame(Vector3 direction)
    {
        _lastSortingOrder++;
        
        var damage = baseDamage * _player.damageMultiplier;
        var flame = GetFlame();
        ConfigureFlame(flame);

        _player.DamageLatchers(damage, DamageType.Fire);
        flame.Spawn(_lastSortingOrder, _flameSpeed, _flameMovingLifespan, _flameTotalLifespan, _player.team, true);
    }

    public FlamethrowerFlame GetFlame()
    {
        var flame = _flames.FirstOrDefault(f => f.gameObject.activeInHierarchy == false);

        if (!flame)
        {
            flame = Instantiate(flamePrefab);
            flame.owner = this;
            flame.player = _player;
            flame.team = _player.team;
            flame.transform.parent = _origin;
            flame.hitWalls = hitWalls;
            flame.GetComponent<DamageCreatureTrigger>().damage = baseDamage * _player.damageMultiplier;
            _flames.Add(flame);
        }

        return flame;
    }

    /// <summary>[MODCONTENT] Map stream index (0 = main) to a spread slot so the main stream stays centred.</summary>
    private static int Remap(int stream, int count)
    {
        var centre = (count - 1) / 2;
        if (stream == 0) { return centre; }
        return stream <= centre ? stream - 1 : stream;
    }

    private void EnsureStreams(int count)
    {
        if (_extraStreams == null) { _extraStreams = new List<FlameStream>(); }
        while (_extraStreams.Count < count)
        {
            var go = new GameObject("FlameStream" + _extraStreams.Count);
            go.transform.parent = _player.transform;
            go.transform.localPosition = Vector3.zero;
            _extraStreams.Add(new FlameStream { owner = this, originTransform = go.transform });
        }
    }

    private FlamethrowerFlame GetFlameFor(IFlameOrigin owner, List<FlamethrowerFlame> pool, Transform parent)
    {
        var flame = pool.FirstOrDefault(f => f && f.gameObject.activeInHierarchy == false);
        if (!flame)
        {
            flame = Instantiate(flamePrefab);
            flame.owner = owner;
            flame.player = _player;
            flame.team = _player.team;
            flame.transform.parent = parent;
            flame.hitWalls = hitWalls;
            pool.Add(flame);
        }
        ConfigureFlame(flame);
        return flame;
    }

    /// <summary>[MODCONTENT] Per-flame synergy: damage x fire rate, size = flame scale,
    /// bolt elements + statuses on top of the weapon's own (Toxin Spray keeps its poison), procs on hit.</summary>
    private void ConfigureFlame(FlamethrowerFlame flame)
    {
        if (!_cachedBase)
        {
            var prefabTrigger = flamePrefab.GetComponent<DamageCreatureTrigger>();
            _baseDamageType = prefabTrigger ? prefabTrigger.damageType : DamageType.Fire;
            _baseStatusEffects = prefabTrigger && prefabTrigger.statusEffects != null ? new List<StatusEffect>(prefabTrigger.statusEffects) : new List<StatusEffect>();
            _cachedBase = true;
        }

        var trigger = flame.GetComponent<DamageCreatureTrigger>();
        var rate = WeaponSynergy.FireRate(_player);
        trigger.damage = baseDamage * _player.damageMultiplier * rate;
        WeaponSynergy.ConfigureTrigger(trigger, _player, _baseDamageType, _baseStatusEffects, item == MajorItem.ToxinSpray ? 211 : 6, rate);
        flame.transform.localScale = Vector3.one * Mathf.Clamp(WeaponSynergy.Size(_player), 1f, 2.5f);
    }

    public override void OnAttackDown()
    {
        if (_player.state != DamageableState.Alive) return;

        if (_player.energy > 0)
        {
            emitting = true;
            _player.PlayOneShot(flameStart); //so that it won't get cut off
            _player.loopingAudio.clip = flameLoop;
            _player.loopingAudio.PlayScheduled(flameStart.length - 0.5f);
            _player.attacking = true;
        }
    }

    public override void OnAttackUp()
    {
        Stop();
    }

    public override void OnDeselect()
    {
        Stop();
    }

    public override void Stop()
    {
        if (_player && _player.loopingAudio)
        {
            _player.loopingAudio.Stop();
        }
        emitting = false;
        _player.attacking = false;
    }

    public void OnDestroy()
    {
        Stop();
        foreach (var flame in _flames)
        {
            if (flame) Destroy(flame.gameObject, _flameTotalLifespan);
        }
        if (_extraStreams != null)
        {
            foreach (var s in _extraStreams)
            {
                foreach (var flame in s.flames) { if (flame) Destroy(flame.gameObject, _flameTotalLifespan); }
                if (s.originTransform) Destroy(s.originTransform.gameObject, _flameTotalLifespan);
            }
        }
    }
}
