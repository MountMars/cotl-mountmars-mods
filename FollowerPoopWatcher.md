# Follower Poop Watcher

`FollowerPoopWatcher.cs` is a BepInEx plugin for **Cult of the Lamb**
(`com.mountmars.cotl.followerpoopwatcher`, v1.0.0). It tracks follower
defecation events in real time and broadcasts notification messages
both to the BepInEx console log and over UDP (port 9999) for telemetry
bridges (such as `udp_to_ble.py`).

---

## What It Does

The plugin applies Harmony postfix patches to three follower tasks:

1. **`FollowerTask_Bathroom.ProgressTask`**:
   - Detects when a follower uses an outhouse (`UsingStructureID > 0`).
   - If pooping on the floor, inspects private field `poopType`
     (`StructureBrain.TYPES`) to identify special poop variants.

2. **`FollowerTask_InstantPoop.ProgressTask`**:
   - Triggers when a follower defecates immediately on the floor.
   - Inspects `poopType` to check for special poop variants.

3. **`FollowerTask_IllPoopy.ProgressTask`**:
   - Triggers when an ill follower poops on the floor.
   - Checks traits for special poop:
     - `RoyalPooper` -> `Gold`
     - `RotstonePooper` or `Mutated` -> `Rotstone`

---

## Poop Types Detected

Mapped from `StructureBrain.TYPES` by `Plugin.FormatSpecialPoop`:

| Type | Formatted Name |
| :--- | :--- |
| `POOP_GOLD` | `Gold` |
| `POOP_RAINBOW` | `Rainbow` |
| `POOP_MASSIVE` | `Massive` |
| `POOP_DEVOTION` | `Devotion` |
| `POOP_GLOW` | `Glow` |
| `POOP_ROTSTONE` | `Rotstone` |
| `POOP_PET` | `Pet` |
| Standard / Other | *(none)* |

---

## Action Detail Formats

The `actionDetail` string is generated based on context:

| Condition | `actionDetail` Output |
| :--- | :--- |
| Used toilet / outhouse | `Used the outhouse` |
| Normal floor poop | `Pooped on the floor` |
| Special floor poop | `Pooped (<Special>) on the floor` |
| Sick follower poop | `Pooped on the floor (Sick)` |
| Sick special poop | `Pooped (<Special>) on the floor (Sick)` |

*Example special outputs:*
- `Pooped (Gold) on the floor`
- `Pooped (Rotstone) on the floor (Sick)`

---

## Messages Sent

### Follower Name Resolution
- Uses `brain.Info.Name`.
- If missing or empty, defaults to: `Follower_<ID>` (e.g. `Follower_12`).

### 1. BepInEx Logger (Info Level)
Format:
```text
[POOP] <FollowerName>: <actionDetail>
```

Examples:
```text
[POOP] Narinder: Pooped on the floor
[POOP] Baal: Pooped (Gold) on the floor
[POOP] Follower_4: Used the outhouse
[POOP] Leshy: Pooped (Rotstone) on the floor (Sick)
```

### 2. UDP Telemetry Broadcast
Dispatched via `UdpMessenger.Send(...)` to `127.0.0.1:9999` as a UTF-8
string ending with a newline (`\n`):

Format:
```text
<FollowerName>: <actionDetail>\n
```

Examples:
```text
Narinder: Pooped on the floor\n
Baal: Pooped (Gold) on the floor\n
Follower_4: Used the outhouse\n
Leshy: Pooped (Rotstone) on the floor (Sick)\n
```
