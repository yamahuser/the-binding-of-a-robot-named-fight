using System;
using System.Collections.Generic;
using UnityEngine;

// [MODCONTENT] Shared synergy layer for energy weapons.
//
// Every energy weapon scales with every player stat:
//   Damage      -> damage (all weapons)
//   Fire rate   -> refire speed for shot weapons, damage-per-second for held weapons
//   Shot speed  -> projectile speed / beam range / throw speed
//   Shot size   -> projectile size / beam width / flame & arc size
//   Multishot   -> each weapon has its own interpretation (see each weapon)
// and picks up the bolt's mods (status effects, damage types, child effects, pierce,
// bounce, fragment, explosive, phasing, hit/kill hooks). Weapons that don't fire
// projectiles turn Explosive/Fragment into on-hit "procs" via HitHandler.
public static class WeaponSynergy
{
    // ---------------- stat scaling (1 = starting stats) ----------------

    public static float Damage(Player p) { return p.damageMultiplier; }

    /// <summary>Attack-speed multiplier: 1 at start, &gt;1 with rate-of-fire upgrades.</summary>
    public static float FireRate(Player p)
    {
        var rate = Constants.startingAttackDelay / Mathf.Max(0.02f, p.attackDelay);
        // Big Bolt's slow heavy bolt should not slow energy weapons; they keep its size instead.
        if (HasBigBolt(p)) { rate *= BigBoltAttackMultiplier(); }
        return Mathf.Clamp(rate, 0.25f, 5f);
    }

    public static bool HasBigBolt(Player p) { return p.itemsPossessed != null && p.itemsPossessed.Contains(MajorItem.BigBolt); }
    public static bool HasBoltHelm(Player p) { return p.itemsPossessed != null && p.itemsPossessed.Contains(MajorItem.BoltHelm); }

    private static float _bigBoltMult = -1;
    private static float BigBoltAttackMultiplier()
    {
        if (_bigBoltMult < 0)
        {
            MajorItemInfo info;
            _bigBoltMult = ItemManager.items != null && ItemManager.items.TryGetValue(MajorItem.BigBolt, out info) && info.baseAttackMultiplier > 0
                ? info.baseAttackMultiplier : 1f;
        }
        return _bigBoltMult;
    }

    private static float _nextRefresh;

    /// <summary>Health Booster / Scrap Booster bonuses depend on current health / scrap. Held weapons
    /// never triggered the stat recalculation, so refresh it a few times a second while a weapon is out.</summary>
    public static void RefreshLiveStats(Player p)
    {
        if (!p || (!p.healthBooster && !p.scrapBooster)) { return; }
        if (Time.time < _nextRefresh) { return; }
        _nextRefresh = Time.time + 0.2f;
        p.CalculateAttackDelay(false);
        p.CalculateShotSpeed(false);
        p.CalculateShotSize(false);
        p.CalculateDamage(false);
    }

    /// <summary>Bolt Helm with an energy weapon equipped: the helm bolt picks up that weapon's character.</summary>
    public static ProjectileStats HelmEcho(ProjectileStats bolt, Player p, MajorItem weapon)
    {
        var s = new ProjectileStats(bolt);
        s.onHurt = bolt.onHurt;
        s.statusEffects = bolt.statusEffects != null ? new List<StatusEffect>(bolt.statusEffects) : new List<StatusEffect>();
        switch (weapon)
        {
            case MajorItem.Flamethrower:
            case MajorItem.HellfireCannon:
                s.damageType |= DamageType.Fire; AddStatus(s, "NormalBurn"); s.type = ProjectileType.FireBolt; break;
            case MajorItem.ToxinSpray:
            case MajorItem.NecroluminantSpray:
                s.damageType |= DamageType.Hazard; AddStatus(s, "NormalPoison"); s.type = ProjectileType.ToxinBolt; break;
            case MajorItem.LightningGun:
            case MajorItem.PulseMGL:
                s.damageType |= DamageType.Electric; s.type = ProjectileType.ElectroBolt; break;
            case MajorItem.RocketLauncher:
                s.damageType |= DamageType.Explosive; s.type = ProjectileType.ExplosiveBolt;
                if (s.explosion == Explosion.None) { s.explosion = Explosion.E16x16; s.explosionDamage = Mathf.Max(s.explosionDamage, 1.5f * p.damageMultiplier); }
                break;
            case MajorItem.BuzzsawGun:
                s.damageType |= DamageType.Mechanical; s.penetrative = true; s.bounce = true; break;
            case MajorItem.RailGun:
            case MajorItem.LaserBeam:
                s.penetrative = true; s.speed *= 1.5f; s.type = ProjectileType.PenetrativeBolt; break;
            case MajorItem.Phaserang:
                s.ignoreTerrain = true; s.type = ProjectileType.PhaseBolt; break;
            case MajorItem.ScatterGun:
                s.fragment = true;
                s.fragmentStats = new ProjectileFragmentStats { sizeMod = 0.5f, damageMod = 0.5f, amount = 3, arc = 60, lifeSpan = 1.5f };
                break;
            case MajorItem.MachineGun:
                s.speed *= 1.25f; s.homing = Mathf.Max(s.homing, 2f); s.homingRadius = Mathf.Max(s.homingRadius, 5f); break;
            case MajorItem.ArtificeBeam:
                s.damage *= 1.5f; break;
        }
        return s;
    }

    private static void AddStatus(ProjectileStats s, string name)
    {
        var e = ResourcePrefabManager.instance ? ResourcePrefabManager.instance.LoadStatusEffect("StatusEffects/" + name) : null;
        if (e && !s.statusEffects.Exists(x => x && x.type == e.type)) { s.statusEffects.Add(e); }
    }

    /// <summary>Shot-speed multiplier: 1 at start.</summary>
    public static float ShotSpeed(Player p)
    {
        var speed = p.projectileStats.speed > 0 ? p.projectileStats.speed : Constants.startingProjectileSpeed;
        return Mathf.Clamp(speed / Constants.startingProjectileSpeed, 0.5f, 3f);
    }

    /// <summary>Shot-size multiplier: 1 at start.</summary>
    public static float Size(Player p) { return Mathf.Clamp(p.projectileStats.size, 0.5f, 4f); }

    /// <summary>Extra shots granted by multishot items (Triple Shot = 2 extra).</summary>
    public static int ExtraShots(Player p) { return p.arcShots > 1 ? p.arcShots - 1 : 0; }

    public static float Arc(Player p, float fallback) { return p.fireArc > 0 ? p.fireArc : fallback; }

    public static bool Explosive(Player p) { return p.projectileStats.explosion != Explosion.None; }
    public static bool Fragment(Player p) { return p.projectileStats.fragment; }

    /// <summary>Spread directions evenly across arc degrees (count 1 = straight ahead).</summary>
    public static Vector3 SpreadDirection(Vector3 forward, int index, int count, float arc)
    {
        if (count <= 1) { return forward.normalized; }
        float t = (index / (count - 1f)) * 2f - 1f;
        return (Quaternion.AngleAxis(t * arc * 0.5f, Vector3.forward) * forward).normalized;
    }

    // ---------------- synergy load -> energy cost ----------------
    //
    // Each synergy multiplies the energy cost by its weight, so they compound:
    // Triple Shot (x2) + Explosive (x1.5) + Burn (x1.15) = x3.45.
    // Stat ups (damage, rate, size, speed) are not synergies and cost nothing extra.

    public const float MultishotWeight = 2.0f;   // per Triple Shot's worth (2 extra shots)
    public const float ExplosiveWeight = 1.5f;
    public const float FragmentWeight  = 1.4f;
    public const float PierceWeight    = 1.3f;
    public const float HomingWeight    = 1.25f;
    public const float BounceWeight    = 1.2f;
    public const float PhasingWeight   = 1.2f;
    public const float GrowthWeight    = 1.2f;
    public const float ChildWeight     = 1.2f;   // each projectile child effect
    public const float ElementWeight   = 1.15f;  // each extra damage type
    public const float StatusWeight    = 1.15f;  // each status effect type
    public const float BigBoltWeight   = 1.3f;   // huge beams/flames/rails without the fire-rate penalty
    public const float BoltHelmWeight  = 1.25f;  // free echo bolts flavored by the equipped weapon
    public const float BoosterWeight   = 1.15f;  // Health Booster / Scrap Booster live scaling

    public struct Synergy
    {
        public string name;
        public float weight;
        public Synergy(string name, float weight) { this.name = name; this.weight = weight; }
        public override string ToString() { return name + " x" + weight.ToString("0.##"); }
    }

    /// <summary>Every synergy the bolt currently feeds into energy weapons, with its cost weight.</summary>
    public static List<Synergy> ActiveSynergies(Player p)
    {
        var list = new List<Synergy>();
        if (!p) { return list; }
        var b = p.projectileStats;

        var extra = ExtraShots(p);
        if (extra > 0)
        {
            // x2 for every 2 extra shots (Triple Shot = x2, two Triple Shots = x5.7)
            list.Add(new Synergy("Multishot +" + extra, Mathf.Pow(MultishotWeight, extra / 2f)));
        }
        if (b.statusEffects != null)
        {
            var seen = new HashSet<StatusEffectsType>();
            foreach (var e in b.statusEffects) { if (e && seen.Add(e.type)) list.Add(new Synergy(e.type.ToString(), StatusWeight)); }
        }
        foreach (DamageType d in Enum.GetValues(typeof(DamageType)))
        {
            if (d == DamageType.Generic || (b.damageType & d) == 0) continue;
            if (d == DamageType.Explosive && b.explosion != Explosion.None) continue; // counted once as Explosive below
            list.Add(new Synergy(d.ToString(), ElementWeight));
        }
        if (b.explosion != Explosion.None) list.Add(new Synergy("Explosive", ExplosiveWeight));
        if (b.fragment) list.Add(new Synergy("Fragment", FragmentWeight));
        if (b.penetrative) list.Add(new Synergy("Pierce", PierceWeight));
        if (b.homing > 0) list.Add(new Synergy("Homing", HomingWeight));
        if (b.bounce) list.Add(new Synergy("Bounce", BounceWeight));
        if (b.ignoreTerrain) list.Add(new Synergy("Phasing", PhasingWeight));
        if (b.sizePerSecond > 0 || b.damageGainPerSecond > 0) list.Add(new Synergy("Growth", GrowthWeight));
        if (b.childEffects != null) { foreach (var c in b.childEffects) { if (c) list.Add(new Synergy(c.name, ChildWeight)); } }
        if (HasBigBolt(p)) list.Add(new Synergy("Big Bolt", BigBoltWeight));
        if (HasBoltHelm(p)) list.Add(new Synergy("Bolt Helm", BoltHelmWeight));
        if (p.healthBooster) list.Add(new Synergy("Health Booster", BoosterWeight));
        if (p.scrapBooster) list.Add(new Synergy("Scrap Booster", BoosterWeight));
        return list;
    }

    private static int _costFrame = -1;
    private static Player _costPlayer;
    private static float _costValue = 1f;

    /// <summary>Product of every active synergy's weight (1 = no synergies).</summary>
    public static float CostMultiplier(Player p)
    {
        if (Time.frameCount != _costFrame || _costPlayer != p)
        {
            _costFrame = Time.frameCount;
            _costPlayer = p;
            var m = 1f;
            foreach (var s in ActiveSynergies(p)) { m *= s.weight; }
            _costValue = 1f + (m - 1f) * CharacterRoster.SynergyCostScale; // Volt pays half the surcharge
        }
        return _costValue;
    }

    public static float EnergyCost(Player p, float baseCost) { return baseCost * CostMultiplier(p); }

    // ---------------- damage types & status effects ----------------

    /// <summary>The weapon's own damage type plus every elemental type the bolt has gained.</summary>
    public static DamageType MergeDamageType(DamageType weaponType, Player p)
    {
        var extra = p.projectileStats.damageType & ~DamageType.Generic;
        return weaponType | extra;
    }

    /// <summary>The weapon's own status effects plus the bolt's (strongest of each type wins in Stack()).</summary>
    public static List<StatusEffect> MergeStatusEffects(IEnumerable<StatusEffect> weaponEffects, Player p)
    {
        var list = new List<StatusEffect>();
        if (weaponEffects != null) { foreach (var e in weaponEffects) { if (e) list.Add(e); } }
        if (p.projectileStats.statusEffects != null)
        {
            foreach (var e in p.projectileStats.statusEffects) { if (e) list.Add(e); }
        }
        return list;
    }

    // ---------------- projectile weapons ----------------

    [Flags]
    public enum Mods
    {
        None = 0,
        Status = 1, DamageType = 2, ChildEffects = 4, Pierce = 8, Bounce = 16,
        Fragment = 32, Explosive = 64, Phase = 128, Growth = 256, Homing = 512,
        All = ~0,
    }

    /// <summary>Copy the bolt's mods onto an energy-weapon projectile.
    /// If the weapon already explodes and the player has an explosive mod, the blast grows a tier.</summary>
    public static void ApplyShotMods(ProjectileStats s, Player p, Mods mods = Mods.All)
    {
        var b = p.projectileStats;

        if ((mods & Mods.Status) != 0 && b.statusEffects != null)
        {
            if (s.statusEffects == null) { s.statusEffects = new List<StatusEffect>(); }
            else { s.statusEffects = new List<StatusEffect>(s.statusEffects); }
            foreach (var e in b.statusEffects) { if (e && !s.statusEffects.Contains(e)) s.statusEffects.Add(e); }
        }

        if ((mods & Mods.DamageType) != 0) { s.damageType = MergeDamageType(s.damageType, p); }

        if ((mods & Mods.ChildEffects) != 0 && b.childEffects != null && b.childEffects.Count > 0)
        {
            s.childEffects = s.childEffects == null ? new List<ProjectileChildEffect>() : new List<ProjectileChildEffect>(s.childEffects);
            foreach (var c in b.childEffects) { if (c && !s.childEffects.Contains(c)) s.childEffects.Add(c); }
        }

        if ((mods & Mods.Pierce) != 0 && b.penetrative) { s.penetrative = true; }
        if ((mods & Mods.Bounce) != 0 && b.bounce) { s.bounce = true; }
        if ((mods & Mods.Phase) != 0 && b.ignoreTerrain) { s.ignoreTerrain = true; }

        if ((mods & Mods.Fragment) != 0 && b.fragment && !s.fragment)
        {
            s.fragment = true;
            s.fragmentStats = b.fragmentStats;
        }

        if ((mods & Mods.Explosive) != 0 && b.explosion != Explosion.None)
        {
            if (s.explosion == Explosion.None)
            {
                s.explosion = b.explosion;
                s.explosionDamage = b.explosionDamage;
            }
            else
            {
                // Already explosive weapon + explosive mod: bigger, harder blast.
                s.explosion = BumpExplosion(s.explosion);
                s.explosionDamage = s.explosionDamage * 1.5f + b.explosionDamage;
            }
        }

        if ((mods & Mods.Growth) != 0)
        {
            s.sizePerSecond += b.sizePerSecond;
            s.damageGainPerSecond += b.damageGainPerSecond;
        }

        if ((mods & Mods.Homing) != 0 && b.homing > s.homing)
        {
            s.homing = b.homing;
            s.homingRadius = Mathf.Max(s.homingRadius, b.homingRadius);
            s.homingArc = Mathf.Max(s.homingArc, b.homingArc);
        }
    }

    public static Explosion BumpExplosion(Explosion e)
    {
        switch (e)
        {
            case Explosion.E16x16: return Explosion.E32x32;
            case Explosion.E32x32: return Explosion.E48x48;
            case Explosion.E48x48: return Explosion.E64x64;
            case Explosion.Elec16x16: return Explosion.Elec32x32;
            case Explosion.Elec32x32: return Explosion.Elec48x48;
            case Explosion.Elec48x48: return Explosion.Elec64x64;
            default: return e;
        }
    }

    // ---------------- held / contact weapons (beams, flames, arcs, boomerangs) ----------------

    private static readonly Dictionary<long, float> _procTimers = new Dictionary<long, float>();
    private static float _lastPrune;

    /// <summary>On-hit callback for weapons that deal damage without projectiles.
    /// Feeds kill hooks (Hive Bolt etc.) and turns Explosive / Fragment into periodic procs on the target.
    /// procRate scales how often procs fire (held weapons use FireRate).</summary>
    public static Action<IDamageable> HitHandler(Player p, int weaponKey, float procRate = 1f)
    {
        return (d) => OnHit(p, d, weaponKey, procRate);
    }

    public static void OnHit(Player p, IDamageable d, int weaponKey, float procRate)
    {
        if (!p || d == null || d.gameObject == null) { return; }
        p.OnHurtEnemy(d);

        var now = Time.time;
        if (now - _lastPrune > 10f) { _procTimers.Clear(); _lastPrune = now; }

        var id = ((long)d.gameObject.GetInstanceID() << 8) ^ weaponKey;
        var rate = Mathf.Max(0.1f, procRate);

        if (Explosive(p) && Ready(id ^ 0x1000000, now, 0.6f / rate))
        {
            var b = p.projectileStats;
            ProjectileManager.instance.SpawnExplosion(d.position, b.explosion, p.team, b.explosionDamage, true, MergeDamageType(DamageType.Explosive, p));
        }

        if (Fragment(p) && Ready(id ^ 0x2000000, now, 0.8f / rate))
        {
            var b = p.projectileStats;
            var shards = Mathf.Max(2, b.fragmentStats.amount);
            for (int i = 0; i < shards; i++)
            {
                var s = new ProjectileStats(b);
                s.fragment = false;
                s.explosion = Explosion.None;
                s.isFragment = true;
                s.size = Mathf.Max(0.5f, b.size * b.fragmentStats.sizeMod);
                s.damage = b.damage * b.fragmentStats.damageMod;
                s.lifeSpan = b.fragmentStats.lifeSpan;
                s.homing = Mathf.Max(s.homing, 4f);
                s.homingRadius = Mathf.Max(s.homingRadius, 6f);
                var dir = (Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.forward) * Vector3.right);
                ProjectileManager.instance.Shoot(s, d.position + dir * 0.75f, dir);
            }
        }
    }

    private static bool Ready(long key, float now, float interval)
    {
        float last;
        if (_procTimers.TryGetValue(key, out last) && now - last < interval) { return false; }
        _procTimers[key] = now;
        return true;
    }

    /// <summary>Configure a damage trigger owned by a player weapon.</summary>
    public static void ConfigureTrigger(DamageCreatureTrigger t, Player p, DamageType weaponType, IEnumerable<StatusEffect> weaponEffects, int weaponKey, float procRate)
    {
        if (!t) { return; }
        t.damageType = MergeDamageType(weaponType, p);
        t.statusEffects = MergeStatusEffects(weaponEffects, p);
        t.onDamageParam = HitHandler(p, weaponKey, procRate);
    }
}
