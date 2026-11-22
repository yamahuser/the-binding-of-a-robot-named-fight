using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RailGun", menuName = "Player Energy Weapons/Rail Gun", order = 4)]
public class RailGun : PlayerEnergyWeapon, IHasOnDestroy
{
    public AudioClip beamSound;
    public bool emitting;
    public float minDamage;
    public float maxDamage;
    public float maxEnergy = 5f;

    private LaserStats _laserStats;
    // [MODCONTENT] one rail per multishot shot
    private readonly List<ILaser> _lasers = new List<ILaser>();

    private IEnumerator _stopAfterTime;

    public Action onDestroy { get; set; }
    public Transform transform { get { return _player.transform; } }

    public override void Initialize(Player player)
    {
        base.Initialize(player);
        _allowTurning = true;
        _laserStats = new LaserStats();
        _laserStats.laserType = LaserType.RailGun;
        _laserStats.damageType = DamageType.Generic;
        _laserStats.ignoreDoors = false;
        _laserStats.layerMask = LayerMask.GetMask();
        _laserStats.range = 36;
        _laserStats.sortingLayerName = "Foreground1";
        _laserStats.sortingOrder = 20;
        _laserStats.stopTime = 0.33f;
        _laserStats.team = _player.team;
        _laserStats.stopType = LaserStopType.Fade;
    }

    public override void OnAttackDown()
    {
        if (!emitting)
        {
            emitting = true;
            _player.attacking = true;

            // [MODCONTENT] synergy load: the same rail power costs more energy with more synergies
            var costMult = WeaponSynergy.CostMultiplier(_player);
            var energyUse = Mathf.Clamp(_player.energy / costMult, minEnergy, maxEnergy);
            var power = energyUse / maxEnergy;

            _player.PlayOneShot(beamSound, power * 1.5f); //so that it won't get cut off

            _player.energy -= Mathf.Min(_player.energy, energyUse * costMult);

            var aimingInfo = _player.GetAimingInfo();
            var origin = aimingInfo.origin + aimingInfo.direction * 0.5f;

            // [MODCONTENT] synergies: size = rail width, shot speed = range, bolt elements/statuses,
            // explosive = blast where each rail lands, multishot = a fan of rails.
            _laserStats.team = _player.team;
            _laserStats.width = power * 1.5f * Mathf.Clamp(WeaponSynergy.Size(_player), 0.75f, 3f);
            _laserStats.damage = Mathf.Lerp(minDamage, maxDamage, power) * _player.damageMultiplier;
            _laserStats.range = 36f * WeaponSynergy.ShotSpeed(_player);
            _laserStats.damageType = WeaponSynergy.MergeDamageType(DamageType.Generic, _player);
            _laserStats.statusEffects = WeaponSynergy.MergeStatusEffects(null, _player);
            _laserStats.onHit = WeaponSynergy.HitHandler(_player, 19);

            var rails = 1 + WeaponSynergy.ExtraShots(_player);
            var arc = WeaponSynergy.Arc(_player, 20f);
            _lasers.Clear();
            for (int i = 0; i < rails; i++)
            {
                var dir = WeaponSynergy.SpreadDirection(aimingInfo.direction, i, rails, arc);
                var rotation = Quaternion.FromToRotation(Vector3.right, dir);
                var laser = LaserManager.instance.AttachAndFireLaser(_laserStats, origin - _player.transform.position, rotation, 0, this);
                laser.gameObject.SetActive(true);
                _lasers.Add(laser);

                if (WeaponSynergy.Explosive(_player))
                {
                    var hit = Physics2D.Raycast(origin, dir, _laserStats.range, LayerMask.GetMask("Default"));
                    var end = hit.collider ? (Vector3)hit.point : origin + dir * _laserStats.range;
                    var b = _player.projectileStats;
                    ProjectileManager.instance.SpawnExplosion(end, WeaponSynergy.BumpExplosion(b.explosion), _player.team,
                        b.explosionDamage * (1f + power), true, WeaponSynergy.MergeDamageType(DamageType.Explosive, _player));
                }
            }

            _player.DamageLatchers(_laserStats.damage, DamageType.Generic);

            _stopAfterTime = StopAfterTime();
            _player.StartCoroutine(_stopAfterTime);
        }
    }

    private IEnumerator StopAfterTime()
    {
        yield return new WaitForSeconds(0.1f);
        StopLasers();
        // [MODCONTENT] rate of fire shortens the recovery between rails
        yield return new WaitForSeconds(0.43f / WeaponSynergy.FireRate(_player));
        emitting = false;
        _player.attacking = false;
        _stopAfterTime = null;
    }

    private void StopLasers()
    {
        foreach (var laser in _lasers) { if (laser != null) { laser.Stop(); } }
        _lasers.Clear();
    }

    public override void OnDeselect()
    {
        if(_stopAfterTime != null)
        {
            _player.StopCoroutine(_stopAfterTime);
        }
        Stop();
    }

    public override void Stop()
    {
        if (_player.loopingAudio)
        {
            _player.loopingAudio.Stop();
        }

        StopLasers();

        emitting = false;
        _player.attacking = false;
    }

    public void OnDestroy()
    {
        if (onDestroy != null) onDestroy();
        Stop();
    }
}
