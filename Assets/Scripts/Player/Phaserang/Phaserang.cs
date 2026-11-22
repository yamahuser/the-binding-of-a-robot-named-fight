using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "Phaserang", menuName = "Player Energy Weapons/Phaserang", order = 4)]
public class Phaserang : PlayerEnergyWeapon
{
    public PhaserangBoomerang boomerangPrefab;    
    public AudioClip shootSound;
    public AudioClip returnSound;
    public float baseDamagePerSecond = 30;
    private List<PhaserangBoomerang> _boomerangs = new List<PhaserangBoomerang>();
    private float _velocityFactor = 0;

    public override void Initialize(Player player)
    {
        base.Initialize(player);
        NewBoomerang();
    }

    private PhaserangBoomerang NewBoomerang()
    {
        var boomerang = Instantiate(boomerangPrefab);
        boomerang.transform.parent = _player.transform.parent;
        boomerang.gameObject.SetActive(false);
        boomerang.onReturn += () =>
        {
            if (_player && boomerang.refundOnReturn) // [MODCONTENT] only the main boomerang refunds
            {
                _player.PlayOneShot(returnSound);
                _player.GainEnergy(minEnergy);
            }
        };
        _boomerangs.Add(boomerang);
        return boomerang;
    }

    private IEnumerator Attack()
    {
        _player.attacking = true;
        yield return new WaitForSeconds(0.1f);
        _player.attacking = false;
    }

    public override void OnAttackDown()
    {
        if (_player.energy > minEnergy)
        {
            if (_player.state != DamageableState.Alive) return;
            if (_velocityFactor < 1) { _velocityFactor += Time.deltaTime * 0.75f; }
            if (!_player.flashing)
            {
                _player.StartFlash(1, 0.2f, Constants.blasterGreen, Mathf.Lerp(0, 1, Mathf.Clamp01(_velocityFactor - 0.5f)), false);
            }
        }
        else
        {
            if (!_player.flashing)
            {
                _player.StartFlash(1, 0.2f, Constants.damageFlashColor, 0.25f, false);
            }
        }
    }

    public override void OnAttackUp()
    {
        if (_player.energy > minEnergy)
        {
            if (_player.spinJumping) _player.spinJumping = false;
            _velocityFactor = Mathf.Clamp(_velocityFactor, 0.75f, 1);

            // [MODCONTENT] multishot = several boomerangs in a fan; size = boomerang size;
            // shot speed = throw speed; rate of fire = DPS; bolt elements/statuses/procs; homing bends the throw.
            var aim = _player.GetAimingInfo();
            var count = 1 + WeaponSynergy.ExtraShots(_player);
            var arc = WeaponSynergy.Arc(_player, 30f);
            var rate = WeaponSynergy.FireRate(_player);
            for (int i = 0; i < count; i++)
            {
                var boomerang = GetBoomerang();
                boomerang.refundOnReturn = i == 0;
                boomerang.damagePerSecond = baseDamagePerSecond * _player.damageMultiplier * rate;
                boomerang.damageType = WeaponSynergy.MergeDamageType(boomerangPrefab.damageType, _player);
                boomerang.statusEffects = WeaponSynergy.MergeStatusEffects(null, _player);
                boomerang.onHit = WeaponSynergy.HitHandler(_player, 23 + i, rate);
                boomerang.homing = _player.projectileStats.homing;
                boomerang.transform.localScale = Vector3.one * Mathf.Clamp(WeaponSynergy.Size(_player), 1f, 2.5f);
                var shotAim = aim;
                shotAim.direction = WeaponSynergy.SpreadDirection(aim.direction, i, count, arc);
                boomerang.Shoot(_player.team, _player.transform, shotAim, _velocityFactor * Mathf.Sqrt(WeaponSynergy.ShotSpeed(_player)));
            }
            _player.energy -= WeaponSynergy.EnergyCost(_player, minEnergy);
            _player.StartCoroutine(Attack());
            _player.PlayOneShot(shootSound);
            _velocityFactor = 0.55f;
        }
    }

    public PhaserangBoomerang GetBoomerang()
    {
        foreach (var b in _boomerangs)
        {
            if (!b.alive) { return b; }
        }

        return NewBoomerang();
    }

    public override void OnDeselect()
    {
        base.OnDeselect();
        _velocityFactor = 0.55f;
    }

    public override bool Usable()
    {
        return _player && (_player.energy > minEnergy || _boomerangs.Any(b => b.alive));
    }
}