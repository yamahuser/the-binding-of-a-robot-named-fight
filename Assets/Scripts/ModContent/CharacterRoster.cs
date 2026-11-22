using System.Collections.Generic;
using UnityEngine;

// [MODCONTENT] Playable characters. Each one is:
//   - a hidden "trait" MajorItemInfo that the normal item pipeline applies (stats, bolt mods, regen...)
//     without it ever appearing in the inventory or save,
//   - a starting kit granted when the run is created (non-traversal items are kept out of room placement),
//   - a look built from existing parts (helmet, shoulder pad, palette) or the Samus sprite,
//   - a strength AND a strong flaw, so nobody is strictly better than Fight. Rules that the item
//     pipeline can't express are read by small hooks (damage taken, healing, energy regen,
//     scrap per hit, synergy cost, shots fired from a familiar).
// The chosen id is saved on the run (SaveGameData.character). Missing = "Fight".
public class CharacterDef
{
    public string id;
    public string name;
    public string role;
    public string description;
    public string strength;                  // shown on the select screen
    public string flaw;                      // shown on the select screen
    public MajorItem[] startingItems = new MajorItem[0];
    public int startingScrap;
    public string head;                      // PlayerParts/<head>Head
    public string shoulderPad;               // PlayerParts/<pad>ShoulderPad
    public string palette;                   // Palettes/Player/PlayerPalette<palette>
    public bool samusSprite;

    // rule hooks
    public float damageTakenMultiplier = 1f; // Player.HandleDamage
    public float healingMultiplier = 1f;     // Player.GainHealth
    public bool noEnergyRegen;               // Player passive energy regen
    public float synergyCostScale = 1f;      // WeaponSynergy.CostMultiplier (0.5 = surcharge halved)
    public int hitsPerRoom;
    public bool noBolt;                      // Volt: no arm-cannon bolt; attack always uses defaultWeapon
    public MajorItem defaultWeapon;          // auto-selected energy weapon (CharacterRules + Player attack hook)
    public float beamBaseRange;              // Volt: Laser Beam range = this x shot size (0 = normal)
    public float salvageChance;              // Scrapper: chance an enemy drops gray scrap on death                  // Wraith: max health = this, each hit costs 1, refills per room (CharacterRules)
    public bool firesFromFamiliar;           // Player.GetAimingInfo -> SwarmBrood
    public bool minionSynergy;               // MinionLink: orbs scale with his stats and bolt mods

    // select-screen stat bars, 0..5
    public int barHealth = 3, barSpeed = 3, barDamage = 3, barFireRate = 3, barEnergy = 3;
    public System.Action<MajorItemInfo> configureTrait;

    private MajorItemInfo _trait;
    public MajorItemInfo Trait
    {
        get
        {
            if (_trait == null && configureTrait != null)
            {
                _trait = ScriptableObject.CreateInstance<MajorItemInfo>();
                _trait.name = "CharacterTrait_" + id;
                _trait.fullName = name;
                _trait.type = MajorItem.None;
                _trait.decalOrder = -100;
                configureTrait(_trait);
            }
            return _trait;
        }
    }
}

public static class CharacterRoster
{
    public const string DefaultId = "Fight";

    public static readonly List<CharacterDef> All = new List<CharacterDef>
    {
        new CharacterDef
        {
            id = "Fight", name = "FIGHT", role = "Balanced",
            description = "The original robot. The yardstick every other robot is measured against.",
            strength = "No weaknesses.",
            flaw = "No strengths.",
        },
        new CharacterDef
        {
            id = "Volt", name = "VOLT", role = "Beam caster",
            description = "No arm cannon. His only attack is a laser beam he projects from himself, fed by a huge, fast-charging battery.",
            strength = "Laser Beam is his default attack. +8 energy, very fast recharge, electric damage, synergy surcharge halved.",
            flaw = "No normal bolt at all. The beam is short (it reaches further as shot size grows), and when energy runs dry he can't attack.",
            startingItems = new[] { MajorItem.LaserBeam },
            defaultWeapon = MajorItem.LaserBeam,
            noBolt = true,
            beamBaseRange = 6f,
            head = "CognitiveStabilizer", palette = "BrightShell",
            synergyCostScale = 0.5f,
            barHealth = 3, barSpeed = 3, barDamage = 3, barFireRate = 4, barEnergy = 5,
            configureTrait = t =>
            {
                t.baseEnergyBonus = 8;
                t.itemEnergyRegenRate = 0.9f;
                t.applyDamageTypeToProjectile = true;
                t.damageType = DamageType.Electric;
            },
        },
        new CharacterDef
        {
            id = "Wraith", name = "WRAITH", role = "Glass cannon",
            description = "Kills fast, moves fast. Has no health bar: just three hits to spend in every room.",
            strength = "x1.35 damage, much faster movement, piercing bolts. Starts with Cloaking Device. Hits refill on entering a new room.",
            flaw = "Any hit costs one of only 3 per room, however small. Healing and health upgrades do nothing.",
            startingItems = new[] { MajorItem.CloakingDevice },
            head = "TyrsHorns", palette = "WraithShell",
            hitsPerRoom = 3,
            healingMultiplier = 0f,
            barHealth = 1, barSpeed = 5, barDamage = 5, barFireRate = 3, barEnergy = 2,
            configureTrait = t =>
            {
                t.baseSpeedBonus = 2f;
                t.damageMultiplier = 1.35f;
                t.penetrativeShot = true;
            },
        },
        new CharacterDef
        {
            id = "Swarmlord", name = "SWARMLORD", role = "Summoner",
            description = "Can't fire a shot himself. His Brood fires everything for him: bolts, beams, flames, all with his upgrades.",
            strength = "The Brood inherits every upgrade and weapon. All his orbs scale with his stats: damage, fire rate, size, speed, health and energy, plus his bolt mods. +2 pickup range.",
            flaw = "Every shot fires from the Brood, which trails behind him and gets blocked when it drifts into walls.",
            head = "HiveHelm", palette = "HiveShell",
            firesFromFamiliar = true,
            minionSynergy = true,
            barHealth = 3, barSpeed = 3, barDamage = 3, barFireRate = 3, barEnergy = 3,
            configureTrait = t =>
            {
                t.pickUpRangeBonus = 2f;
            },
        },
        new CharacterDef
        {
            id = "Scrapper", name = "SCRAPPER", role = "Salvager",
            description = "Scrap is everything: his health, his power and his money. The more he hoards, the harder he hits.",
            strength = "His bolt grows with every scrap carried, colored scrap powers his stats, kills often drop scrap, and he pulls pickups from far away.",
            flaw = "Scrap is his health. Every hit knocks scrap out of him, which also shrinks his bolt, and every shop purchase spends his life.",
            startingItems = new[] { MajorItem.ArtificeHelm, MajorItem.ScrapBooster },
            startingScrap = 20,
            salvageChance = 0.35f,
            palette = "AspectShell",
            barHealth = 2, barSpeed = 3, barDamage = 4, barFireRate = 3, barEnergy = 3,
            configureTrait = t =>
            {
                t.pickUpRangeBonus = 3f;
            },
        },
        new CharacterDef
        {
            id = "Hunter", name = "HUNTER", role = "Bounty hunter",
            description = "Fast, precise, missiles from the start. Every shot of ammo has to be found.",
            strength = "Fast shots, faster movement. Starts with Rocket Launcher. Uses the Samus sprite.",
            flaw = "Energy never recharges on its own; it only comes from pickups.",
            startingItems = new[] { MajorItem.RocketLauncher },
            samusSprite = true,
            noEnergyRegen = true,
            barHealth = 3, barSpeed = 4, barDamage = 3, barFireRate = 3, barEnergy = 1,
            configureTrait = t =>
            {
                t.baseShotSpeedBonus = 3f;
                t.baseSpeedBonus = 0.5f;
            },
        },
    };

    public static CharacterDef Get(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            foreach (var c in All) { if (c.id == id) return c; }
        }
        return All[0];
    }

    /// <summary>The character of the run in progress (Fight when none is set).</summary>
    public static CharacterDef Current
    {
        get
        {
            var game = SaveGameManager.activeGame;
            return Get(game != null ? game.character : null);
        }
    }

    public static bool IsDefault(CharacterDef c) { return c == null || c.id == DefaultId; }

    public static bool IsCharacterHead(string head) { return !string.IsNullOrEmpty(head) && All.Exists(c => c.head == head); }
    public static bool IsCharacterShoulderPad(string pad) { return !string.IsNullOrEmpty(pad) && All.Exists(c => c.shoulderPad == pad); }

    public static float DamageTakenMultiplier { get { return Current.damageTakenMultiplier; } }
    public static float HealingMultiplier { get { return Current.healingMultiplier; } }
    public static bool NoEnergyRegen { get { return Current.noEnergyRegen; } }
    public static float SynergyCostScale { get { return Current.synergyCostScale; } }
    public static bool ForcesSamusSprite { get { return Current.samusSprite; } }

    /// <summary>Select the character's default energy weapon if it is owned and usable.</summary>
    public static bool SelectDefaultWeapon(Player p)
    {
        var c = Current;
        if (!p || c.defaultWeapon == MajorItem.None) { return false; }
        var index = p.energyWeapons.FindIndex(w => w && w.item == c.defaultWeapon);
        if (index < 0 || !p.energyWeapons[index].Usable() || p.energy <= 0) { return false; }
        if (p.selectedEnergyWeapon == p.energyWeapons[index]) { return true; }
        p.SelectEnergyWeapon(index);
        return true;
    }

    /// <summary>Grant the character's kit to a freshly created run (called while the run is set up).</summary>
    public static void GrantStartingKit(SaveGameData game)
    {
        var c = Get(game.character);
        foreach (var item in c.startingItems)
        {
            MajorItemInfo info;
            if (!ItemManager.items.TryGetValue(item, out info)) { continue; }
            if (!game.itemsCollected.Contains(item)) { game.itemsCollected.Add(item); }
            if (info.isActivatedItem) { game.currentActivatedItem = item; }
            else if (!game.itemsPossessed.Contains(item)) { game.itemsPossessed.Add(item); }
        }
        game.grayScrap += c.startingScrap;
    }

    /// <summary>Non-traversal starting items should not also be placed in rooms.</summary>
    public static IEnumerable<MajorItem> NonTraversalStartingItems(string characterId)
    {
        var c = Get(characterId);
        foreach (var item in c.startingItems)
        {
            MajorItemInfo info;
            if (ItemManager.items.TryGetValue(item, out info) && !info.isTraversalItem) { yield return item; }
        }
    }
}
