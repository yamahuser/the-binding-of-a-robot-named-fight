using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScatterGun", menuName = "Player Energy Weapons/ScatterGun", order = 0)]
public class ScatterGun : PlayerEnergyWeapon
{
    public AudioClip shootSound;
    public float baseDamage;
    public float minSpeed;
    public float maxSpeed;
    public float minGravity;
    public float maxGravity;
    public float energyCost;
    public float attackDelay;
    public float fireArc;
    public float arcShots;

    public override void OnAttackDown()
    {
        var cost = WeaponSynergy.EnergyCost(_player, energyCost); // [MODCONTENT] synergy load
        if (_player.energy >= cost)
        {
            _player.energy -= cost;
            if (shootSound)
            {
                _player.PlayOneShot(shootSound);
            }

            var modifiedAttackDelay = attackDelay * (_player.attackDelay / Constants.startingAttackDelay);
            _player.StartCoroutine(_player.DummyAttack(modifiedAttackDelay));

            var aimingInfo = _player.GetAimingInfo();
            var actualArcShots = _player.arcShots > 0 ? arcShots * _player.arcShots : arcShots;
            // [MODCONTENT] Fragment mod = 50% more pellets instead of every pellet splitting.
            if (WeaponSynergy.Fragment(_player)) { actualArcShots *= 1.5f; }
            var actualFireArc = _player.arcShots > 0 ? fireArc * _player.arcShots * 0.75f : fireArc;

            for (int i = 0; i < actualArcShots; i++)
            {
                var stats = new ProjectileStats();
                stats.team = _player.team;
                stats.type = ProjectileType.Bullet;
                stats.speed = Random.Range(minSpeed, maxSpeed) + _player.shotSpeedModifier;
                stats.damage = baseDamage * _player.damageMultiplier;
                stats.gravity = Random.Range(minGravity, maxGravity);
                stats.lockRotation = true;
                stats.ignoreAegis = true;
                stats.canOpenDoors = true;
                stats.homing = _player.projectileStats.homing;
                stats.homingRadius = _player.projectileStats.homingRadius;
                stats.size = Mathf.Clamp(_player.projectileStats.size, 1, 2.5f); // [MODCONTENT] scales smoothly
                stats.ignoreAegis = true;
                // [MODCONTENT] Pellets take every bolt mod except fragment; explosive = every 4th pellet explodes.
                WeaponSynergy.ApplyShotMods(stats, _player, WeaponSynergy.Mods.All & ~WeaponSynergy.Mods.Fragment & ~WeaponSynergy.Mods.Explosive);
                if (WeaponSynergy.Explosive(_player) && i % 4 == 0)
                {
                    stats.explosion = _player.projectileStats.explosion;
                    stats.explosionDamage = _player.projectileStats.explosionDamage;
                }

                float angleMod = (((float)i / (actualArcShots - 1f)) * 2f) - 1f;
                Vector3 shotDirection = (Quaternion.AngleAxis(angleMod * actualFireArc / 2, Vector3.forward) * aimingInfo.direction).normalized;
                ProjectileManager.instance.Shoot(stats, aimingInfo.origin, shotDirection);                
            }
        }
    }
}
