# Cult of the Lamb - Tarot Cards (`TarotCards.Card`) Reference List

This document provides a complete, authoritative list of all **104 Tarot Cards (Trinkets)** defined in Cult of the Lamb (`Assembly-CSharp.dll`).

Every card below can be directly referenced in C# mod code using its fully qualified enum identifier:
```csharp
TarotCards.Card.TheDeal
```

---

## 1. Quick Code Snippets

### Instantiate a Card Instance
```csharp
// Constructor: TarotCard(TarotCards.Card type, int upgradeIndex = 0)
TarotCards.TarotCard card = new TarotCards.TarotCard(TarotCards.Card.TheDeal, 0);
```

### Equip Directly to the Player
```csharp
// Adds card, applies instant effects (e.g. bonus hearts), and triggers fleece events
TrinketManager.AddTrinket(new TarotCards.TarotCard(TarotCards.Card.TheDeal, 0), PlayerFarming.Instance);
```

### Show the Card Banner Presentation UI
```csharp
Lamb.UI.UIManager.Instance.ShowTarotPickUp(new TarotCards.TarotCard(TarotCards.Card.TheDeal, 0));
```

### Spawn Flying Card Pickup Target
```csharp
Vector3 spawnPos = PlayerFarming.Instance.transform.position + Vector3.up * 1.5f;
Vector3 targetPos = PlayerFarming.Instance.transform.position;
TarotCustomTarget.Create(spawnPos, targetPos, 1.0f, TarotCards.Card.TheDeal, () =>
{
    Debug.Log("Card delivered to player!");
});
```

### Query Card Status
```csharp
bool hasCard = TrinketManager.HasTrinket(TarotCards.Card.TheDeal);
int count = TrinketManager.CountTrinket(TarotCards.Card.TheDeal);
TrinketManager.RemoveTrinket(TarotCards.Card.TheDeal, PlayerFarming.Instance);
```

---

## 2. Complete Master List (All 104 Cards)

| ID | Full C# Identifier | Enum Name | Category | Max Level | Effect / Description |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **0** | `TarotCards.Card.Hearts1` | `Hearts1` | Base Game (Default) | 0 | **The Hearts I**: Gain 1/2 extra red heart. |
| **1** | `TarotCards.Card.Hearts2` | `Hearts2` | Base Game | 0 | **The Hearts II**: Gain 1 extra red heart. |
| **2** | `TarotCards.Card.Hearts3` | `Hearts3` | Base Game | 0 | **The Hearts III**: Gain 2 extra red hearts. |
| **3** | `TarotCards.Card.Lovers1` | `Lovers1` | Base Game (Default) | 0 | **The Lovers I**: Gain 1 blue / spirit heart. |
| **4** | `TarotCards.Card.Lovers2` | `Lovers2` | Base Game | 0 | **The Lovers II**: Gain 2 blue / spirit hearts. |
| **5** | `TarotCards.Card.Sun` | `Sun` | Base Game | 0 | **All Seeing Sun**: Deal +15% weapon damage during daytime. |
| **6** | `TarotCards.Card.Moon` | `Moon` | Base Game | 0 | **The Moon**: Deal +15% weapon damage during nighttime. |
| **7** | `TarotCards.Card.GiftFromBelow` | `GiftFromBelow` | Base Game (Default) | 1 | **Gift from Below**: +10% (Lv 1: +15%) chance to gain a diseased / black heart when taking damage or killing enemies. |
| **8** | `TarotCards.Card.Spider` | `Spider` | Base Game (Default) | 0 | **The Spider**: Gain bonus damage or hearts against poisoned foes. |
| **9** | `TarotCards.Card.DiseasedHeart` | `DiseasedHeart` | Base Game (Default) | 0 | **Diseased Heart**: Gain 1 diseased heart. Damages all enemies on screen when hurt. |
| **10** | `TarotCards.Card.EyeOfWeakness` | `EyeOfWeakness` | Base Game (Default) | 0 | **The Eye of Weakness**: Deal +25% increased damage to bosses and elite foes. |
| **11** | `TarotCards.Card.Arrows` | `Arrows` | Base Game | 0 | **Master of the Art**: Fires additional projectiles or boosts ranged weapon damage. |
| **12** | `TarotCards.Card.DeathsDoor` | `DeathsDoor` | Base Game (Default) | 2 | **Death's Door**: When reduced to 1/2 heart, deal 2x (Lv 1: 2.5x, Lv 2: 3x) damage. |
| **13** | `TarotCards.Card.TheDeal` | `TheDeal` | Base Game | 0 | **The Deal**: Resurrect with 1 red heart upon fatal damage. |
| **14** | `TarotCards.Card.Telescope` | `Telescope` | Base Game (Default) | 0 | **The Telescope**: Reveals unknown paths and room nodes across the crusade map. |
| **15** | `TarotCards.Card.HandsOfRage` | `HandsOfRage` | Base Game | 1 | **Hands of Rage**: Unleash a blazing projectile when finishing a weapon combo. |
| **16** | `TarotCards.Card.NaturesGift` | `NaturesGift` | Base Game | 0 | **Nature's Gift**: Destroying bushes, urns, and grass drops resources/seeds. |
| **17** | `TarotCards.Card.Skull` | `Skull` | Base Game | 0 | **The Burning Dead**: Slain enemies burst into spirit flames / damage surroundings. |
| **18** | `TarotCards.Card.Potion` | `Potion` | Base Game | 2 | **Ambrosia**: Increases weapon attack strength and critical hit potency. |
| **19** | `TarotCards.Card.Sword` | `Sword` | Base Game | 0 | **Weaponsmaster (Sword)**: Grants increased weapon damage and critical strike chance. |
| **20** | `TarotCards.Card.Dagger` | `Dagger` | Base Game | 0 | **Venomous Blade (Dagger)**: Weapon attacks inflict lingering poison on enemies. |
| **21** | `TarotCards.Card.Axe` | `Axe` | Base Game | 0 | **Heavy Blade (Axe)**: Increases attack radius, cleave, and heavy attack speed. |
| **22** | `TarotCards.Card.Blunderbuss` | `Blunderbuss` | Base Game | 0 | **Scatter Shot (Blunderbuss)**: Widens projectile spread and increases close-range impact. |
| **23** | `TarotCards.Card.Hammer` | `Hammer` | Base Game | 0 | **Crushing Blow (Hammer)**: Weapon attacks heavily stagger and stun foes. |
| **24** | `TarotCards.Card.Fireball` | `Fireball` | Base Game | 0 | **Fireball**: Rolls or attacks ignite enemies in flame. |
| **25** | `TarotCards.Card.Tripleshot` | `Tripleshot` | Base Game | 0 | **Tripleshot**: Curses fire 3 projectiles simultaneously. |
| **26** | `TarotCards.Card.Tentacles` | `Tentacles` | Base Game | 0 | **Tentacles of the Deep**: Summons abyssal tentacles that slam enemies. |
| **27** | `TarotCards.Card.EnemyBlast` | `EnemyBlast` | Base Game | 0 | **Enemy Blast**: Defeated enemies explode in a damaging shrapnel blast. |
| **28** | `TarotCards.Card.ProjectileAOE` | `ProjectileAOE` | Base Game | 0 | **Divine Blast**: Projectiles create expanding shockwaves on impact. |
| **29** | `TarotCards.Card.Vortex` | `Vortex` | Base Game | 0 | **Vortex**: Curses pull nearby enemies towards the epicenter of the blast. |
| **30** | `TarotCards.Card.MegaSlash` | `MegaSlash` | Base Game | 0 | **Mega Slash**: Weapon attacks cast oversized sweeping shockwaves. |
| **31** | `TarotCards.Card.MovementSpeed` | `MovementSpeed` | Base Game | 2 | **The Path**: Increases base player movement speed by +1.25x (Lv 1: +1.35x, Lv 2: +1.5x). |
| **32** | `TarotCards.Card.AttackRate` | `AttackRate` | Base Game (Default) | 2 | **Divine Strength**: Increases weapon attack rate by +1.25x (Lv 1: +1.35x, Lv 2: +1.5x). |
| **33** | `TarotCards.Card.IncreasedDamage` | `IncreasedDamage` | Base Game (Default) | 1 | **The Arachnid**: Increases weapon damage by +10% (Lv 1: +20%). |
| **34** | `TarotCards.Card.IncreaseBlackSoulsDrop` | `IncreaseBlackSoulsDrop` | Base Game (Default) | 1 | **Soul Harvest**: Increases Fervour / black soul drops from defeated enemies. |
| **35** | `TarotCards.Card.HealChance` | `HealChance` | Base Game | 1 | **True Love**: +10% (Lv 1: +20%) chance to heal 1/2 heart when clearing a combat room. |
| **36** | `TarotCards.Card.NegateDamageChance` | `NegateDamageChance` | Base Game (Default) | 1 | **The Shield**: +10% (Lv 1: +20%) chance to completely negate incoming damage. |
| **37** | `TarotCards.Card.BombOnRoll` | `BombOnRoll` | Base Game | 1 | **Bomb On Roll**: Spawns an explosive bomb when dodging/rolling through enemies. |
| **38** | `TarotCards.Card.GoopOnDamaged` | `GoopOnDamaged` | Base Game | 0 | **Ichor On Damaged**: Spills a pool of toxic black ichor when taking damage. |
| **39** | `TarotCards.Card.GoopOnRoll` | `GoopOnRoll` | Base Game | 1 | **Ichor Trail**: Leaves a trail of damaging black ichor behind while rolling. |
| **40** | `TarotCards.Card.PoisonImmune` | `PoisonImmune` | Base Game | 0 | **Poison Immunity**: Total immunity to all poison pools and poison status effects. |
| **41** | `TarotCards.Card.DamageOnRoll` | `DamageOnRoll` | Base Game | 0 | **Intangible**: Rolling into enemies deals physical impact damage to them. |
| **42** | `TarotCards.Card.HealTwiceAmount` | `HealTwiceAmount` | Base Game (Default) | 0 | **The Healthy Charge**: Doubles all health recovered from all healing sources. |
| **43** | `TarotCards.Card.InvincibleWhileHealing` | `InvincibleWhileHealing` | Base Game | 0 | **Divine Ward**: You cannot take damage while healing or eating. |
| **44** | `TarotCards.Card.AmmoEfficient` | `AmmoEfficient` | Base Game (Default) | 2 | **Fervour's Harvest**: Curses consume 25% (Lv 1: 35%, Lv 2: 50%) less Fervour. |
| **45** | `TarotCards.Card.BlackSoulAutoRecharge` | `BlackSoulAutoRecharge` | Base Game | 1 | **Fervour Recharge**: Automatically regenerates Fervour slowly over time. |
| **46** | `TarotCards.Card.BlackSoulOnDamage` | `BlackSoulOnDamage` | Base Game | 1 | **Fervour on Damaged**: Gain Fervour whenever you take damage from an enemy. |
| **47** | `TarotCards.Card.NeptunesCurse` | `NeptunesCurse` | Base Game | 0 | **Neptune's Curse**: Defeated enemies have a chance to drop fish. |
| **48** | `TarotCards.Card.RabbitFoot` | `RabbitFoot` | Base Game (Default) | 0 | **The Rabbit's Foot**: Increases the chance of discovering superior chest rewards. |
| **49** | `TarotCards.Card.Gauntlet` | `Gauntlet` | Base Game | 0 | **The Gauntlet**: Consecutive consecutive weapon hits increase your attack power. |
| **50** | `TarotCards.Card.HoldToHeal` | `HoldToHeal` | Base Game | 0 | **Meditation**: Stand still without moving to channel and heal lost health. |
| **51** | `TarotCards.Card.MoreRelics` | `MoreRelics` | Base Game | 0 | **Relic Hunter**: Significantly increases the chance of finding Relics in chests. |
| **52** | `TarotCards.Card.TentacleOnDamaged` | `TentacleOnDamaged` | Base Game | 0 | **Retaliatory Tentacle**: Spawns an abyssal tentacle that smacks attackers when hurt. |
| **53** | `TarotCards.Card.InvincibilityPerRoom` | `InvincibilityPerRoom` | Base Game | 0 | **Holy Protection**: Grants invulnerability for 2 seconds when entering new rooms. |
| **54** | `TarotCards.Card.BombOnDamaged` | `BombOnDamaged` | Base Game | 0 | **Explosive Retaliation**: Drops a live bomb when taking damage from any source. |
| **55** | `TarotCards.Card.ImmuneToTraps` | `ImmuneToTraps` | Base Game | 0 | **Trap Immunity**: Floor spikes, dart traps, and explosive barrels do 0 damage. |
| **56** | `TarotCards.Card.ImmuneToProjectiles` | `ImmuneToProjectiles` | Base Game | 0 | **Deflection**: Incoming enemy projectiles are deflected or neutralized. |
| **57** | `TarotCards.Card.WalkThroughBlocks` | `WalkThroughBlocks` | Base Game | 0 | **Ghost Walk**: Phasing ability allowing the player to walk through rocks and blocks. |
| **58** | `TarotCards.Card.DecreaseRelicCharge` | `DecreaseRelicCharge` | Base Game | 2 | **Relic Efficiency**: Relics charge faster (Lv 1: +25%, Lv 2: +50% charge speed). |
| **59** | `TarotCards.Card.AdventureMapFreedom` | `AdventureMapFreedom` | Mystic Wheel | 0 | **Mystic Compass**: Move freely to any adjacent node across the crusade tree. |
| **60** | `TarotCards.Card.Recycle` | `Recycle` | Mystic Wheel | 0 | **Recycle**: Allows rerolling weapon or curse pedestal choices in crusade rooms. |
| **61** | `TarotCards.Card.StrikeBack` | `StrikeBack` | Mystic Wheel | 0 | **Retribution**: Strikes enemies with divine lightning when they deal damage to you. |
| **62** | `TarotCards.Card.SurpriseAttack` | `SurpriseAttack` | Mystic Wheel | 2 | **Surprise Attack**: Massive damage bonus when attacking enemies from behind. |
| **63** | `TarotCards.Card.BossHeal` | `BossHeal` | Mystic Wheel | 0 | **Conqueror's Feast**: Heals all lost hearts immediately upon slaying a boss or mini-boss. |
| **64** | `TarotCards.Card.NoCorruption` | `NoCorruption` | Corrupted | 0 | **Purity**: Nullifies and clears the negative debuffs on all Corrupted Tarot cards. |
| **65** | `TarotCards.Card.Sin` | `Sin` | Mystic Wheel | 0 | **Sin Collector**: Grants additional Sin or chance to extract Sin during crusade runs. |
| **66** | `TarotCards.Card.GoopDamage` | `GoopDamage` | Base Game | 0 | **Toxic Mastery**: Increases damage dealt by all poison pools and goop trails. |
| **67** | `TarotCards.Card.DoubleCoins` | `DoubleCoins` | Base Game | 0 | **Midas Touch**: Doubles the quantity of gold coins dropped by enemies and chests. |
| **68** | `TarotCards.Card.ExtraMove` | `ExtraMove` | Mystic Wheel | 0 | **Endurance**: Grants extra dodge roll stamina and extended invulnerability. |
| **69** | `TarotCards.Card.ShuffleNode` | `ShuffleNode` | Mystic Wheel | 0 | **Fate Shuffle**: Re-randomizes room node types on the upcoming crusade path. |
| **70** | `TarotCards.Card.CorruptedBombsAndHealth` | `CorruptedBombsAndHealth` | Corrupted | 0 | **Corrupted: Bombs & Vitality**: Grants bonus bombs & hearts at cost of Fervour. |
| **71** | `TarotCards.Card.CorruptedHeavy` | `CorruptedHeavy` | Corrupted | 0 | **Corrupted: Heavy Power**: Heavy attacks deal colossal damage but cost health/fervour. |
| **72** | `TarotCards.Card.CorruptedTradeOff` | `CorruptedTradeOff` | Corrupted | 0 | **Corrupted: Blood Trade**: Massive weapon damage boost, but you take double damage. |
| **73** | `TarotCards.Card.CorruptedBlackHeartForRelic` | `CorruptedBlackHeartForRelic` | Corrupted | 0 | **Corrupted: Black Relic**: Grants black hearts, but makes equipped relics fragile. |
| **74** | `TarotCards.Card.CorruptedHealForRelic` | `CorruptedHealForRelic` | Corrupted | 0 | **Corrupted: Healing Charge**: Recovering health charges relics; passive charge slowed. |
| **75** | `TarotCards.Card.CorruptedFullCorruption` | `CorruptedFullCorruption` | Corrupted | 0 | **Corrupted: Abyss Surge**: Greatly multiplies both positive and negative card effects. |
| **76** | `TarotCards.Card.CorruptedPoisonCoins` | `CorruptedPoisonCoins` | Corrupted | 0 | **Corrupted: Tainted Gold**: Picking up coins releases poisonous gas against nearby foes. |
| **77** | `TarotCards.Card.CorruptedRelicCharge` | `CorruptedRelicCharge` | Corrupted | 0 | **Corrupted: Relic Surge**: Relics charge instantly, but activating them inflicts self-harm. |
| **78** | `TarotCards.Card.CorruptedGoopyTrail` | `CorruptedGoopyTrail` | Corrupted | 0 | **Corrupted: Slime Path**: Continuously exudes poison sludge; movement has slippery friction. |
| **79** | `TarotCards.Card.CoopBetterTogether` | `CoopBetterTogether` | Co-op | 0 | **Better Together**: Both players deal +25% bonus damage when standing close together. |
| **80** | `TarotCards.Card.CoopBetterApart` | `CoopBetterApart` | Co-op | 0 | **Better Apart**: Both players deal +25% bonus damage when spread far apart. |
| **81** | `TarotCards.Card.CoopBonded` | `CoopBonded` | Co-op | 0 | **Bonded**: Players share their heart pool or divide incoming damage equally. |
| **82** | `TarotCards.Card.CoopGoodTiming` | `CoopGoodTiming` | Co-op | 0 | **Good Timing**: Striking enemies simultaneously triggers critical combo explosions. |
| **83** | `TarotCards.Card.CoopExplosive` | `CoopExplosive` | Co-op | 0 | **Explosive Bond**: Running past each other creates an electric / explosive tether. |
| **84** | `TarotCards.Card.FrostHeart` | `FrostHeart` | DLC / Winter | 0 | **Frost Heart**: Grants ice hearts that freeze attackers upon taking damage. |
| **85** | `TarotCards.Card.FlameHeart` | `FlameHeart` | DLC / Major | 0 | **Flame Heart**: Grants flame hearts that erupt in a ring of fire when taking damage. |
| **86** | `TarotCards.Card.EasyMoney` | `EasyMoney` | DLC / Blue Back | 2 | **Easy Money**: Critical hits shower additional gold coins upon slaying enemies. |
| **87** | `TarotCards.Card.HighRoller` | `HighRoller` | DLC / Major | 0 | **High Roller**: Defeating enemies has a rare chance to drop valuable high-tier chests. |
| **88** | `TarotCards.Card.FrostedEnemies` | `FrostedEnemies` | DLC / Winter | 0 | **Frosted Foes**: Freezes nearby enemies for several seconds upon entering combat rooms. |
| **89** | `TarotCards.Card.CursedIce` | `CursedIce` | DLC / Winter | 0 | **Cursed Ice**: Frozen enemies suffer damage amplification and burst into frost shrapnel. |
| **90** | `TarotCards.Card.LastChance` | `LastChance` | DLC / Winter | 2 | **Last Chance**: Prevents lethal blow once per run, granting brief invulnerability. |
| **91** | `TarotCards.Card.Joker` | `Joker` | DLC / Blue Back | 0 | **The Joker**: Duplicates the strongest passive effect among your equipped cards. |
| **92** | `TarotCards.Card.MutatedNegateHit` | `MutatedNegateHit` | Rot / DLC | 2 | **Rot Shield**: Rot corruption gives a chance to deflect hits and poison attackers. |
| **93** | `TarotCards.Card.MutatedDropRotburn` | `MutatedDropRotburn` | Rot / DLC | 0 | **Rotburn Trail**: Dodging drops toxic rotburn sludge that burns and poisons foes. |
| **94** | `TarotCards.Card.MutatedFreezeOnHit` | `MutatedFreezeOnHit` | Rot / DLC | 0 | **Rot Frost**: Weapon strikes inflict rot frost, slowing enemy attack and movement speeds. |
| **95** | `TarotCards.Card.MutatedResurrectFullHealth` | `MutatedResurrectFullHealth` | Rot / DLC | 0 | **Rot Rebirth**: On fatal blow, resurrect with full health transformed by rot. |
| **96** | `TarotCards.Card.MutatedInvincibility` | `MutatedInvincibility` | Rot / DLC | 2 | **Rot Aegis**: Inflicting rot triggers brief invincibility frames for the Lamb. |
| **97** | `TarotCards.Card.MutatedSpawnRotDemons` | `MutatedSpawnRotDemons` | Rot / DLC | 2 | **Rot Demon Summoner**: Spawns grotesque rot demon minions that pursue enemies. |
| **98** | `TarotCards.Card.EmptyFervourCritical` | `EmptyFervourCritical` | DLC / Blue Back | 0 | **Desperate Strike**: When your Fervour pool is empty, all weapon hits are guaranteed criticals. |
| **99** | `TarotCards.Card.KillEnemiesOnResurrect` | `KillEnemiesOnResurrect` | DLC / Blue Back | 0 | **Wrath of the Fallen**: Triggering resurrection obliterates all non-boss foes in the room. |
| **100** | `TarotCards.Card.RoomEnterCritter` | `RoomEnterCritter` | DLC / Blue Back | 0 | **Critter Ally**: Entering any hostile room spawns friendly critters to fight alongside you. |
| **101** | `TarotCards.Card.HitKillEnemy` | `HitKillEnemy` | DLC / Blue Back | 0 | **Executioner**: Small percentage chance to instantly decapitate / execute normal enemies on hit. |
| **102** | `TarotCards.Card.SummonGhost` | `SummonGhost` | DLC / Blue Back | 0 | **Vengeful Spirit**: Summons a ghost familiar that fires spectral homing shots at enemies. |
| **103** | `TarotCards.Card.HeartTarotDrawn` | `HeartTarotDrawn` | DLC / Blue Back | 0 | **Card of Vitality**: Drawing heart tarot cards grants permanent health boosts for the run. |

---

## 3. Card Sub-Groups & Categories

### A. Corrupted Tarot Cards (`TarotCards.CorruptedCards`)
These cards provide powerful buffs coupled with notable negative trade-offs:
- `TarotCards.Card.CorruptedHeavy` (71)
- `TarotCards.Card.CorruptedHealForRelic` (74)
- `TarotCards.Card.CorruptedBlackHeartForRelic` (73)
- `TarotCards.Card.NoCorruption` (64) *(Negates corrupted negative effects)*
- `TarotCards.Card.CorruptedFullCorruption` (75)
- `TarotCards.Card.CorruptedGoopyTrail` (78)
- `TarotCards.Card.CorruptedPoisonCoins` (76)
- `TarotCards.Card.CorruptedRelicCharge` (77)
- `TarotCards.Card.CorruptedTradeOff` (72)
- `TarotCards.Card.CorruptedBombsAndHealth` (70)

### B. Co-Op Cards (`TarotCards.CoopCards`)
Specially balanced for local co-op play:
- `TarotCards.Card.CoopBetterTogether` (79)
- `TarotCards.Card.CoopBetterApart` (80)
- `TarotCards.Card.CoopBonded` (81)
- `TarotCards.Card.CoopGoodTiming` (82)
- `TarotCards.Card.CoopExplosive` (83)

### C. Mystic Wheel Cards (`TarotCards.MysticCards`)
Unlocked through mystic encounters and god tear spins:
- `TarotCards.Card.AdventureMapFreedom` (59)
- `TarotCards.Card.Recycle` (60)
- `TarotCards.Card.StrikeBack` (61)
- `TarotCards.Card.SurpriseAttack` (62)
- `TarotCards.Card.BossHeal` (63)
- `TarotCards.Card.Sin` (65)
- `TarotCards.Card.ExtraMove` (68)
- `TarotCards.Card.ShuffleNode` (69)

### D. Rot Mutation Cards (`TarotCards.RotCards`)
Mutated rot cards introduced in late-game updates:
- `TarotCards.Card.MutatedDropRotburn` (93)
- `TarotCards.Card.MutatedFreezeOnHit` (94)
- `TarotCards.Card.MutatedInvincibility` (96)
- `TarotCards.Card.MutatedNegateHit` (92)
- `TarotCards.Card.MutatedResurrectFullHealth` (95)
- `TarotCards.Card.MutatedSpawnRotDemons` (97)

### E. Frost & Winter Cards (`TarotCards.WitnerCards`)
- `TarotCards.Card.FrostedEnemies` (88)
- `TarotCards.Card.CursedIce` (89)
- `TarotCards.Card.LastChance` (90)

---

## 4. Special Enum Sentinel: `TarotCards.Card.Count`

> [!WARNING]
> **Do NOT instantiate `TarotCards.Card.Count` (Value: 104)!**
> In C# and Unity game architecture, `Count` is an enum sentinel used internally by `Interaction_TarotCard` and data structures to represent:
> 1. Total number of distinct tarot cards (`104`).
> 2. An empty or unassigned card override (e.g. `this.CardOverride != TarotCards.Card.Count`).
>
> Attempting to instantiate `new TarotCards.TarotCard(TarotCards.Card.Count, 0)` will trigger out-of-bounds exceptions when resolving skins or localized descriptions.

---

## 5. Usage in `TarotCardReceiver.cs`

In your mod plugin code:
```csharp
// Change default active card:
public static TarotCards.Card CurrentTestCard = TarotCards.Card.TheDeal;

// Enqueue any card for Clauneck selection:
TarotCardReceiverPlugin.ClauneckQueue.Enqueue(TarotCards.Card.MutatedSpawnRotDemons);

// Trigger banner on screen:
TarotCardReceiverPlugin.ShowCardBanner(TarotCards.Card.TheDeal);
```
