# SEA — Math Reference
**Document 2 of 4** · Version 2.0 · September 2026

Every number and formula in the game. If a number is not here, it does not exist. Every table in this document was produced by running the formulas (§13 has the script that regenerates them).

---

## Contents
1. Conventions
2. Ship stats and caps
3. Firing: volley, reload, magazine
4. Ammo
5. Taking damage: facing, armor, HP
6. Healing: repair and kits
7. Enemies (derived from player numbers)
8. Skills, crew, abilities
9. Progression (Map Rank)
10. Economy
11. Ratings and Honor
12. Balance tests
13. Reference script and constants

---

## 1. Conventions

- Percentages are decimals in formulas (25% = 0.25).
- **Add-then-cap**: all bonuses to one stat, from every source, are added, then the sum is capped. Bonuses never multiply each other.
- **Base ship** at Map Rank N = the hull tier and cannon tier of that rank (§9.2), every cannon slot filled with that tier, Round Shot, no skills, crew, plates, or sails.
- Time in seconds unless written ms. Squares are the map grid unit.
- Damage rounds down once, at the end.
- Symbols: `P_DPS(N)`, `P_EHP(N)` = base ship sustained DPS and effective HP (sides) at rank N. `G(N)` = base gold drop on map N.

---

## 2. Ship stats and caps

### 2.1 The twelve stats
Volley Damage · Reload · Magazine · Max HP · Armor Front · Armor Sides · Armor Back · Speed · Turn Rate · Range · Repair Amount · Repair Channel

### 2.2 Per-stat caps
| Stat | Base from | Cap on total bonus | At cap |
|---|---|---|---|
| Volley Damage | Σ cannons | +0.25 | 1.25× |
| Reload | slowest cannon | −0.20 | 0.80× (floor 1.5 s) |
| Magazine | 3 | +2 | 5 |
| Max HP | hull | +0.25 | 1.25× |
| Armor per face | hull | +15 points | base + 0.15, absolute max 0.45 |
| Speed | hull + sails | +0.25 | 1.25× |
| Turn Rate | hull + sails | +0.25 | 1.25× |
| Range | cannons | +2 squares | |
| Repair Amount | 0.20 | +0.50 | 0.30 |
| Repair Channel | 3.0 s | −0.50 | 1.5 s |

### 2.3 Combat Power budget (the shared cap)
The four stats that decide a fight also share one budget, so a ship cannot reach the damage cap and the HP cap at the same time.
```
CombatPower = 100·damage_bonus + 100·reload_bonus + slot_bonus_pct + 100·hp_bonus + 1.4·max_face_armor_points
CombatPower ≤ 45
slot_bonus_pct = 100 × extra_slots / hull_slots
```
- Armor points cost 1.4 each because a point of armor is worth more than a point of HP.
- Every source counts: skills, crew, plates, sails, figurehead, temporary buffs. Skills can reach about 39–41 CP on their own (a full Cannons or Armor tree); a full legendary combat crew asks about 22. Nobody gets both, which is what makes crew and skills a real choice.
- Over 45, bonuses apply in this order and the rest is inactive: hull variant → plates → sails → crew → skills → buffs. The Ship window shows the inactive amount.
- Speed, Turn, Range, Magazine, and Repair are outside the budget.

**Result**: the highest fight score any legal ship can reach is **1.58×** base, at every tier (§12.2).

### 2.4 Hulls
| Tier | HP | Front | Sides | Back | Slots | Speed sq/s | Turn °/s | Cost gold |
|---|---|---|---|---|---|---|---|---|
| 1 | 1,600 | 0.15 | 0.08 | 0.03 | 8 | 2.4 | 60 | 0 |
| 2 | 4,800 | 0.18 | 0.10 | 0.04 | 14 | 2.3 | 52 | 20,000 |
| 3 | 10,500 | 0.22 | 0.12 | 0.05 | 20 | 2.2 | 45 | 120,000 |
| 4 | 20,000 | 0.26 | 0.14 | 0.06 | 26 | 2.0 | 38 | 500,000 |
| 5 | 36,000 | 0.30 | 0.16 | 0.08 | 32 | 1.8 | 32 | 2,000,000 |

Magazine 3 on every hull. Draft: T1–T3 can enter shallow water; T4–T5 cannot.
Variants (tiers 4–5): War slots +2, HP ×0.90 · Merchant cargo ×1.5, speed +0.05, slots −2 · Beast HP ×1.10, out-of-combat regen 0.01·MaxHP/s, slots −2. Variant bonuses count toward caps and budget.

### 2.5 Cannons
| Tier | Damage | Reload | Range | Cost each |
|---|---|---|---|---|
| 1 | 20 | 3.0 | 8 | 500 |
| 2 | 32 | 2.9 | 8 | 3,000 |
| 3 | 48 | 2.8 | 9 | 15,000 |
| 4 | 68 | 2.7 | 9 | 50,000 |
| 5 | 92 | 2.6 | 10 | 150,000 |

### 2.6 Sails and plates (bonuses; count toward caps and budget)
| Item | Speed | Turn | HP | Front | Sides | Back | Other |
|---|---|---|---|---|---|---|---|
| Reef Sails | 0 | +0.10 | | | | | |
| Storm Sails | +0.08 | 0 | | | | | storms and fog do not slow |
| Trade Sails | +0.06 | −0.05 | | | | | cargo +0.25 |
| Oak Plate | | | +0.05 | | | | |
| Ironwood Plate | | | +0.05 | +3 | | | |
| Obsidian Plate | | | | | +4 | | burn duration ×0.5 |
| Whalebone Plate | | | +0.05 | | | +5 | freeze duration ×0.5 |

### 2.7 Legendary edge
A legendary item (Knowledge §17) grants bonuses that sit **outside** the Combat Power budget, capped:
```
LegendaryCP ≤ 3      (e.g. damage +0.02 and reload −0.01; or HP +0.03)
Per-stat caps still apply.  One legendary equipped per ship.
```
The fight-score bound with a legendary is 1.582 × 1.03 ≈ **1.63** (§12.2). This is the only place money can buy any power, and it is 1–2 items per month in the whole world.

---

## 3. Firing

### 3.1 Volley Damage
```
VolleyDamage = floor( Σ cannon_damage_i × ammo_dmg_mult × (1 + min(0.25, damage_bonus)) )
```

### 3.2 Reload
```
Reload = max( 1.5 , max_i(cannon_reload_i) × ammo_reload_mult × (1 − min(0.20, reload_bonus)) )
```
The slowest equipped cannon sets the reload for the whole volley.

### 3.3 Magazine
```
Magazine = min(5, 3 + magazine_bonus)
```
- One volley reloads every `Reload` seconds, always, firing or not.
- Q fires one volley if at least one is ready. Minimum 1.0 s between presses.
- Full when leaving port. Refills to full after 15 s with no shot fired or taken.

### 3.4 Derived
```
SustainedDPS = VolleyDamage / Reload
Burst        = Magazine × VolleyDamage   fired over (Magazine − 1) seconds
```
Magazine changes burst only. Reload changes sustained DPS only. There is no firing arc and no broadside bonus; facing matters only when taking damage (§5).

### 3.5 Base ship values (all cannons of the tier, Round Shot)
| Tier | Volley | Reload | Sustained DPS | Burst (3 volleys) |
|---|---|---|---|---|
| 1 | 160 | 3.0 | 53.3 | 480 |
| 2 | 448 | 2.9 | 154.5 | 1,344 |
| 3 | 960 | 2.8 | 342.9 | 2,880 |
| 4 | 1,768 | 2.7 | 654.8 | 5,304 |
| 5 | 2,944 | 2.6 | 1,132.3 | 8,832 |

---

## 4. Ammo

| Ammo | dmg_mult | reload_mult | Gold / volley | Effect | Value | Duration | Range limit |
|---|---|---|---|---|---|---|---|
| Round | 1.00 | 1.00 | 10 | — | | | |
| Chain | 0.70 | 1.00 | 40 | target speed | −0.30 | 4 s | |
| Fire | 0.85 | 1.10 | 60 | burn; healing ×0.5 while burning | 0.006·MaxHP per s | 5 s | |
| Grape | 0.60 | 0.90 | 40 | target reload | +0.50 | 3 s | 4 squares |
| Frost | 0.70 | 1.10 | 60 | target turn | −0.50 | 4 s | |
| Blessed | 1.00 | 1.20 | 80 | vs ghosts | ×2.0 (×1.0 otherwise) | | |
| Heavy | 1.40 | 1.50 | 80 | — | | | |

**Sustained DPS relative to Round** (`dmg_mult / reload_mult`, plus burn for Fire):
| Chain | Fire (T1 → T5) | Grape | Frost | Blessed | Blessed vs ghost | Heavy |
|---|---|---|---|---|---|---|
| 0.70 | 1.05 → 1.11 | 0.67 | 0.64 | 0.83 | 1.67 | 0.93 (burst ×1.40) |

Fire per volley = `0.85·Volley + 0.03·MaxHP_target`, every `1.1·Reload` seconds. At T4: (1,503 + 600) / 2.97 = 708 DPS vs 655 Round = **1.08×**. Fire Shot is a little stronger than Round, costs 6× more, and halves healing. That is the intended trade.

Rules: the same effect does not stack (a new hit refreshes the timer); different effects stack. Ammo is free in arena.

---

## 5. Taking damage

### 5.1 Facing
```
θ = angle between the target's heading and the vector from target to shooter
Front if |θ| ≤ 45°,  Back if |θ| ≥ 135°,  else Sides
```

### 5.2 Damage taken
```
armor_face  = min(0.45, hull_face + min(15, armor_points_face) / 100)
DamageTaken = floor( VolleyDamage × (1 − armor_face) )
```
Against NPCs: `× 0` for an ammo the NPC is immune to; `× weakness_mult` for a weakness.

### 5.3 HP and effective HP
```
MaxHP     = floor( hull_hp × (1 + min(0.25, hp_bonus)) )
EHP(face) = MaxHP / (1 − armor_face)
```

### 5.4 Base ship EHP
| Tier | MaxHP | EHP front | EHP sides | EHP back |
|---|---|---|---|---|
| 1 | 1,600 | 1,882 | 1,739 | 1,649 |
| 2 | 4,800 | 5,854 | 5,333 | 5,000 |
| 3 | 10,500 | 13,462 | 11,932 | 11,053 |
| 4 | 20,000 | 27,027 | 23,256 | 21,277 |
| 5 | 36,000 | 51,429 | 42,857 | 39,130 |

Showing your back instead of your front to an equal ship shortens your life by 12–24%.

### 5.5 Ramming
```
Requires attacker speed ≥ 0.90 × its max speed
RamToTarget = 0.15 × attacker_MaxHP × (1 + ram_bonus)          ram_bonus ≤ 0.60
RamToSelf   = 0.05 × attacker_MaxHP × (1 − self_reduction)      self_reduction ≤ 0.50
Both ships stop 1.0 s. Ram cooldown 8 s.
```

### 5.6 Wind and currents (kept small on purpose)
```
θ_wind     = angle between the ship's heading and the direction the wind blows toward
WindMult   = 1 + 0.10 × cos(θ_wind)             +10% with the wind, −10% against, 0 across
Navigator  = headwind half only: 1 + 0.10 × cos(θ) × 0.7 when cos(θ) < 0
Wind Reader = headwind penalty removed: WindMult = max(1, WindMult)
Storm      = ×0.85 on top (Storm Sails skill ignores)
Current    = adds a fixed vector of at most 0.30 sq/s in marked sectors only
Speed      = min(1.25, base_speed × (1 + speed_bonus)) × WindMult × StormMult, then + current
Rule: the combined effect of wind, storm, and current never changes speed by more than ±25% of base.
```
Why ±10%: two equal ships, one running downwind and one chasing upwind, separate at `2 × 0.10 × speed`. Time for the runner to open a full cannon range:
| Tier | Speed | Relative gain | Time to open range |
|---|---|---|---|
| 1 | 2.4 | 0.48 sq/s | 17 s |
| 3 | 2.2 | 0.44 sq/s | 20 s |
| 4 | 2.0 | 0.40 sq/s | 22 s |
| 5 | 1.8 | 0.36 sq/s | 28 s |
That is half a fight or more: wind helps a runner but cannot decide a fight by itself. At the old ±20% the same escape took 8–14 s, which was too much. Wind changes every 3–5 minutes, so both sides get it in turn.

### 5.7 Boarding
Every ship carries **hands** (fighting sailors) and an **Arms Locker** that equips all of them.
```
Hands(hull)     = 10 / 20 / 30 / 40 / 50 by tier, +2 per crew member on board
Weapon (attack) = Cutlass 1.0 · Boarding Axe 1.2 · Pistol 1.5 · Musket 1.8 · Blunderbuss 2.2
Guard (defence) = None 1.0 · Leather 1.1 · Buff Coat 1.25 · Breastplate 1.4
Attack  A = Hands_A × Weapon_A × (0.6 + 0.4 × HP_frac_A) × (1 + board_bonus_A)
Defence D = Hands_D × Weapon_D × Guard_D × (0.4 + 0.6 × HP_frac_D) × (1 + board_bonus_D)
P(success)  = clamp( A / (A + D) , 0.05 , 0.90 )
LootMult    = clamp( A / D , 0.5 , 2.0 )
board_bonus = Master-at-Arms (+0.15 / +0.25 / +0.40) + Marauder (+0.25); Boarding Haul skill +0.15 per level on the haul
```
Against NPCs: `Hands_D = 10 × tier`, Weapon_D = 1.0, Guard by family (Ashlords 1.25, Ghosts 1.4, others 1.0).

| Case (same hull) | P(success) | Loot × |
|---|---|---|
| Equal weapons, defender at full HP | 0.50 | 1.00 |
| Equal weapons, defender at 30% HP | 0.63 | 1.72 |
| Musket vs Cutlass, defender full HP | 0.64 | 1.80 |
| Musket vs Cutlass, defender 30% HP | 0.76 | 2.00 |
| Cutlass vs Musket + Breastplate, full HP | 0.28 | 0.50 |
| Blunderbuss vs Cutlass, defender 30% HP | 0.79 | 2.00 |

Arms Locker prices (gold, per ship, whole locker): Boarding Axe 5,000 · Pistol 40,000 · Musket 250,000 · Blunderbuss 1,000,000 · Leather 5,000 · Buff Coat 40,000 · Breastplate 250,000. Weapons are gated by Map Rank 3 / 5 / 7 / 9; guards by 3 / 5 / 7. Boarding gear is outside the Combat Power budget because it does not change cannon combat.

**Outcome**
```
Success: attacker receives a Boarding Haul from the game: gold = 15 × G(map) × LootMult and one Elite-tier item roll (player target) or a bonus loot roll × LootMult (NPC). The victim does not lose gold or items;
         defender's cannons are silenced 3 s; defender loses 0.10 × MaxHP and 10% of its hands; defender is not sunk.
         attacker loses 5% of its hands (a fight always costs sailors).
Fail:    attacker loses 0.10 × MaxHP,
         loses gold = min( 25 × G(MapRank) , 0.05 × attacker_gold ),
         loses hands = round( Hands_A × 0.30 × (1 − P) )      (a long shot that fails kills more sailors: 27% of hands at P = 0.10, 15% at P = 0.50),
         and cannot board for 60 s.
Hands recover 1 per minute at sea and to full instantly in any port or guild fort. A ship with fewer than 50% of its hands cannot board.
Cooldowns: attacker 30 s after success, 60 s after a fail. A player can be boarded at most once per 5 min.
```
Fail-cost examples: Frigate at Map Rank 7, 100,000 gold on hand → loses 2,000 HP, 5,000 gold (25 × 503 = 12,575 capped by 5% = 5,000), and 6 of 40 hands at P = 0.50.

---

## 6. Healing

### 6.1 Repair (R)
```
RepairAmount  = min(0.30, 0.20 + repair_amount_bonus)
RepairChannel = max(1.5, 3.0 × (1 − min(0.50, repair_channel_bonus)))
Fatigue       = 0.6^n     n = heals (R or kit) completed in the last 60 s
BurnMult      = 0.5 if burning, else 1.0
Heal          = floor( MaxHP × RepairAmount × Fatigue × BurnMult )
Cooldown      = 15 s from the end of the channel (also after a cancel)
Cancel        = damage taken during the channel ≥ CancelThreshold × MaxHP, or a Fire Shot hit
CancelThreshold = 0.15 (0.25 with Steady Repair)
```
Cadence: channel 3 s + cooldown 15 s = one repair every 18 s → up to 4 in 60 s.

### 6.2 Repair Kit (gold item)
```
Heal      = floor( MaxHP × KitAmount × Fatigue × BurnMult )    instant, cannot be interrupted
KitAmount = 0.25 (0.30 with Kit Master)
Cooldown  = 45 s, separate from R.  Counts as a heal for Fatigue.
Cost      = 10 × G(MapRank) gold.  Stacks without limit.  Never sold for Diamonds.
```

### 6.3 Healing ceilings (60-second fight, best possible timing)
| Build | Sequence | Total heal (× MaxHP) |
|---|---|---|
| Base, R only | 0.20 × (1 + 0.6 + 0.36 + 0.216) | 0.435 |
| Base + 1 kit | 0.20 + 0.25·0.6 + 0.20·(0.36 + 0.216 + 0.130) | 0.491 |
| Max Repair tree, R only | 0.30 × 2.176 | 0.653 |
| Max Repair tree + 1 kit | 0.30 + 0.25·0.6 + 0.30·0.706 | 0.662 |

Max Repair tree adds about **0.23 × MaxHP** over base in a full minute. Real fights are shorter (§12.1) and repairs get cancelled, so this stays inside the fight-score margin.

---

## 7. Enemies

### 7.1 Definitions
For map N, the **base ship** of Map Rank N gives `P_EHP(N)` (sides) and `P_DPS(N)`. Every NPC is a multiple of those two numbers. Enemy files store only `tier` and `map`; the server computes HP and DPS at spawn.

| Tier | HP | DPS | Gold | Design intent |
|---|---|---|---|---|
| 1 Common | 0.50·P_EHP | 0.25·P_DPS | G(N) | Solo, ~17 s |
| 2 Veteran | 1.00·P_EHP | 0.40·P_DPS | 2.5·G(N) | Solo, ~35 s, one repair |
| 3 Elite | 2.20·P_EHP | 0.70·P_DPS | 8·G(N) | Two players ~40 s; solo is risky |
| 4 Named | 5.00·P_EHP | 0.90·P_DPS | 25·G(N) | Three players ~60 s |
| 5 Boss | 30·P_EHP × S(players) | 1.20·P_DPS split | 150·G(N) shared | Six players 12–20 min |
| 6 World Boss | 120·P_EHP(10) × S(players) | 1.50·P_DPS(10) | 400·G(10) shared | Twenty players 20–30 min |

```
S(p) = 1 + 0.35 × max(0, min(p, cap) − 3)     cap = 12 (boss), 30 (world boss)
```
NPC armor on every face: 0.10 (tiers 1–2), 0.15 (tier 3), 0.20 (tiers 4–6).

### 7.2 Computed values
| N | Tier | P_EHP | P_DPS | Common HP / DPS | Veteran HP | Elite HP / DPS | Named HP | Boss HP, 6 players |
|---|---|---|---|---|---|---|---|---|
| 1–2 | T1 | 1,739 | 53.3 | 870 / 13 | 1,739 | 3,826 / 37 | 8,696 | — |
| 3–4 | T2 | 5,333 | 154.5 | 2,667 / 39 | 5,333 | 11,733 / 108 | 26,667 | 328,000 (map 3) |
| 5–6 | T3 | 11,932 | 342.9 | 5,966 / 86 | 11,932 | 26,250 / 240 | 59,659 | 734,000 (map 5) |
| 7–8 | T4 | 23,256 | 654.8 | 11,628 / 164 | 23,256 | 51,163 / 458 | 116,279 | 1,430,000 (map 7) |
| 9–10 | T5 | 42,857 | 1,132.3 | 21,429 / 283 | 42,857 | 94,286 / 793 | 214,286 | 2,636,000 (map 9) · World 10,543,000 |

### 7.3 Kill times (base players, pure damage)
| Enemy | Players | Time | Notes |
|---|---|---|---|
| Common | 1 | 16–19 s | Deals ~12% of a player's EHP before dying |
| Veteran | 1 | 33–38 s | Deals ~40%; one repair needed |
| Elite | 2 | 36–42 s | Solo: 72–83 s under 0.70·P_DPS; needs 3+ repairs; doable for a Tank or Healer |
| Named | 3 | 54–63 s | |
| Boss | 6 | 5.6–6.5 min pure; 12–20 min with phases, adds, repairs | |
| World Boss | 20 | ≈ 21 min at 70% uptime | |

### 7.4 Boss counter and loot (damage share on every NPC)
```
Boss spawns when elites_sunk_in_biome ≥ 50 since the last boss (server-wide per biome). Lockout 3 h after a kill.
For EVERY NPC:
  share_i = (damage_i + 2·healing_i + 500·debuffs_i) / Σ_all        (party members' damage pools into the party, split equally)
  Gold_i  = floor(total_gold × share_i)
  Item rolls (Knowledge Appendix E): everyone with share_i ≥ 0.05.  Boss guaranteed roll: top 3 by share (top 5, world boss).
```

---

## 8. Skills, crew, abilities

### 8.1 Skill points and the three-tree rule
```
SkillPoints = rank_points(MapRank) + achievement_points (≤ 10)        max 90
Trees with points > 0 ≤ 3
Cost per level: Tier 1 = 1, Tier 2 = 2, Tier 3 = 3, Capstone = 5 (needs 30 points spent in that tree)
```
| Map Rank | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Points | 5 | 10 | 15 | 20 | 30 | 40 | 50 | 60 | 70 | 80 |

### 8.2 Trees
"CP" = Combat Power used at max level. "Cost" = points to max that skill.

**Cannons — damage** (full tree 52 points, 39 CP)
| Tier | Skill | Levels | Per level | At max | Cost | CP |
|---|---|---|---|---|---|---|
| 1 | Steady Hands | 5 | damage +0.02 | +0.10 | 5 | 10 |
| 1 | Powder Monkey | 5 | reload −0.01 | −0.05 | 5 | 5 |
| 2 | Master Gunner | 5 | damage +0.02 | +0.10 | 10 | 10 |
| 2 | Quick Load | 5 | reload −0.02 | −0.10 | 10 | 10 |
| 2 | Deep Magazine | 2 | magazine +1 | +2 | 4 | 0 |
| 2 | Long Nines | 2 | range +1 | +2 | 4 | 0 |
| 3 | Extra Gun | 1 | cannon slot +1 | +1 | 3 | 100/hull_slots (3.85 on a Frigate) |
| 3 | Chain Mastery | 1 | Chain slow −0.30 → −0.40 | | 3 | 0 |
| 3 | Burning Focus | 1 | burn 5 s → 7 s | | 3 | 0 |
| Cap | Devastation | 1 | ability | | 5 | 0 |
At max: damage +0.20, reload −0.15. The last +0.05 damage and −0.05 reload to the caps must come from crew or gear.

**Armor — tank** (full tree 68 points, 41 CP)
| Tier | Skill | Levels | Per level | At max | Cost | CP |
|---|---|---|---|---|---|---|
| 1 | Hull Bracing | 5 | HP +0.02 | +0.10 | 5 | 10 |
| 1 | Iron Flanks | 5 | sides +1 | +5 | 5 | 7 |
| 2 | Reinforced Hull | 5 | HP +0.02 | +0.10 | 10 | 10 |
| 2 | Iron Bow | 5 | front +2 | +10 | 10 | 14 if front is the highest face |
| 2 | Sternguard | 5 | back +2 | +10 | 10 | 14 if back is the highest face |
| 2 | Anchor Point | 1 | ability | | 2 | 0 |
| 3 | Iron Flanks II | 5 | sides +2 | +10 | 15 | 14 |
| 3 | Reinforced Prow | 1 | ram +0.60, self −0.50 | | 3 | 0 |
| 3 | Thick Skin | 1 | burn and freeze duration ×0.5 | | 3 | 0 |
| Cap | Bastion | 1 | ability | | 5 | 0 |
At max: HP +0.20, sides +15. The last +0.05 HP must come from crew or plates.

**Sails — speed and escape** (full tree 75 points; no CP)
| Tier | Skill | Levels | Per level | At max | Cost |
|---|---|---|---|---|---|
| 1 | Trim Sails | 5 | speed +0.02 | +0.10 | 5 |
| 1 | Tacking | 5 | turn +0.02 | +0.10 | 5 |
| 2 | Full Canvas | 5 | speed +0.02 | +0.10 | 10 |
| 2 | Sharp Rudder | 5 | turn +0.02 | +0.10 | 10 |
| 2 | Wind Reader | 1 | headwind penalty 0 | | 2 |
| 2 | Evasive Roll | 1 | ability | | 2 |
| 3 | Racing Hull | 5 | speed +0.01 | +0.05 | 15 |
| 3 | Fine Helm | 5 | turn +0.01 | +0.05 | 15 |
| 3 | Ghost Wake | 1 | ability | | 3 |
| 3 | Storm Sails | 1 | storms and fog do not slow | | 3 |
| Cap | Outrun | 1 | ability | | 5 |

**Repair — healing and support** (full tree 60 points; no CP)
| Tier | Skill | Levels | Per level | At max | Cost |
|---|---|---|---|---|---|
| 1 | Damage Control | 5 | repair amount +0.04 | +0.20 | 5 |
| 1 | Fast Hands | 5 | repair channel −0.04 | −0.20 | 5 |
| 2 | Master Carpenter | 5 | repair amount +0.04 | +0.20 | 10 |
| 2 | Calm Under Fire | 5 | repair channel −0.04 | −0.20 | 10 |
| 2 | Field Surgeon | 1 | ability | | 2 |
| 2 | Cleanse | 1 | ability | | 2 |
| 3 | Shipwright's Touch | 5 | repair amount +0.02 | +0.10 | 15 |
| 3 | Steady Repair | 1 | cancel threshold 0.15 → 0.25 | | 3 |
| 3 | Kit Master | 1 | kit heal 0.25 → 0.30 | | 3 |
| Cap | Tide Turner | 1 | ability | | 5 |

**Plunder — gold, loot, boarding** (full tree 60 points; CP only via Captain's Call)
| Tier | Skill | Levels | Per level | At max | Cost |
|---|---|---|---|---|---|
| 1 | Plunder | 5 | gold +0.04 | +0.20 | 5 |
| 1 | Salvager | 5 | materials +0.04 | +0.20 | 5 |
| 2 | Smuggler | 5 | cargo +0.06 | +0.30 | 10 |
| 2 | Boarding Party | 1 | ability | | 2 |
| 2 | Quick Hands | 2 | boarding channel −0.5 s | −1.0 s | 4 |
| 2 | Captain's Call | 1 | ability | | 2 |
| 3 | Pirate's Luck | 3 | blueprint chance ×1.15 | ×1.52 | 9 |
| 3 | Boarding Haul | 3 | boarding haul +0.15 | +0.45 | 9 |
| 3 | Thrifty Gunner | 3 | ammo cost −0.05 | −0.15 | 9 |
| Cap | Marauder | 1 | boarding score +0.25; haul ×2 | | 5 |

**What 90 points buy**: one full tree (52–75) plus the first tier or two of a second; or tiers 1–2 of three trees. No player can fill two trees.

**Skills alone never reach the caps.** Full Cannons stops at damage +0.20 and reload −0.15; full Armor at HP +0.20. The rest must come from crew and gear, which is why crew matters (§8.3).

### 8.3 Crew (bonuses count toward caps and budget)
Value at level 20 by rarity. Level scaling: `value × (0.6 + 0.4 × (level − 1) / 19)`; level 1 gives 60%.

| Role | Stat | Common | Rare | Legendary | CP at legendary |
|---|---|---|---|---|---|
| Gunner | damage | +0.03 | +0.05 | +0.08 | 8 |
| Powder Master | reload | −0.03 | −0.05 | −0.08 | 8 |
| Carpenter | HP | +0.03 | +0.05 | +0.08 | 8 |
| Armorer | every face | +2 | +3 | +5 | 7 |
| Helmsman | speed | +0.03 | +0.05 | +0.08 | 0 |
| Navigator | turn; headwind ×0.7 | +0.03 | +0.05 | +0.08 | 0 |
| Boatswain | repair amount | +0.06 | +0.10 | +0.15 | 0 |
| Surgeon | kit heal; one free injury per day | +0.02 | +0.03 | +0.05 | 0 |
| Quartermaster | gold | +0.06 | +0.10 | +0.15 | 0 |
| Lookout | vision; reveals stealth within 2 squares | +0.10 | +0.15 | +0.20 | 0 |
| Cook | damage and HP, 30 min after port | +0.01 each | +0.02 | +0.03 | 6 |
| Master-at-Arms | ram damage; boarding channel | +0.15, −0.25 s | +0.25, −0.5 s | +0.40, −1.0 s | 0 |

Two crew of the same role do not stack; the higher one applies. Legendary crew also carry one ability (Knowledge §9.3).

**Crew Combat Power examples** (all legendary, level 20): Gunboat (Gunner, Powder Master, Cook) = 22 CP · Wall (Carpenter, Armorer, Cook) = 21 CP. With a full Cannons tree (39 CP) a Gunboat crew asks 61 and gets 45: the player must choose. With Sails 45 + Repair 30 (0 CP) the same crew fits with 23 CP to spare for plates and sails. That is the composition system.

### 8.4 Abilities
Every ability: `cooldown ≥ 4 × duration`. The server refuses to start with any row that breaks this.
| Ability | Source | Effect | Duration | Cooldown | Ratio |
|---|---|---|---|---|---|
| Devastation | Cannons cap | Next 2 volleys ignore armor | 8 s | 60 s | 7.5 |
| Bastion | Armor cap | All faces +25 points; cannot fire | 6 s | 60 s | 10 |
| Anchor Point | Armor | Immune to slow, pull, root | 5 s | 45 s | 9 |
| Outrun | Sails cap | Speed +0.50 (ignores cap); removes slows | 5 s | 60 s | 12 |
| Evasive Roll | Sails | All shots at you miss | 1.5 s | 20 s | 13 |
| Ghost Wake | Sails | Invisible while not firing or repairing | 6 s | 60 s | 10 |
| Tide Turner | Repair cap | Party heals 0.05·MaxHP per second | 5 s | 90 s | 18 |
| Field Surgeon | Repair | Heal one ally 0.20·MaxHP | 1 s | 30 s | 30 |
| Cleanse | Repair | Remove burn, slow, freeze from self or ally | 1 s | 25 s | 25 |
| Captain's Call | Plunder | Party damage +0.10 (inside cap and budget) | 15 s | 60 s | 4 |
| Boarding Party | Plunder | Target cannot fire | 3 s | 45 s | 15 |
| Kraken | figurehead | Root target | 2 s | 45 s | 22 |
| Serpent | figurehead | Ram applies burn (0.006/s) | 5 s | 30 s | 6 |
| Phoenix | figurehead | Revive at 0.20·MaxHP | 1 s | 1,800 s | — |
| Frost Wyrm | figurehead | Freeze within 3 squares (turn −0.50, speed −0.30) | 2 s | 45 s | 22 |
| Gilded Lady | figurehead | Party gold +0.25 | passive | — | — |

---

## 9. Progression

### 9.1 Unlocking the next map
```
Map N+1 unlocks when, on map N:  story_missions(N) complete
                              AND took part in ≥ 1 kill of unlock_npc(N)  (any damage share > 0)
                              AND chart_cost(N+1) paid
```

### 9.2 Rank table
| Map Rank | Hull tier | Cannon tier | Chart cost | Skill points | Cumulative hours (focused) |
|---|---|---|---|---|---|
| 1 | 1 | 1 | — | 5 | 0 |
| 2 | 1 | 1 | 1,000 | 10 | 1 |
| 3 | 2 | 2 | 5,000 | 15 | 3 |
| 4 | 2 | 2 | 15,000 | 20 | 5 |
| 5 | 3 | 3 | 40,000 | 30 | 8 |
| 6 | 3 | 3 | 100,000 | 40 | 11 |
| 7 | 4 | 4 | 200,000 | 50 | 15 |
| 8 | 4 | 4 | 400,000 | 60 | 19 |
| 9 | 5 | 5 | 700,000 | 70 | 24 |
| 10 | 5 | 5 | 1,000,000 | 80 | 30 |
| Max gear | 5 | 5 | — | 90 | 40 |

### 9.3 Attack permission
```
A may attack B  ⇔  |MapRank(A) − MapRank(B)| ≤ 2  ∧  both flagged  ∧  neither protected
Objective on map N may be taken  ⇔  MapRank ≤ N + 2
Guards on maps 1–3 sink an attacker  ⇔  MapRank(attacker) ≥ N + 3 and it fires on a player
```

### 9.4 Progression schedule (a focused player)
| Map Rank | Hours | Hull | Cannons | Points | Typical spend | Gold earned so far |
|---|---|---|---|---|---|---|
| 1 | 0 | T1 | 8 × T1 (mission reward) | 5 | Cannons T1 | 0 |
| 2 | 1 | T1 | 8 × T1 | 10 | Cannons T1 full | 8,000 |
| 3 | 3 | T2 | 14 × T2 | 15 | second tree T1 | 60,000 |
| 4 | 5 | T2 | 14 × T2 | 20 | second tree T1 full | 120,000 |
| 5 | 8 | T3 | 20 × T3 (crafted) | 30 | first tree T2 | 400,000 |
| 6 | 11 | T3 | 20 × T3 | 40 | first tree T2 full | 700,000 |
| 7 | 15 | T4 | 26 × T4 | 50 | second tree T2 | 1,900,000 |
| 8 | 19 | T4 | 26 × T4 | 60 | first tree T3 | 3,300,000 |
| 9 | 24 | T5 | 32 × T5 | 70 | first capstone | 6,500,000 |
| 10 | 30 | T5 | 32 × T5 | 80 | second tree T3, third tree T1 | 10,000,000 |
| Max gear | 40 | T5 + plates + crew | 32 × T5 + Extra Arms | 90 | done | 13,500,000 |

Skill points come only from Map Rank and achievements. Gear comes from gold and blueprints. Neither is sold for Diamonds.

### 9.5 Achievements (10 skill points)
First boss kill · first arena win · Map Rank 5 · Map Rank 10 · 100 PvP kills · Captain rank · a full week of dailies · first island war · Guild Arena Gold · all ten materials collected.

---

## 10. Economy

### 10.1 Gold per enemy
```
G(N) = floor( 30 × 1.6^(N−1) )
```
| N | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| G(N) | 30 | 48 | 76 | 122 | 196 | 314 | 503 | 805 | 1,288 | 2,061 |

Multipliers: Veteran ×2.5 · Elite ×8 · Named ×25 · Boss ×150 shared · Hot Map ×1.5. Gold bonuses (Plunder, Quartermaster, Gilded Lady) add to one **gold bonus** capped at +0.50.

### 10.2 Price list
| Item | Gold |
|---|---|
| Hulls T1–T5 | 0 · 20,000 · 120,000 · 500,000 · 2,000,000 |
| Cannons T1–T5, each | 500 · 3,000 · 15,000 · 50,000 · 150,000 |
| Full cannon set T1–T5 | 4,000 · 42,000 · 300,000 · 1,300,000 · 4,800,000 |
| Ammo per volley | Round 10 · Chain 40 · Grape 40 · Fire 60 · Frost 60 · Blessed 80 · Heavy 80 |
| Charts | §9.2 (total 2,461,000) |
| Repair in port, full | 0.02 × hull cost (T1 free) |
| Repair Kit | 10 × G(MapRank); stacks without limit |
| Rally Beacon | 50 × G(MapRank); lasts 10 min |
| Harbor Jump | 20 × G(destination map); 5 min cooldown |
| Common crew hire | 5,000 × hull tier of the port's map |
| Crew injury heal | 0.10 × hull cost |
| Skill reset (one tree) | 10,000 × MapRank |
| Guild creation | 50,000 |
| Dock / bank | 5 hulls / 60 gear items free; +1 hull slot 400 Diamonds, +20 bank items 400 Diamonds |
| Island toll | 0–0.05 of NPC gold on that map, set by the owner |

**Total cost of the full progression** (hulls + cannon sets + charts): 2,640,000 + 6,446,000 + 2,461,000 = **11.5 M**. With ammo, kits, crew, and repairs the schedule's 13.5 M by max gear leaves about 2 M of margin.

### 10.3 Earning rate
About 100 Common kills per hour (17 s fight + 19 s travel); with Veterans and Elites mixed in, income ≈ `180 × G(N)` per hour from NPCs alone.

| N | Gold / hour | Next hull + set | Hours, grind only |
|---|---|---|---|
| 3 | 13,700 | T2: 62,000 | 4.5 |
| 5 | 35,300 | T3: 420,000 | 11.9 |
| 7 | 90,500 | T4: 1,800,000 | 19.9 |
| 9 | 231,800 | T5: 6,800,000 | 29.3 |

Grind-only total ≈ 66 h. Missions, objectives, boarding hauls, boss shares, and the Hot Map cut that to about the 40 h target. If real players land outside 35–50 h, tune `G(N)` first.

### 10.4 Sinking
| Map | Gold | Cargo lost | Crew injury |
|---|---|---|---|
| 1–3 | port repair only | 0 | no |
| 4, 6, 8 | port repair | 0.20 | no |
| 5, 7, 9 (boss maps) | port repair | 0.50 | one |
| 10 | port repair | 1.00 | one |
Gear, skills, crew, and Map Rank are never lost. Lost cargo **disappears** (a gold sink); nothing drops for the killer. Sinking a player pays Honor, Combat Rating, and mission progress only.

### 10.5 Respawn and regroup
| Where | Cost | Delay |
|---|---|---|
| Nearest port | port repair (T1 free) | 8 s |
| Guild fort on the same map | free | 8 s |
| Party Rally Beacon | free, full HP | 20 s |
| Island war staging beacon | free, full HP | 8 s |
| Arena / Guild Arena | free | next game |

### 10.6 Daily NPC gold cap
After 8 hours of NPC-gold income in a day, NPC gold ×0.5 until the 06:00 reset.

### 10.7 Diamonds and monetization
**500 Diamonds ≈ 4.99 USD.** Packs: 500 / 1,100 / 2,400 / 5,200 / 11,000.

| Category | Item | Diamonds |
|---|---|---|
| Ship looks | Ship skin common / rare / legendary (same silhouette) | 800 / 1,500 / 2,500 |
| Ship looks | Sail pattern · cannon fire color · wake effect · figurehead skin | 300 · 400 · 600 · 500 |
| Identity | Flag design · name color · portrait frame · cosmetic title · companion | 300 · 200 · 200 · 300 · 900 |
| Social | Victory animation · ping sound pack | 700 · 200 |
| Guild | Guild flag effect · fort banner skin | 2,000 · 1,500 |
| Convenience | Loadout slot (max +3) · bank tab · name change · skill reset | 500 · 400 · 300 · 200 |
| Season | Sea Pass: 40 cosmetic tiers, +2 loadout slots, one free reset per week | 1,000 |
| Market | Anonymous order book: sell orders matched to buyers at best price; price band ±0.20 of the 7-day volume-weighted average; 10% tax on the seller; orders expire in 7 days | |

**Never for Diamonds**: hulls, cannons, ammo, plates, stat sails, crew, Repair Kits, Beacons, Harbor Jumps, skill points, Map Rank, Honor, Combat Rating, Team Rating, boss or event access, extra missions.

Rough revenue per 1,000 monthly active players: 5% Sea Pass (≈500 USD) + 3% one cosmetic (≈400 USD) + 1% heavy buyers (≈500 USD) ≈ **1.4 USD per player per month**.

### 10.8 Islands: towers and Garrison Supply
**Towers** (3 per island, fixed):
| Island | Map | HP per tower | Tower DPS (= map base DPS) | Siege time, 20 attackers at 50% uptime |
|---|---|---|---|---|
| Saltwind | 1/3 | 500,000 | 155 | 7.3 min |
| Cinder | 3/1 | 1,000,000 | 343 | 7.6 min |
| Glacier | 4/1 | 1,800,000 | 655 | 7.9 min |
Outside the war window towers have 3× HP and the flag cannot be taken. Towers regenerate only from Garrison Supply (below) and defender repairs (5 s channel, 10% of tower HP, cancelled by damage). Tower range 6 squares; they fire only at flagged enemies in combat with the fort or its owners.

**Garrison Supply** (0–100 per island):
```
Drain            = 10 per day (applied hourly: 10/24)
Turn in material = +1 per 5 units of the map's material at the fort (max +20/day per island)
Elite sunk       = +1 (Named +5) by an owner member on the map (max +20/day)
Garrison mission = +5 per member per day (3 dailies with "Garrison" tag; max +30/day)
Convoy escorted  = +10 (max +10/day)
Capture / defence= set to 60 on capture; +20 on a successful defence
Effects:
  tower_max_hp   = base × (0.5 + 0.5 × Supply/100)
  tower_regen    = 0.5% of max HP per minute while Supply ≥ 60; 0 below
  toll & materials & daily Honor  × (Supply/100)
  Supply < 30    → "weak garrison" flag on the map; war notice 12 h instead of 24 h
  Supply = 0 for 3 consecutive days → island turns neutral (towers 50%, anyone can take the flag, no declaration needed)
```
Pace: a guild of 30 that turns in materials, sinks Elites, and does Garrison dailies makes +40 to +80 a day against −10 and stays near 100; a guild that stops playing is neutral in 13 days.

### 10.9 Time bands
```
Bands: 00:00–04:00, 08:00–12:00, 16:00–20:00 UTC
Guild home band: chosen at creation; changeable once per 30 days.
Island war window: 2 h inside the DEFENDER's home band; declaration ≥ 24 h before (12 h if weak garrison).
Guild Arena queue: open in all three bands; a team's 5 matches per week can be played in any band.
Events: Ghost Tide, Convoy, Kraken Rising rotate through the bands day by day so each band sees each event at least twice a week.
```

---

## 11. Ratings and Honor

### 11.1 Combat Rating (Elo)
```
E_A   = 1 / (1 + 10^((CR_B − CR_A)/400))
ΔCR_A = round( K × (result_A − E_A) )        win 1, loss 0
K = 30 arena, 20 open sea.  Start 1000.  Floor 0.
Decay: CR ← CR × 0.99 per day without a PvP result, only while CR > 1500.
Season end: CR ← 1000 + 0.5 × (CR − 1000).
```
Typical: arena +15…+30 / −10…−25; open sea +5…+20 / −5…−15.

**Open-sea kill counts only if all hold**
1 victim flagged · 2 |ΔMapRank| ≤ 2 and |ΔCR| ≤ 300 · 3 victim fired ≥ 3 volleys · 4 fight ≥ 15 s · 5 victim HP ≥ 0.60·MaxHP at fight start · 6 no kill of this victim by this killer in 24 h · 7 not same guild, party, or friends, and not same guild in the last 7 days · 8 no shared IP in 30 days · 9 killer ≤ 3 counted kills this hour and ≤ 10 today · 10 victim account ≥ 3 days and ≥ 2 h played.
A fight starts at the first volley between two players and ends 20 s after the last hit.

### 11.2 Ranks
| Deckhand | Mate | Lieutenant | Captain | Commodore | Admiral | Sea Lord |
|---|---|---|---|---|---|---|
| 0–999 | 1000–1199 | 1200–1399 | 1400–1599 | 1600–1799 | 1800–1999 | ≥ 2000 and top 10 |

### 11.3 Team Rating (Guild Arena)
Same Elo, `K = 40`, one result per best-of-3, start 1000. Players get +10 CR on a win and −5 on a loss (flat). A new team with ≥ 3 members of a disbanded team inherits `max(1000, old TR)`.
Tiebreak at 360 s: higher Σ damage to enemy ships (healing not counted).

### 11.4 Honor
| Source | Honor |
|---|---|
| Duel win | 5 (first 10 duel wins per day) |
| Open-sea kill (rule-passed) | 3 |
| Arena match | 10 loss / 25 win |
| Guild Arena match | Bronze 20 · Silver 30 · Gold 40 · Platinum 55 · Diamond 75; ×2 on a win; 0 against a same-guild or allied team |
| Island war kill | 5 |
| Island capture or defence | 50 per participant |
| Island held, per member, per full day | 5 |
| Objective taken | 3 |
| Player bounty claimed | min(50, bounty_gold / 1,000) |
| Daily PvP mission | 15 |

### 11.5 Honor integrity numbers
```
Related window after leaving a guild or alliance     14 days
Guild join probation (no guild Honor)                 72 h  (7 days for a guild hopper: ≥ 3 guilds in 30 days)
Related guilds: member overlap in 30 days             > 0.20
Same-opponent diminishing returns per day             100% → 50% → 0%
Reciprocity list: paid results between a pair in 14 d ≥ 6  AND  (alternation ≥ 0.60  OR  share of either player's results ≥ 0.40)
Guild ring: ≥ 3 guilds, ≥ 0.50 of their PvP results against each other in 30 d, alternating winners
Contested war: losing side ≥ 5 members ≥ 5 min on map  AND  (tower HP destroyed ≥ 0.25 OR attackers sunk ≥ 0.25)
Daily Honor caps: duels 10 wins · arena 30 matches · guild arena 15 · war kills 30 per war · objectives 10 · bounties 5
Duel pays only if: length ≥ 30 s, opponent damage ≥ 0.15 × your MaxHP, no concede in first 30 s, opponent ≥ 2 days and ≥ 1 h played
Loss pays (arena) only if damage dealt ≥ 0.10 × enemy HP
```
Maximum honest Honor per day (one player): 50 duels + 750 arena (30 wins) + 2,250 guild arena (15 Diamond wins) + 150 war kills + 50 capture + 5 hold + 30 objectives + 250 bounties + 15 mission ≈ 3,550. Honor shop prices are set against this: a seasonal skin costs 20,000–40,000 Honor, about 2–4 weeks of strong play.

### 11.6 Guild Renown and levels
**Renown sources**
| Source | Renown |
|---|---|
| Weekly guild mission | 300 (easy) · 600 (medium) · 1,200 (hard); 5 posted per week |
| Member daily mission completed | 5 |
| Member Honor (rule-passed only) | 1 per 10 Honor |
| Island capture / successful defence (contested) | 2,000 / 1,500 |
| Island held, per full day | 300 |
| Guild Arena match win | 100 × division (Bronze 1 … Diamond 5); 0 against a same-guild or allied team |
| Guild Arena promotion | 1,000 |
| Boss kill with ≥ 3 members in top shares | 300 (world boss 1,000) |
| Season bonus | 10 × average CR of the top 20 members (e.g. average 1,500 → 15,000) |
| Per-member daily cap (dailies + Honor) | 60 |

**Level thresholds** (cumulative): `Renown(L) = 2,000 × L × (L + 1) / 2` → L2 6,000 · L5 30,000 · L10 110,000 · L15 240,000 · L20 420,000.
Member slots: `20 + 4 × (L − 1)` → 20 … 96, and 100 at L20. Alliance size: 2 (L1), 5 (L5), 10 (L10), 14 (L15), 20 (L20).

**Pace check**: an active guild of 30 (each doing 3 dailies and earning 100 Honor a day) makes 30 × (15 + 10) = 750 Renown a day from members alone (under the 60 cap each), plus missions (~2,000 a week), plus wars and arena. About 8,000–12,000 a week → Level 10 in about 3 months, Level 20 in about a year. A guild that holds an island and wins Diamond arena matches roughly doubles that.

**Upkeep**: perks active this week only if last week's Renown ≥ `200 × L`.

**Weekly bank payout**: `member_share = payout_gold × contribution_member / Σ contribution_(members past probation)`.

### 11.7 Wanted
```
Wanted += 1 per kill of a lower-rank victim (within the ±2 window) who did not fire first
Wanted −= 1 per 6 h without such a kill
Wanted ≥ 3: shown on every map; guards target; automatic bounty 5,000 × Wanted
```

---

## 12. Balance tests

These run in the build. A failing test blocks deploy.

### 12.1 Fight length — base vs base, sides, Round Shot
```
TTK       = P_EHP / P_DPS                                   must be in [32, 38] s
TTK_2rep  = TTK + 0.32 × MaxHP / (1 − sides) / P_DPS        must be in [42, 50] s
TTK_kit   = TTK_2rep + 0.25 × 0.36 × MaxHP / (1 − sides) / P_DPS
```
| Tier | TTK | + 2 repairs (0.20 + 0.12) | + 1 kit (0.25 × 0.36) |
|---|---|---|---|
| 1 | 32.6 s | 43.0 s | 46.0 s |
| 2 | 34.5 s | 45.6 s | 48.7 s |
| 3 | 34.8 s | 45.9 s | 49.1 s |
| 4 | 35.5 s | 46.9 s | 50.1 s |
| 5 | 37.8 s | 50.0 s | 53.4 s |

A clean burst fight ends near 35 s. A fight with repairs near 45–50 s. T5 with a kit reaches 53 s, accepted because T5 is endgame and both players carry kits.

### 12.2 Fight score — the anti-pay-to-win test
```
FightScore = (DPS × EHP_sides) / (P_DPS × P_EHP)
Must be ≤ 1.60 for every allocation that satisfies §2.2 and §2.3, and ≤ 1.63 with one legendary (§2.7).
```
Exhaustive search over damage 0–25, reload 0–20, HP 0–25, sides 0–15 with `d + r + h + 1.4a ≤ 45`:
| Tier | Max score | At |
|---|---|---|
| 1–4 | 1.582 | damage 12, reload 20, HP 13, armor 0 |
| 5 | 1.583 | same |
Named cases: damage 25 + reload 20 → 1.5625 · reload 20 + HP 25 → 1.5625 · reload 20 + sides 15 + HP 4 → 1.575. Healing adds ≤ 0.23·MaxHP per minute over base (§6.3), inside the margin because it can be cancelled or out-burst.

### 12.3 Ability ratio
`cooldown ≥ 4 × duration` for every ability. Lowest in the game: Captain's Call at exactly 4.

### 12.4 Ammo
No ammo may exceed `1.20 × Round` sustained DPS against a base ship, effect included. Highest: Fire at T5 = 1.11. Pass.

### 12.5 NPC solo safety
A base player must sink a Common in ≤ 20 s (max 18.9 s, pass) and must survive a Common for 60 s while repairing on cooldown: `0.25·P_DPS × 60 < P_EHP + 0.435·MaxHP`. T4: 9,822 < 31,956. Pass at every tier.

### 12.6 Sea effects
Combined wind + storm + current must never change speed by more than ±25% of base. Time for an equal runner to open full range downwind must be ≥ 15 s at every tier (§5.6: 17–28 s, pass).

### 12.7 Tree mixing and crew
`count(trees with points > 0) ≤ 3` and `Σ level costs ≤ SkillPoints`. Two crew of the same role never stack. The server rejects any assignment that fails. Skills-only maximum CP = 41 (full Armor); crew-only maximum = 22; both together are clamped at 45, so the fight-score maximum of 1.582 (§12.2) holds regardless of source.

### 12.8 Economy drift
Full progression cost (§10.2) must be ≤ 0.85 × schedule gold at max gear (11.5 M ≤ 11.5 M, pass at the limit). Grind-only hours (§10.3) must be ≤ 1.7 × the 40 h target (66 h, pass). Re-run when `G(N)`, prices, or drops change.

---

## 13. Reference script and constants

### 13.1 Script
The tables above are produced by this script. Run it after any change and paste the output.
```python
import math
hull_hp=[1600,4800,10500,20000,36000]; sides=[.08,.10,.12,.14,.16]
slots=[8,14,20,26,32]; cdmg=[20,32,48,68,92]; crel=[3.0,2.9,2.8,2.7,2.6]
G=[math.floor(30*1.6**(n-1)) for n in range(1,11)]
for t in range(5):
    v=slots[t]*cdmg[t]; dps=v/crel[t]; ehp=hull_hp[t]/(1-sides[t])
    ttk=ehp/dps; rep=0.32*hull_hp[t]/(1-sides[t])/dps; kit=0.25*0.36*hull_hp[t]/(1-sides[t])/dps
    print(f"T{t+1} volley={v} dps={dps:.1f} ehp={ehp:.0f} ttk={ttk:.1f} +2rep={ttk+rep:.1f} +kit={ttk+rep+kit:.1f}")
    best=max(((1+d/100)/(1-r/100)*(1+h/100)*((1-sides[t])/(1-sides[t]-a/100)),d,r,h,a)
             for d in range(26) for r in range(21) for h in range(26) for a in range(16) if d+r+h+1.4*a<=45.0001)
    print("  max fight score", best)
```

### 13.2 Constants
| Constant | Value | § |
|---|---|---|
| CAP_DAMAGE / CAP_RELOAD | 0.25 / 0.20 | 2.2 |
| CAP_MAGAZINE / CAP_HP | 2 / 0.25 | 2.2 |
| CAP_ARMOR_POINTS / ARMOR_ABS_MAX | 15 / 0.45 | 2.2 |
| CAP_SPEED / CAP_TURN / CAP_RANGE | 0.25 / 0.25 / 2 | 2.2 |
| CAP_REPAIR_AMOUNT / CAP_REPAIR_CHANNEL | 0.50 / 0.50 | 2.2 |
| CP_BUDGET / CP_ARMOR_WEIGHT | 45 / 1.4 | 2.3 |
| RELOAD_FLOOR / FIRE_MIN_INTERVAL / MAGAZINE_REFILL_IDLE | 1.5 s / 1.0 s / 15 s | 3 |
| BURN_PER_SEC / BURN_DURATION / BURN_HEAL_MULT | 0.006 / 5 s / 0.5 | 4 |
| RAM_TO_TARGET / RAM_TO_SELF / RAM_MIN_SPEED / RAM_COOLDOWN | 0.15 / 0.05 / 0.90 / 8 s | 5.5 |
| REPAIR_BASE / CHANNEL / COOLDOWN | 0.20 / 3.0 s / 15 s | 6.1 |
| REPAIR_FATIGUE / FATIGUE_WINDOW / CANCEL_THRESHOLD | 0.6 / 60 s / 0.15 | 6.1 |
| KIT_HEAL / KIT_COOLDOWN | 0.25 / 45 s | 6.2 |
| STACK_LIMIT (all consumables, materials) | none (uint64) | 10.2 |
| NPC_HP_MULT T1–T6 | 0.5 / 1.0 / 2.2 / 5 / 30 / 120 | 7.1 |
| NPC_DPS_MULT T1–T6 | 0.25 / 0.40 / 0.70 / 0.90 / 1.2 / 1.5 | 7.1 |
| BOSS_SCALE_PER_PLAYER / BOSS_CAP / WORLD_CAP | 0.35 / 12 / 30 | 7.1 |
| BOSS_COUNTER / BOSS_LOCKOUT | 50 / 3 h | 7.4 |
| MAX_TREES / SKILL_COST T1 T2 T3 Cap / CAPSTONE_REQ | 3 / 1 2 3 5 / 30 | 8.1 |
| ABILITY_MIN_RATIO | 4 | 8.4 |
| RANK_WINDOW | 2 | 9.3 |
| G_BASE / G_GROWTH | 30 / 1.6 | 10.1 |
| GOLD_MULT Vet Elite Named Boss Hot | 2.5 / 8 / 25 / 150 / 1.5 | 10.1 |
| CAP_GOLD_BONUS | 0.50 | 10.1 |
| RESPAWN_SECS / BEACON_SECS / SPAWN_SHIELD | 8 / 20 / 10 | 10.5 |
| DAILY_CAP_HOURS / DAILY_CAP_MULT | 8 / 0.5 | 10.6 |
| MARKET_TAX | 0.10 | 10.7 |
| ELO_K_ARENA / OPEN / TEAM | 30 / 20 / 40 | 11 |
| CR_START / CR_DECAY / CR_DECAY_FLOOR | 1000 / 0.99 / 1500 | 11.1 |
| CR_OPEN_MAX_HOUR / CR_OPEN_MAX_DAY / FIGHT_TIMEOUT | 3 / 10 / 20 s | 11.1 |
| WANTED_DECAY | 6 h | 11.5 |
| ARENA_TIME_LIMIT | 360 s | 11.3 |
| WIND_EFFECT / STORM_MULT / CURRENT_MAX / SEA_SPEED_CAP | ±0.10 / 0.85 / 0.30 sq/s / ±0.25 | 5.6 |
| HANDS T1–T5 / HANDS_PER_CREW | 10 20 30 40 50 / 2 | 5.7 |
| BOARD_P_MIN / MAX / LOOT_MIN / MAX | 0.05 / 0.90 / 0.5 / 2.0 | 5.7 |
| BOARD_CD_SUCCESS / FAIL / VICTIM_LOCK | 30 s / 60 s / 5 min | 5.7 |
| BOARD_FAIL_HP / FAIL_GOLD / FAIL_GOLD_CAP / FAIL_HANDS | 0.10 / 25·G / 0.05 / 0.30·(1−P) | 5.7 |
| HANDS_REGEN_SEA / BOARD_MIN_HANDS | 1 per min / 0.50 | 5.7 |
| DUEL_TIME / DUEL_HONOR_CAP | 180 s / 10 wins per day | Mechanics |
| LEGENDARY_CP_MAX / FIGHT_SCORE_MAX_LEGENDARY | 3 / 1.63 | 2.7 |
| TOWER_HP (Saltwind / Cinder / Glacier) / TOWER_RANGE / OUT_OF_WINDOW_MULT | 500k / 1.0M / 1.8M / 6 sq / 3 | 10.8 |
| SUPPLY_DRAIN / SUPPLY_NEUTRAL_DAYS / SUPPLY_WEAK / SUPPLY_ON_CAPTURE | 10 per day / 3 / 30 / 60 | 10.8 |
| BANDS (UTC) / WAR_NOTICE / WAR_NOTICE_WEAK | 00 08 16 / 24 h / 12 h | 10.9 |
| MARKET_BAND / ORDER_EXPIRY | ±0.20 / 7 days | 10.7 |
| PARTY_SIZE / RAID_SIZE | 5 / 15 | Mechanics |
| DOCK_SLOTS / BANK_SLOTS | 5 / 60 | 10.2 |
| LOOT_SHARE_MIN | 0.05 | 7.4 |
| DAY_NIGHT_CYCLE / WIND_CHANGE | 60 min / 3–5 min | Mechanics |
