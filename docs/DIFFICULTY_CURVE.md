# Booty! Difficulty Curve Document
**Sprint:** S3.6 — Difficulty Scaling
**Date:** 2026-03-16
**System:** Dual-source tier assignment + renown-based HP scaling

---

## Overview

Enemy difficulty scales on two axes:
1. **Tier** — which ship class spawns (sloop/brig/frigate stats proxy)
2. **HP multiplier** — how much more HP the enemy has based on player renown

Both axes are driven by the player's progression, creating an organic difficulty ramp without manual level design.

---

## Tier Assignment

Enemy tier is the **maximum** of two sources:

### Source 1: Port Defense Rating
Enemies spawned near a port are at least as strong as the port's defenses:

| Port `defense_rating` | Port Tier |
|----------------------|-----------|
| 1.0 | Tier 1 (sloop-class) |
| 2.0 | Tier 2 (brig-class) |
| 3.0–4.0 | Tier 2 (brig-class) |
| 5.0 | Tier 3 (frigate-class) |

Formula: `portTier = Clamp((int)defenseRating / 2 + 1, 1, 3)`

### Source 2: Player Renown
As the player gains notoriety, enemies everywhere become harder:

| Renown Range | Tier Label | Minimum Enemy Tier |
|-------------|-----------|-------------------|
| 0 – 49 | Unknown | Tier 1 |
| 50 – 149 | Notorious | **Tier 2** |
| 150 – 399 | Feared | **Tier 3** |
| 400+ | Legendary | **Tier 3** |

### Final Tier
```
finalTier = Max(portTier, renownTier)
```

A Feared-renown player (150+) fighting near a weak port (defense 1) still faces tier-3 enemies. A weaker player at a heavily-fortified port faces the port's minimum tier. Always the harder of the two.

---

## HP Scaling Formula

```
baseHP    = DefaultEnemyHP + (tier - 1) × 20
scaledHP  = Round(baseHP × difficultyMultiplier)
```

Where `difficultyMultiplier = Min(1 + renown × 0.002, 2.0)`.

### Base HP by Tier
| Tier | Base HP | At mult=1.0 | At mult=1.3 | At mult=2.0 |
|------|---------|-------------|-------------|-------------|
| 1    | 100     | 100         | 130         | 200         |
| 2    | 120     | 120         | 156         | 240         |
| 3    | 140     | 140         | 182         | 280         |

### Full Progression Reference

| Renown | Tier Label | Min Tier | Diff Mult | Tier-1 HP | Tier-2 HP | Tier-3 HP |
|--------|-----------|----------|-----------|-----------|-----------|-----------|
| 0      | Unknown   | 1        | 1.00      | 100       | 120       | 140       |
| 25     | Unknown   | 1        | 1.05      | 105       | 126       | 147       |
| 50     | Notorious | 2        | 1.10      | 110       | 132       | 154       |
| 100    | Notorious | 2        | 1.20      | 120       | 144       | 168       |
| 150    | Feared    | 3        | 1.30      | 130       | 156       | 182       |
| 250    | Feared    | 3        | 1.50      | 150       | 180       | 210       |
| 400    | Legendary | 3        | 1.80      | 180       | 216       | 252       |
| 500    | Legendary | 3        | 2.00      | 200       | 240       | 280       |

---

## Gold Reward Scaling

Kill rewards also scale with tier via `LootPopup.Configure(GoldRewardPerKill × tier)`:

| Tier | Kill Gold |
|------|-----------|
| 1    | 80g       |
| 2    | 160g      |
| 3    | 240g      |

Higher-tier fights pay better — incentivizes players to seek out dangerous areas.

---

## Design Intent

- **Early game** (renown 0–49): All enemies are tier 1 sloops with 100 HP. Learning curve is gentle.
- **Mid game** (renown 50–149): Tier 2 enemies appear everywhere. HP up to 144. Gold rewards up to 160g.
- **Late game** (renown 150+): Tier 3 enemies dominate. HP 182–252. Economy scales proportionally.
- **Port difficulty** is always respected — players approaching Fort Imperial (defense 4.0) get tier-2 enemies even at renown 0.

---

## Files Modified

- `Assets/Booty/Code/World/EnemySpawner.cs`
  - `SpawnEnemyNearPort()`: tier assignment now uses `Max(portTier, renownTier)`
  - `SpawnEnemyNearPort()`: HP now configured via `enemyHP.Configure(scaledHP)` with difficulty multiplier applied
