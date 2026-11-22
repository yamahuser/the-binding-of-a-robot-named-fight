using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhaserangBoomerang : MonoBehaviour, IHasTeam
{
    public float damagePerSecond;
    public DamageType damageType;
    private float _initialVelocity = 24;
    private float _acceleration = 24;
    public bool alive;

    private Team _team;
    public Team team
    {
        get { return _team; }
        set { _team = value; }
    }

    private Collider2D _collider2D;
    private Transform _origin;
    private Vector3 _direction;
    private float _velocity;
    private bool _returning;

    public Action onReturn;
    // [MODCONTENT] synergy hooks
    public bool refundOnReturn = true;
    public List<StatusEffect> statusEffects;
    public Action<IDamageable> onHit;
    public float homing;

    private void Awake()
    {
        _collider2D = GetComponentInChildren<Collider2D>();
    }

    private void Update()
    {
        if (alive)
        {
            if (_returning)
            {
                _velocity += Time.deltaTime * _acceleration;
                transform.position = Vector3.MoveTowards(transform.position, _origin.position, _velocity * Time.deltaTime);
                if (Vector3.Distance(transform.position, _origin.position) < 0.125f)
                {
                    if (onReturn != null) onReturn();
                    gameObject.SetActive(false);
                    _collider2D.enabled = false;
                    alive = false;
                }
            }
            else
            {
                // [MODCONTENT] homing mods bend the outbound arc toward the nearest enemy
                if (homing > 0 && EnemyManager.instance)
                {
                    var e = EnemyManager.instance.GetClosest(transform.position, 6f);
                    if (e)
                    {
                        var to = (e.position - transform.position); to.z = 0;
                        _direction = Vector3.RotateTowards(_direction, to.normalized, homing * 0.5f * Time.deltaTime, 0).normalized;
                    }
                }
                transform.position += _direction * _velocity * Time.deltaTime;
                _velocity -= Time.deltaTime * _acceleration;
                if (_velocity < 0)
                {
                    _returning = true;
                    _velocity = 0;
                }
            }
        }
    }

    public void Shoot(Team team, Transform origin, AimingInfo aimingInfo, float velocityFactor)
    {
        if (_collider2D == null) { _collider2D = GetComponentInChildren<Collider2D>(); }
        alive = true;
        transform.parent = null;
        _origin = origin;
        transform.position = aimingInfo.origin;
        _direction = aimingInfo.direction.normalized;
        _team = team;
        _collider2D.enabled = true;
        _returning = false;
        _velocity = _initialVelocity * velocityFactor;

        gameObject.SetActive(true);
        gameObject.layer = LayerMask.NameToLayer("Default");

        switch (team)
        {
            case Team.Player:
            case Team.DeathMatch0:
            case Team.DeathMatch1:
            case Team.DeathMatch2:
            case Team.DeathMatch3:
                gameObject.layer = LayerMask.NameToLayer("ProjectileIgnoreTerrain");
                break;
            case Team.Enemy:
                gameObject.layer = LayerMask.NameToLayer("EnemyProjectileIgnoreTerrain");
                break;
        }

        if (team >= Team.DeathMatch0 && team <= Team.DeathMatch3)
        {
            if (DeathmatchManager.instance)
            {
                DeathmatchManager.instance.SetCollisionIgnore(team, _collider2D);
            }
            else
            {
                Debug.LogError("A Boomerang is set to a deathmatch team and there's no DeathmatchManager");
            }
        }
    }

    public IEnumerator Die(bool offScreen = false)
    {
        alive = false;
        yield return new WaitForEndOfFrame();
        _collider2D.enabled = false;
        gameObject.SetActive(false);
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        var hasTeam = collider.gameObject.GetComponent<IHasTeam>();
        if (hasTeam != null && hasTeam.team == _team) { return; }

        var damageable = collider.gameObject.GetComponent<IDamageable>();

        if (damageable != null && damageable.enabled && damageable.state == DamageableState.Alive)
        {
            var rDamage = damagePerSecond * Time.deltaTime;
            if (_team == Team.Player && PlayerManager.instance) { rDamage *= PlayerManager.instance.coOpMod; }
            if (damageable.Hurt(rDamage, gameObject, damageType, true))
            {
                // [MODCONTENT]
                if (statusEffects != null && statusEffects.Count > 0) { damageable.ApplyStatusEffects(statusEffects, _team); }
                if (onHit != null) { onHit(damageable); }
            }
        }
    }
}
