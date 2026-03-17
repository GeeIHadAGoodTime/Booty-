# Booty! Game Balance Document
**Sprint:** S3.6 — Game Balance Tuning
**Date:** 2026-03-16
**Philosophy:** Sid Meier's Pirates! — combat is tactical, economy is rewarding, difficulty is fair

---

## Combat Balance

### Goal
- Combat encounters should last **30–60 seconds**
- Each volley feels meaningful, not a spray of random fire
- Player vs. basic enemy sloop: tense but winnable

### Before → After

| Parameter | Before | After | Rationale |
|-----------|--------|-------|-----------|
| `BaseDamage` | 10 | **8** | Fewer one-shots; volleys matter more |
| `ProjectilesPerVolley` | 3 | **2** | Fewer shots = more tactical, less chaotic |
| `FireCooldown` | 2.5s | **2.0s** | Compensates for fewer projectiles; keeps engagement rhythm |
| `DefaultEnemyHP` | 80 | **100** | Basic enemies survive long enough to fight back |
| `DefaultPlayerHP` | 150 | 150 | Unchanged — player leniency is good UX |

### Combat Timing Math (Post-Tuning)

Assumptions: 60% hit rate (realistic arc firing), player vs. tier-1 enemy sloop:
- Damage per volley: 2 shots × 8 dmg × 0.6 hit rate = **~9.6 dmg/volley** effective
- Enemy HP: 100
- Volleys to kill: 100 / 9.6 ≈ **~10.4 volleys**
- Time at 2.0s cooldown: 10.4 × 2.0s = **~21 seconds minimum**
- With maneuvering, missed volleys, broadside positioning: **30–45 seconds** realistic ✓

---

## Economy Balance

### Goal
- Afford sloop→brig upgrade (1,500g) after **12–15 kills** or **2–3 port captures**
- Repair 1 fight's worth of damage costs less than the kill reward
- Port income supplements combat income but doesn't let players idle to victory

### Before → After

| Parameter | Before | After | Rationale |
|-----------|--------|-------|-----------|
| `GoldRewardPerKill` (CombatConfig) | 50 | **80** | Better pacing toward first upgrade |
| `baseCombatReward` (EconomySystem) | 50 | **80** | Matches CombatConfig constant |
| `combatRewardPerTier` | 25 | **35** | Tier2=115g, Tier3=150g (meaningful upgrade incentive) |
| `incomeIntervalSeconds` | 30s | **60s** | Port income every 60s — supplement, not primary |
| Starting gold | 200 | **300** | Better early experience, less grind immediately |
| `costPerHpPoint` (RepairShop) | 1.5 | **1.0** | More accessible repairs |
| `minimumRepairCost` | 5 | **15** | Tiny repairs are never free (discourages cheesy port-camping) |

### Economy Flow (Post-Tuning)

| Stage | Gold per Kill | Kills to Brig (1,500g) | Kills to Frigate (4,000g) |
|-------|--------------|------------------------|---------------------------|
| Early (tier 1) | 80g | ~15 kills | ~38 kills |
| Mid (tier 2) | 115g | ~10 kills | ~27 kills |
| Late (tier 3) | 150g | ~8 kills | ~20 kills |

Starting gold 300g + Port Haven income (50g/60s) = player can afford basic repairs immediately.

### Repair Cost Reference

| Damage Taken | Repair Cost |
|-------------|-------------|
| 0 HP missing | 0g |
| 10 HP missing (1 volley) | max(15g, 10×1.0) = **15g** |
| 30 HP missing (3 volleys) | max(15g, 30×1.0) = **30g** |
| 80 HP missing (full damage) | max(15g, 80×1.0) = **80g** |

One kill (80g) covers ~2-3 fights worth of partial repair damage ✓

---

## Files Modified

- `Assets/Booty/Code/Combat/CombatConfig.cs` — BaseDamage, FireCooldown, ProjectilesPerVolley, DefaultEnemyHP, GoldRewardPerKill
- `Assets/Booty/Code/Economy/EconomySystem.cs` — incomeIntervalSeconds, baseCombatReward, combatRewardPerTier, starting gold
- `Assets/Booty/Code/Economy/RepairShop.cs` — costPerHpPoint, minimumRepairCost
