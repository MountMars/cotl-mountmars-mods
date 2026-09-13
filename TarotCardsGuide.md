# Cult of the Lamb - Tarot Cards Developer Guide

This document details how Tarot cards (Trinkets) work internally in Cult of the Lamb and how to programmatically instantiate, equip, spawn, and override specific cards instead of relying on random draws like `TarotCards.DrawRandomCard`.

---

## 1. Core Architecture

Tarot cards in the game are managed across three primary types:

| Class / Enum | Namespace | Description |
| :--- | :--- | :--- |
| `TarotCards.Card` | Global | Enum containing all card IDs (e.g. `Card.Hearts1`, `Card.TheDeal`). |
| `TarotCards.TarotCard` | Global | Class representing an instantiated card instance with its type and upgrade level. |
| `TrinketManager` | Global | Static manager tracking active cards on the player, applying passive bonuses, and invoking events. |

---

## 2. Instantiating a Specific Tarot Card

Instead of `TarotCards.DrawRandomCard(PlayerFarming, bool)`, you can directly instantiate any Tarot card using its constructor:

```csharp
// Constructor signature: TarotCard(TarotCards.Card type, int upgradeIndex = 0)
TarotCards.TarotCard card = new TarotCards.TarotCard(TarotCards.Card.IncreasedDamage, 0);
```

- **`upgradeIndex`**: Represents the card level (normally `0` for base level, or up to `TarotCards.GetMaxTarotCardLevel(cardType)`).

---

## 3. Giving a Specific Card Directly to the Player

To equip a card onto the player for the active run:

```csharp
using UnityEngine;

// 1. Create the specific card
TarotCards.TarotCard card = new TarotCards.TarotCard(TarotCards.Card.Hearts2, 0);

// 2. Add it to the TrinketManager
PlayerFarming player = PlayerFarming.Instance;
TrinketManager.AddTrinket(card, player);
```

### What `TrinketManager.AddTrinket` handles automatically:
1. Fallback validation (defaults to `PlayerFarming.Instance` if `playerFarming` is null).
2. Notifies the fleece manager (`PlayerFleeceManager.OnTarotCardPickedUp(player)`).
3. Adds the card to `TrinketManager.AllTrinkets` and `DataManager.Instance.PlayerRunTrinkets`.
4. Executes immediate card effects via `TarotCards.ApplyInstantEffects(card, player)` (e.g. grants bonus hearts or spirit hearts).
5. Dispatches the `TrinketManager.OnTrinketAdded` event for UI and audio hooks.

---

## 4. Spawning the Animated In-Game Pickup (`TarotCustomTarget`)

If you want the in-game animation where a card flies toward the player and triggers the pickup prompt:

```csharp
using UnityEngine;

Vector3 spawnPos = PlayerFarming.Instance.transform.position + Vector3.up * 1.5f;
Vector3 targetPos = PlayerFarming.Instance.transform.position;
float flightDuration = 1.0f;

TarotCustomTarget.Create(spawnPos, targetPos, flightDuration, TarotCards.Card.TheDeal, () =>
{
    // Callback invoked when the animation finishes and the card is received
    Debug.Log("Tarot card successfully delivered to player!");
});
```

---

## 5. Overriding Shrine and World Pedestal Cards (`Interaction_TarotCard`)

Dungeon tarot card shrines/stands use `Interaction_TarotCard`. This class contains a built-in override field:

```csharp
Interaction_TarotCard shrineCard = GetComponent<Interaction_TarotCard>();

// Force the shrine to offer a specific card instead of a random draw:
shrineCard.CardOverride = TarotCards.Card.AttackRate;
```

When interacted with, `Interaction_TarotCard.DoRoutineRoutine()` checks:
```csharp
if (this.CardOverride != TarotCards.Card.Count)
{
    DataManager.UnlockTrinket(this.CardOverride);
    this.DrawnCard = new TarotCards.TarotCard(this.CardOverride, 0);
}
else
{
    this.DrawnCard = TarotCards.DrawRandomCard(base.playerFarming, false);
}
```

---

## 6. Clauneck Tarot Room Card Selection (`Interaction_Tarot`)

When visiting Clauneck in a crusade room, the interaction is handled by **`Interaction_Tarot`**.

### How Clauneck Picks the Two Cards:
Inside `Interaction_Tarot.DoRoutineRoutine()`:
1. `card1 = this.GetCard(true);`
2. `bool canBeCorrupted = (card1 == null) || !TarotCards.CorruptedCards.Contains(card1.CardType);`
   `card2 = this.GetCard(canBeCorrupted);`
3. `UIManager.Instance.ShowTarotChoice(card1, card2);`

### Does it use `DrawRandomCard`?
**Yes.** `Interaction_Tarot.GetCard(bool canBeCorrupted)` calls `TarotCards.DrawRandomCard(base.playerFarming, canBeCorrupted)`.
- **First Run Exception**: If `!DataManager.Instance.FirstTarot` (the tutorial card), it gives `Card.Lovers1`.
- **Poison Weapon Check**: If it rolls `Card.Spider` and the player does not have a poison weapon equipped, it rerolls once with `DrawRandomCard(...)`.

### Hooking Clauneck's Cards:
To intercept cards offered by Clauneck specifically without affecting other card drops, patch `Interaction_Tarot.GetCard`:

```csharp
[HarmonyPatch(typeof(Interaction_Tarot), "GetCard")]
public static class ClauneckCardPatch
{
    public static bool Prefix(bool canBeCorrupted, ref TarotCards.TarotCard __result)
    {
        // Custom logic for Clauneck's offered cards
        __result = new TarotCards.TarotCard(TarotCards.Card.IncreasedDamage, 0);
        return false; // Skip original random selection
    }
}
```

---

## 7. Triggering the Tarot Card UI Banner (`Lamb.UI.UIManager`)

To open the full-screen Tarot card presentation banner:

```csharp
var card = new TarotCards.TarotCard(TarotCards.Card.MovementSpeed, 0);
Lamb.UI.UIManager.Instance.ShowTarotPickUp(card);
```

For offering a choice between two specific cards:
```csharp
var cardA = new TarotCards.TarotCard(TarotCards.Card.Sword, 0);
var cardB = new TarotCards.TarotCard(TarotCards.Card.Potion, 0);

var menu = Lamb.UI.UIManager.Instance.ShowTarotChoice(cardA, cardB);
menu.OnTarotCardSelected += (chosenCard) =>
{
    TrinketManager.AddTrinket(chosenCard, PlayerFarming.Instance);
};
```

---

## 8. Intercepting `DrawRandomCard` with Harmony

If you want all random card draws in the base game to yield a specific card instead:

```csharp
using HarmonyLib;

[HarmonyPatch(typeof(TarotCards), nameof(TarotCards.DrawRandomCard))]
public static class OverrideDrawRandomCardPatch
{
    public static bool Prefix(PlayerFarming playerFarming, bool canBeCorrupted, ref TarotCards.TarotCard __result)
    {
        // Replace random selection with your desired card
        __result = new TarotCards.TarotCard(TarotCards.Card.TheDeal, 0);

        // Return false to skip the original random selection logic
        return false;
    }
}
```

---

## 9. Querying and Removing Cards

`TrinketManager` provides helper methods to inspect and manage currently active cards:

```csharp
// Check if player has a specific card
bool hasDeal = TrinketManager.HasTrinket(TarotCards.Card.TheDeal);
bool hasDealPlayer = TrinketManager.HasTrinket(TarotCards.Card.TheDeal, PlayerFarming.Instance);

// Count how many copies of a card are active
int count = TrinketManager.CountTrinket(TarotCards.Card.Hearts1);

// Remove a specific card
TrinketManager.RemoveTrinket(TarotCards.Card.TheDeal, PlayerFarming.Instance);

// Remove all cards
TrinketManager.RemoveAllTrinkets(PlayerFarming.Instance);
```

---

## 10. Complete `TarotCards.Card` Enum Reference

> [!TIP]
> For the comprehensive card guide with full C# syntax identifiers, category breakdowns, max upgrade levels, and gameplay descriptions, see **[TarotCardList.md](file:///c:/Program%20Files%20%28x86%29/Steam/steamapps/common/Cult%20of%20the%20Lamb/BepInEx/plugins/TarotCardList.md)**.

| ID | Name | ID | Name |
| :--- | :--- | :--- | :--- |
| 0 | `Hearts1` | 52 | `TentacleOnDamaged` |
| 1 | `Hearts2` | 53 | `InvincibilityPerRoom` |
| 2 | `Hearts3` | 54 | `BombOnDamaged` |
| 3 | `Lovers1` | 55 | `ImmuneToTraps` |
| 4 | `Lovers2` | 56 | `ImmuneToProjectiles` |
| 5 | `Sun` | 57 | `WalkThroughBlocks` |
| 6 | `Moon` | 58 | `DecreaseRelicCharge` |
| 7 | `GiftFromBelow` | 59 | `AdventureMapFreedom` |
| 8 | `Spider` | 60 | `Recycle` |
| 9 | `DiseasedHeart` | 61 | `StrikeBack` |
| 10 | `EyeOfWeakness` | 62 | `SurpriseAttack` |
| 11 | `Arrows` | 63 | `BossHeal` |
| 12 | `DeathsDoor` | 64 | `NoCorruption` |
| 13 | `TheDeal` | 65 | `Sin` |
| 14 | `Telescope` | 66 | `GoopDamage` |
| 15 | `HandsOfRage` | 67 | `DoubleCoins` |
| 16 | `NaturesGift` | 68 | `ExtraMove` |
| 17 | `Skull` | 69 | `ShuffleNode` |
| 18 | `Potion` | 70 | `CorruptedBombsAndHealth` |
| 19 | `Sword` | 71 | `CorruptedHeavy` |
| 20 | `Dagger` | 72 | `CorruptedTradeOff` |
| 21 | `Axe` | 73 | `CorruptedBlackHeartForRelic` |
| 22 | `Blunderbuss` | 74 | `CorruptedHealForRelic` |
| 23 | `Hammer` | 75 | `CorruptedFullCorruption` |
| 24 | `Fireball` | 76 | `CorruptedPoisonCoins` |
| 25 | `Tripleshot` | 77 | `CorruptedRelicCharge` |
| 26 | `Tentacles` | 78 | `CorruptedGoopyTrail` |
| 27 | `EnemyBlast` | 79 | `CoopBetterTogether` |
| 28 | `ProjectileAOE` | 80 | `CoopBetterApart` |
| 29 | `Vortex` | 81 | `CoopBonded` |
| 30 | `MegaSlash` | 82 | `CoopGoodTiming` |
| 31 | `MovementSpeed` | 83 | `CoopExplosive` |
| 32 | `AttackRate` | 84 | `FrostHeart` |
| 33 | `IncreasedDamage` | 85 | `FlameHeart` |
| 34 | `IncreaseBlackSoulsDrop` | 86 | `EasyMoney` |
| 35 | `HealChance` | 87 | `HighRoller` |
| 36 | `NegateDamageChance` | 88 | `FrostedEnemies` |
| 37 | `BombOnRoll` | 89 | `CursedIce` |
| 38 | `GoopOnDamaged` | 90 | `LastChance` |
| 39 | `GoopOnRoll` | 91 | `Joker` |
| 40 | `PoisonImmune` | 92 | `MutatedNegateHit` |
| 41 | `DamageOnRoll` | 93 | `MutatedDropRotburn` |
| 42 | `HealTwiceAmount` | 94 | `MutatedFreezeOnHit` |
| 43 | `InvincibleWhileHealing` | 95 | `MutatedResurrectFullHealth` |
| 44 | `AmmoEfficient` | 96 | `MutatedInvincibility` |
| 45 | `BlackSoulAutoRecharge` | 97 | `MutatedSpawnRotDemons` |
| 46 | `BlackSoulOnDamage` | 98 | `EmptyFervourCritical` |
| 47 | `NeptunesCurse` | 99 | `KillEnemiesOnResurrect` |
| 48 | `RabbitFoot` | 100 | `RoomEnterCritter` |
| 49 | `Gauntlet` | 101 | `HitKillEnemy` |
| 50 | `HoldToHeal` | 102 | `SummonGhost` |
| 51 | `MoreRelics` | 103 | `HeartTarotDrawn` |
