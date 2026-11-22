using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LightningGun", menuName = "Player Energy Weapons/Lightning Gun", order = 3)]
public class LightningGun : PlayerEnergyWeapon
{
    public LightningGunBolt lightningPrefab;
    public AudioClip lightningStart;
    public AudioClip lightningLoop;
    public bool emitting;
    public float baseDamagePerSecond = 1.5f;
    public float energyPerSecond = 2;
    public float range = 8f;
    public float fireArc = 90f;

    private LightningGunBolt _lightning;
    private float _energyTimer;
    private float _energyTime;
    private Vector3 _lastTarget;
    private Quaternion _randomAngle;
    // [MODCONTENT] multishot forks
    private readonly List<LightningGunBolt> _forks = new List<LightningGunBolt>();
    private readonly List<Vector3> _forkLastTargets = new List<Vector3>();
    private float _baseMagnitude = -1;

    public override void Initialize(Player player)
    {
        base.Initialize(player);
        _lightning = Instantiate(lightningPrefab);
        _lightning.gameObject.SetActive(false);
        _lightning.transform.parent = _player.transform;
        _lightning.transform.localPosition = Vector3.zero;
        _lightning.team = Team.Player;

        _allowTurning = true;

        _randomAngle = Quaternion.AngleAxis(Random.Range(-30F, 30F), Vector3.forward);
    }

    public override void Update()
    {
        if (emitting)
        {
            if (!_player.controller.GetButton(_player.attackString))
            {
                Stop();
                return;
            }

            if (!_lightning.gameObject.activeInHierarchy)
            {
                _lightning.gameObject.SetActive(true);
            }

            if (_player.energy <= 0)
            {
                Stop();
            }

            _energyTime = 1 / (energyPerSecond * WeaponSynergy.CostMultiplier(_player)); // [MODCONTENT] synergy load

            var aimingInfo = _player.GetAimingInfo();
            var origin = aimingInfo.origin + aimingInfo.direction * 0.5f;

            var mod = Mathf.Clamp((_player.baseProjectileSpeed - Constants.startingProjectileSpeed), 0, 10);
            var modRange = Mathf.Clamp(range + _player.shotSpeedUps * 0.75f + mod, range-2, 20);

            var closestEnemy = EnemyManager.instance.GetClosestInArc(aimingInfo.origin, aimingInfo.direction, modRange, fireArc);

            _randomAngle = Quaternion.AngleAxis(Random.Range(-30F, 30F), Vector3.forward);
            Vector3 randomDirection = ( _randomAngle * aimingInfo.direction).normalized;

            var target = closestEnemy ? closestEnemy.position : origin + randomDirection * modRange;
            var lerpValue = closestEnemy ? 0.5f : 0.15f;

            var trueTarget = Vector3.Lerp(_lastTarget, target, lerpValue);
            // [MODCONTENT] rate of fire = DPS, size = arc thickness, bolt elements/statuses + procs
            var rate = WeaponSynergy.FireRate(_player);
            _lightning.damagePerSecond = baseDamagePerSecond * _player.damageMultiplier * rate;
            ConfigureBolt(_lightning, 5, rate);
            _lightning.DrawLightning(origin, trueTarget, closestEnemy);
            UpdateForks(origin, aimingInfo.direction, modRange, closestEnemy, rate);
            _player.DamageLatchers(_lightning.damagePerSecond * Time.deltaTime, DamageType.Generic);

            _energyTimer += Time.deltaTime;
            if (_energyTimer > _energyTime)
            {
                _energyTimer = 0;
                _player.energy -= 1;
            }

            _lastTarget = trueTarget;
        }
        else
        {
            _lastTarget = _player.transform.position;
        }
    }

    private void ConfigureBolt(LightningGunBolt bolt, int key, float rate)
    {
        if (_baseMagnitude < 0) { _baseMagnitude = bolt.maxMagnitude; }
        bolt.maxMagnitude = _baseMagnitude * Mathf.Clamp(WeaponSynergy.Size(_player), 1f, 3f);
        var trigger = bolt.GetComponentInChildren<DamageCreatureTrigger>();
        WeaponSynergy.ConfigureTrigger(trigger, _player, DamageType.Electric, null, key, rate);
    }

    /// <summary>[MODCONTENT] Multishot: each extra shot forks a bolt to the next-nearest enemy in range
    /// (or crackles at random if nothing else is there).</summary>
    private void UpdateForks(Vector3 origin, Vector3 direction, float modRange, Enemy primary, float rate)
    {
        var forks = WeaponSynergy.ExtraShots(_player);
        while (_forks.Count < forks)
        {
            var f = Instantiate(lightningPrefab);
            f.transform.parent = _player.transform;
            f.transform.localPosition = Vector3.zero;
            f.team = Team.Player;
            _forks.Add(f);
            _forkLastTargets.Add(origin);
        }

        var used = new HashSet<Enemy>();
        if (primary) { used.Add(primary); }
        for (int i = 0; i < _forks.Count; i++)
        {
            var f = _forks[i];
            if (i >= forks) { if (f.gameObject.activeSelf) f.gameObject.SetActive(false); continue; }
            if (!f.gameObject.activeSelf) { f.gameObject.SetActive(true); }

            Enemy best = null;
            float bestDist = modRange * modRange;
            foreach (var e in EnemyManager.instance.enemies)
            {
                if (!e || used.Contains(e) || e.state != DamageableState.Alive) continue;
                var d = (e.position - origin).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = e; }
            }
            if (best) { used.Add(best); }

            var spread = WeaponSynergy.SpreadDirection(direction, i + 1, forks + 2, WeaponSynergy.Arc(_player, 60f));
            var target = best ? best.position : origin + (Quaternion.AngleAxis(Random.Range(-20f, 20f), Vector3.forward) * spread) * modRange * 0.8f;
            var trueTarget = Vector3.Lerp(_forkLastTargets[i], target, best ? 0.5f : 0.15f);
            _forkLastTargets[i] = trueTarget;

            f.damagePerSecond = baseDamagePerSecond * _player.damageMultiplier * rate;
            ConfigureBolt(f, 50 + i, rate);
            f.DrawLightning(origin, trueTarget, best);
        }
    }

    private void HideForks()
    {
        for (int i = 0; i < _forks.Count; i++)
        {
            if (_forks[i]) _forks[i].gameObject.SetActive(false);
            _forkLastTargets[i] = _player ? _player.transform.position : Vector3.zero;
        }
    }

    public override void OnAttackDown()
    {
        if (_player.state != DamageableState.Alive) return;

        if (_player.energy > 0)
        {
            emitting = true;
            _player.PlayOneShot(lightningStart); //so that it won't get cut off
            _player.loopingAudio.clip = lightningLoop;
            _player.loopingAudio.PlayScheduled(lightningStart.length - 0.5f);
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
        _player.loopingAudio.Stop();
        _lightning.gameObject.SetActive(false);
        HideForks(); // [MODCONTENT]
        emitting = false;
        _player.attacking = false;
    }
}
