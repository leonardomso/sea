# SEA — Knowledge Base
**Document 1 of 4** · Version 2.0 · September 2026

What exists in the game: the world, the maps, the enemies, the ships, the items, the crew, the skills, the missions, the events. Formulas are in *Math*; rules of behavior are in *Mechanics*; the stack and data model are in *Technical*.

---

## Contents
1. The game in one page
2. Naming style
3. The world and its biomes
4. Maps (all 10)
5. Enemies: ships, monsters, bosses
6. Player ships (hulls)
7. Gear: cannons, ammo, sails, plates, figureheads
8. Consumables (stackable)
9. Crew
10. Skill trees
11. Ship Configs: composing a build
12. Currencies, materials, cosmetics
13. Missions
14. Events
15. Factions
16. Guilds, islands, arena
17. Legendary items
18. Glossary
Appendix A. Story missions by map
Appendix B. Trade goods
Appendix C. Crafting recipes
Appendix D. NPC abilities and behavior values
Appendix E. Loot tables
Appendix F. Achievements

---

## 1. The game in one page

**Sea** is a browser pirate MMO. You captain one ship. You fight sea monsters and enemy ships, collect gold, and make your ship stronger. You fight other players at sea, in a ranked arena, with your guild, and in island wars.

Inspired by Seafight: it keeps what works (one ship, numbered maps, kill-counter bosses, damage-share loot, missions that guide you) and fixes what players complain about (pay-to-win, too many currencies and systems, no protection for beginners).

**Design rules**
1. One ship, forever.
2. Safe first, danger later. Beginners never lose protection by accident.
3. Money buys time and looks, never power.
4. Teach one thing at a time.
5. Every boss attack has a warning.
6. The sea is part of the fight.
7. PvP is the heart of the game. Fights are short (35–50 s). Nobody farms for hours.
8. **No two ships need to be the same.** Hull, cannons, skills, crew, and gear all combine, and they share one power budget, so every choice is a trade.

**Progress** is **Map Rank** (1–10): the highest map you have unlocked. There are no character levels.

**Three currencies**: Gold (everything), Diamonds (real money; cosmetics and convenience), Honor (PvP only; cannot be bought).

---

## 2. Naming style

Real Golden-Age pirates gave ships short, plain names: *Fancy*, *Revenge*, *Ranger*, *Delight*, *Fortune*, *Rover*, *Satisfaction*, *Amity*. Captains went by nicknames: Black Sam, Calico Jack, Old Bart. We follow that:

- **Enemy ships** are named like real pirate vessels: one or two words, easy to say in chat.
- **Named enemies** are captains with a nickname: *Red Mary*, *Old Tarr*, *Ironjaw*.
- **Monsters** get plain sea names: *Reef Crab*, *Kelp Eel*, *Mud Wyrm*.
- **Maps** are named like Seafight: a grid code players say in chat (1/1, 1/2, 2/1 …) plus a name.

---

## 3. The world and its biomes

The world is **The Shattered Tides**: a chain of seas around a whirlpool called The Eye.

| Biome | Maps | Look | What it changes |
|---|---|---|---|
| **Sea** | 1, 2, 3 | Calm water, fishing villages, kelp | Kelp hides ships. Normal wind and vision. |
| **Jungle** | 4, 5 | Rivers, fog, drowned temples | Fog cuts vision. River currents push ships. Narrow channels. |
| **Lava** | 6, 7 | Black rock, glowing vents, iron ships | Vents erupt on a timer with a warning. Lava flows move. Repairs are slower. |
| **Snow** | 8, 9 | Icebergs, whiteouts | Ice floats block shots. Snow storms cut vision. Heavy hulls crack the ice. |
| **Storm** | 10 | Permanent storm around a whirlpool | Water pulls to the center. Lightning hits the tallest ship. |

**Day and night**: 60-minute cycle. At night vision drops and ghost ships appear.
**Wind**: one direction per map, shown on screen, changes every 3–5 minutes. With the wind is 10% faster, against is 10% slower. It helps a runner but cannot decide a fight by itself.

---

## 4. Maps

Maps sit on a grid, one row per biome. Players call them by grid code. Neighbors on the grid connect: left–right inside a row, and up–down between rows in the same column. Boss maps are the second column, so the boss column is also a route.

```
Row 1  Sea      [1/1 Havenmere] — [1/2 Gull Rocks] — [1/3 Brine Fields]
                      |                  |
Row 2  Jungle   [2/1 Fever Delta] — [2/2 Sunken Temple]
                      |                  |
Row 3  Lava     [3/1 Ash Shoals]  — [3/2 The Caldera]
                      |                  |
Row 4  Snow     [4/1 Floe Road]   — [4/2 Black Under]
                      \                /
Row 5  Storm            [5/1 The Eye]
```

| Grid | Name | Map Rank | Biome | Size | Port | Island | PvP | Unique material |
|---|---|---|---|---|---|---|---|---|
| 1/1 | Havenmere | 1 | Sea | 20×20 | Port Lowell | — | Optional | Oak |
| 1/2 | Gull Rocks | 2 | Sea | 20×20 | — | — | Optional | Iron |
| 1/3 | Brine Fields | 3 | Sea | 30×30 | — | Saltwind Fort | Optional | Kelp Resin |
| 2/1 | Fever Delta | 4 | Jungle | 30×30 | Mangrove Port | — | Open | Ironwood |
| 2/2 | Sunken Temple | 5 | Jungle | 40×40 | — | — | Open | Temple Jade |
| 3/1 | Ash Shoals | 6 | Lava | 30×30 | Cinderport | Cinder Fort | Open | Obsidian |
| 3/2 | The Caldera | 7 | Lava | 40×40 | — | — | Open | Sulfur |
| 4/1 | Floe Road | 8 | Snow | 30×30 | Bonehaven | Glacier Fort | Open | Whalebone |
| 4/2 | Black Under | 9 | Snow | 40×40 | — | — | Open | Frostglass |
| 5/1 | The Eye | 10 | Storm | 50×50 round | — | — | Open | Storm Core |

Map Rank N still means "the Nth map unlocked" in this order. "Optional" PvP: only flagged players fight each other. "Open": everyone is flagged. Islands and ice floes block movement only; **nothing blocks cannon fire**.

### 1/1 Havenmere (Map Rank 1)
- **Purpose**: learn to sail, select, shoot, repair. First missions. First crew member.
- **Port Lowell** (main port): Shipwright, Gunsmith, Crew Hall, Mission Board, Guild Hall, Arena Gate, Training Dummy.
- **Enemies**: Skiff, Reef Crab, Fancy, **Red Mary**.
- **Objective**: none. **Weather**: none.

### 1/2 Gull Rocks (Map Rank 2)
- **Purpose**: hidden monsters, salvage, night.
- **Enemies**: Fancy, Gull, Kelp Eel, Hulk, **Old Lantern** (ghost, night only).
- **Objective**: Wreck Field — 3 wrecks every 15 minutes, salvage crates.

### 1/3 Brine Fields (Map Rank 3)
- **Purpose**: first PvP objective, first boss, first island.
- **Enemies**: Rover, Kelp Eel, Barnacle, Drowned (ghost, night), **The Harbormaster** (boss, kill counter).
- **Objective**: Kelp Cache — 3× materials every 15 minutes. **Island**: Saltwind Fort.

### 2/1 Fever Delta (Map Rank 4)
- **Purpose**: first open-PvP map. Fog and currents. Crafting.
- **Mangrove Port**: Jungle ammo, Jungle crew, Ironwood crafting.
- **Enemies**: Raider, Delta Snake, Cutter, Warden, **Fever Queen**.
- **Objective**: River Cache — a random river mouth every 20 minutes.
- **Weather**: fog (vision −40%), currents.

### 2/2 Sunken Temple (Map Rank 5)
- **Purpose**: jungle boss.
- **Enemies**: Guardian, Coil Snake, Warship, Jade Priest, **Yaxal** (boss, sea snake).
- **Weather**: fog in boss phase 2.

### 3/1 Ash Shoals (Map Rank 6)
- **Purpose**: lava biome, second island, Black Market.
- **Cinderport**: Fire ammo, Black Market, Obsidian crafting.
- **Enemies**: Ash Cutter, Cinder Crab, Ironclad, Vent Wyrm, **Slagfist**.
- **Objective**: Obsidian Vein — mining makes noise that shows on the map. **Island**: Cinder Fort.
- **Weather**: vents every 45 s with a 5 s warning; repairs −30%.

### 3/2 The Caldera (Map Rank 7)
- **Purpose**: lava boss.
- **Enemies**: Dreadnought (packs of 3), Magma Wyrm, Forge Warden, **Ignis Rex** (boss, iron warship).
- **Weather**: lava walls move every 20 minutes; repairs −30%.

### 4/1 Floe Road (Map Rank 8)
- **Purpose**: snow biome, third island, Guild Arena unlock.
- **Bonehaven**: Frost ammo, best plates, Whalebone crafting.
- **Enemies**: Hunter (harpoon), Frost Eel, Longship, Leviathan, **Bone Whaler**.
- **Objective**: Bone Yard — Whalebone and a chance of a legendary crew member. **Island**: Glacier Fort.
- **Weather**: snow storms (vision 15%) 2 minutes every 10; T5 hulls take damage on thin ice. Ice floes block movement, not shots.

### 4/2 Black Under (Map Rank 9)
- **Purpose**: snow boss.
- **Enemies**: Deep Hunter, Frost Leviathan, Dread Longship, **Mother Frost** (boss, ice kraken).
- **Weather**: whiteout in boss phase 3.

### 5/1 The Eye (Map Rank 10)
- **Purpose**: endgame. World boss every 6 hours.
- **Enemies**: Spawn, Wraith, and one of four world bosses in rotation: **Hollow King**, **Abyssal Maw**, **Iron Saint**, **Tidecaller**.
- **Objective**: The Pupil — the only source of Storm Core, reachable only against the current.
- **Weather**: permanent storm, lightning, inward current.

---

## 5. Enemies

### 5.1 Kinds and tiers
- **Ships** drop gold and blueprints. **Monsters** drop crafting materials. **Bosses** drop epic and legendary items. All are shot with cannons.

| Tier | Name | Spawns | Meant for |
|---|---|---|---|
| 1 | Common | Always, respawn 30 s | 1 player, ~17 s |
| 2 | Veteran | 1 in 5 spawns, roams | 1 player, ~35 s |
| 3 | Elite | 2–4 per map, 10 min, shown on the map | 2 players |
| 4 | Named | 1 per map, 45 min, server message | 3+ players |
| 5 | Boss | Boss maps; after 50 Elites of the biome are sunk | 6+ players |
| 6 | World Boss | Map 10, every 6 h, rotates | 20+ players |

HP and damage are computed from the base player ship of the map (*Math* §7).

### 5.2 Families
| Family | Biome | Signature |
|---|---|---|
| Sea Dogs (coastal pirates) | Sea | Flee under 25% HP, call help |
| Ghosts | Sea at night, boss maps | Immune to fire, double damage from Blessed |
| Reef Beasts | Sea | Hide in kelp, armor plates |
| Raiders | Jungle | Attack from fog |
| Snakes | Jungle | Dive; hit only while surfaced |
| Ashlords | Lava | Ram, fire ammo, packs |
| Wyrms | Lava | Burrow, erupt under you |
| Whalers | Snow | Harpoons slow and pull |
| Leviathans | Snow | Huge HP, armor breaks in segments |
| Spawn | Storm | Random abilities, scale with players |

### 5.3 Full enemy list
| Map | Enemy | Kind | Tier | Family | Special |
|---|---|---|---|---|---|
| 1/1 | Skiff | Ship | 1 | Sea Dogs | — |
| 1/1 | Reef Crab | Monster | 1 | Reef Beasts | Drops Chitin |
| 1/1 | Fancy | Ship | 2 | Sea Dogs | — |
| 1/1 | **Red Mary** | Ship | 4 | Sea Dogs | Calls 2 Fancies at 50% HP |
| 1/2 | Fancy | Ship | 1 | Sea Dogs | — |
| 1/2 | Gull | Ship | 1 | Sea Dogs | Chain Shot |
| 1/2 | Kelp Eel | Monster | 2 | Reef Beasts | Invisible in kelp until it bites |
| 1/2 | Hulk | Ship | 3 | — | Does not move; salvage crates |
| 1/2 | **Old Lantern** | Ship | 4 | Ghosts | Night only; fire immune |
| 1/3 | Rover | Ship | 1 | Sea Dogs | Chain Shot |
| 1/3 | Kelp Eel | Monster | 2 | Reef Beasts | Hides in kelp |
| 1/3 | Barnacle | Monster | 3 | Reef Beasts | Shoot 4 plates to expose the core |
| 1/3 | Drowned | Ship | 3 | Ghosts | Night; Blessed ×2 |
| 1/3 | **The Harbormaster** | Ship | 5 | Ghosts | Fog at 60%; teleports players at 25% |
| 2/1 | Raider | Ship | 1 | Raiders | Ambush from fog |
| 2/1 | Delta Snake | Monster | 1 | Snakes | Dives 4 s / surfaces 6 s |
| 2/1 | Cutter | Ship | 2 | Raiders | Poison (burn) |
| 2/1 | Warden | Ship | 3 | Raiders | Shore battery support |
| 2/1 | **Fever Queen** | Ship | 4 | Raiders | Poison cloud |
| 2/2 | Guardian | Monster | 1 | Snakes | — |
| 2/2 | Coil Snake | Monster | 2 | Snakes | Dives |
| 2/2 | Warship | Ship | 3 | Raiders | — |
| 2/2 | Jade Priest | Ship | 3 | Raiders | +20% damage to nearby NPCs |
| 2/2 | **Yaxal** | Monster | 5 | Snakes | Dives between phases; fog at 60% |
| 3/1 | Ash Cutter | Ship | 1 | Ashlords | Fire Shot |
| 3/1 | Cinder Crab | Monster | 1 | Wyrms | — |
| 3/1 | Ironclad | Ship | 2 | Ashlords | Rams |
| 3/1 | Vent Wyrm | Monster | 3 | Wyrms | Burrows; erupts under target |
| 3/1 | **Slagfist** | Ship | 4 | Ashlords | Ram + Heavy Shot |
| 3/2 | Dreadnought | Ship | 2 | Ashlords | Packs of 3 |
| 3/2 | Magma Wyrm | Monster | 3 | Wyrms | Erupts under target |
| 3/2 | Forge Warden | Ship | 3 | Ashlords | Repairs allies |
| 3/2 | **Ignis Rex** | Ship | 5 | Ashlords | Adds at 60%; lava walls move at 25% |
| 4/1 | Hunter | Ship | 1 | Whalers | Harpoon: slow + pull |
| 4/1 | Frost Eel | Monster | 1 | Leviathans | Frost Shot |
| 4/1 | Longship | Ship | 2 | Whalers | — |
| 4/1 | Leviathan | Monster | 3 | Leviathans | 3 armor segments |
| 4/1 | **Bone Whaler** | Ship | 4 | Whalers | Harpoons two targets |
| 4/2 | Deep Hunter | Monster | 2 | Leviathans | Stealth under ice |
| 4/2 | Frost Leviathan | Monster | 3 | Leviathans | 3 armor segments |
| 4/2 | Dread Longship | Ship | 3 | Whalers | — |
| 4/2 | **Mother Frost** | Monster | 5 | Leviathans | Freezes the sea at 60%; whiteout at 25% |
| 5/1 | Spawn | Monster | 2 | Spawn | Random ability |
| 5/1 | Wraith | Ship | 3 | Ghosts | Lightning |
| 5/1 | **Hollow King** | Ship | 6 | Ghosts | Summons a ghost fleet |
| 5/1 | **Abyssal Maw** | Monster | 6 | Leviathans | Tentacle roots |
| 5/1 | **Iron Saint** | Ship | 6 | Ashlords | Fire broadsides, rams |
| 5/1 | **Tidecaller** | Monster | 6 | Spawn | Changes the wind every 30 s |

### 5.4 Boss phases
Three phases at 100%, 60%, 25% HP. Each can add abilities, spawn adds, and change the map. Every phase change is announced 3 seconds ahead; every big attack has a 1.5-second tell.

---

## 6. Player ships (hulls)

Five hulls, named after the ships real pirates sailed. Cannon slots come with the hull.

| Tier | Name | Map Rank | HP | Armor F/S/B | Cannons | Sails | Plates | Crew | Cargo | Cost |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Skiff | 1 | 1,600 | 15/8/3 | 8 | 1 | 1 | 2 | 20 | Free |
| 2 | Sloop | 3 | 4,800 | 18/10/4 | 14 | 1 | 1 | 3 | 40 | 20,000 |
| 3 | Brigantine | 5 | 10,500 | 22/12/5 | 20 | 2 | 2 | 4 | 60 | 120,000 |
| 4 | Frigate | 7 | 20,000 | 26/14/6 | 26 | 2 | 2 | 5 | 100 | 500,000 |
| 5 | Galleon | 9 | 36,000 | 30/16/8 | 32 | 3 | 2 | 5 | 150 | 2,000,000 |

Every hull has a magazine of 3 volleys (up to 5 with skills). An equal fight lasts about 35 s without repairs and 45–50 s with them.

**Variants** (tiers 4–5): War (+2 cannons, −10% HP), Merchant (+50% cargo, +5% speed, −2 cannons), Beast (+10% HP, heals 1%/s out of combat, −2 cannons).

A player can own up to 5 hulls at once (the dock; more slots with Diamonds) and switch between them with Ship Configs (§11).

---

## 7. Gear

### 7.1 Cannons
| Tier | Name | Map Rank | Damage | Reload | Range | Cost each | Source |
|---|---|---|---|---|---|---|---|
| 1 | Iron Gun | 1 | 20 | 3.0 s | 8 | 500 | Port Lowell |
| 2 | Steel Gun | 3 | 32 | 2.9 s | 8 | 3,000 | Port Lowell |
| 3 | Ironwood Gun | 5 | 48 | 2.8 s | 9 | 15,000 | Crafted at Mangrove Port |
| 4 | Obsidian Gun | 7 | 68 | 2.7 s | 9 | 50,000 | Crafted at Cinderport |
| 5 | Frostglass Gun | 9 | 92 | 2.6 s | 10 | 150,000 | Crafted at Bonehaven |

### 7.2 Ammo (7 types; hard limit 8)
| Ammo | Where | Effect |
|---|---|---|
| Round | Everywhere | Normal |
| Chain | Everywhere | Slows the target |
| Fire | Cinderport, drops | Burns; halves healing |
| Grape | Everywhere | Slows the target's reload; short range |
| Frost | Bonehaven, drops | Slows the target's turning |
| Blessed | Ghost Tide drops | Double damage on ghosts |
| Heavy | Port Lowell, Map Rank 5+ | More damage, slower reload |

### 7.3 Sails
Reef (turn) · Storm (speed; storms and fog do not slow) · Trade (speed and cargo, less turn)

### 7.4 Plates
Oak (HP) · Ironwood (HP, front) · Obsidian (sides, burn resistance) · Whalebone (HP, back, freeze resistance)

### 7.5 Arms Locker (boarding gear, per ship)
Equips all of a ship's hands. One weapon and one guard at a time.
| Weapon | Map Rank | Where | Guard | Map Rank | Where |
|---|---|---|---|---|---|
| Cutlass | 1 | free | None | 1 | — |
| Boarding Axe | 3 | Port Lowell | Leather | 3 | Port Lowell |
| Pistol | 5 | Mangrove Port | Buff Coat | 5 | Mangrove Port |
| Musket | 7 | Cinderport | Breastplate | 7 | Bonehaven |
| Blunderbuss | 9 | Bonehaven (crafted, Frostglass + Sulfur) | | | |
Numbers and odds in *Math* §5.7.

### 7.6 Figureheads (one ability each)
| Figurehead | Ability | Source |
|---|---|---|
| Kraken | Root a target 2 s | The Harbormaster |
| Serpent | Ramming burns | Yaxal |
| Phoenix | Revive once per 30 min at 20% HP | Ignis Rex |
| Frost Wyrm | Freeze everything around you 2 s | Mother Frost |
| Gilded Lady | +25% gold for the party | Honor shop |

---

## 8. Consumables (stackable)

Every consumable **stacks without limit**. A player can hold a million Round Shot and a thousand Repair Kits. Stockpiling is part of the game; the only limits on use are cooldowns and gold.

| Item | What it does | Limit on use | Where |
|---|---|---|---|
| Ammo (all 7) | One unit per volley | none | Ports, drops |
| Repair Kit | Instant heal, no channel, cannot be interrupted | 45 s cooldown; shares repair fatigue | Every port |
| Rally Beacon | Party respawn point at sea for 10 minutes | one active per party | Every port |
| Harbor Jump | Teleport between unlocked ports | 5 min cooldown; not in combat | Every port |
| Lantern | +15% vision at night for 30 minutes | one active | Every port |
| Sonar Charm | Shows hidden ships within 4 squares for 60 s | 2 min cooldown | Mangrove Port, Bonehaven |
| Salvage Crate | Opens to random materials | none | Wrecks, Hulks |
| Blueprints | Needed once per craft; stack | none | Elites and above |

None can be bought with Diamonds. The bank has tabs for gear and crew; consumables need no bank space.

---

## 9. Crew

Crew is the second build layer next to skills. Each crew member has a role, a rarity, and a level, and gives one bonus. Crew bonuses count toward the same caps and the same **Combat Power budget** as skills (*Math* §2.3), so a player chooses where their power comes from: deep skills, strong crew, or a mix.

### 9.1 Roles
| Role | Bonus | Who wants it |
|---|---|---|
| Gunner | +damage | Strikers |
| Powder Master | −reload | Strikers |
| Carpenter | +HP | Tanks |
| Armorer | +armor on every face | Tanks |
| Helmsman | +speed | Runners |
| Navigator | +turn; less wind penalty | Runners, jungle and storm maps |
| Boatswain | +repair amount | Healers, tanks |
| Surgeon | +Repair Kit heal; one free crew injury per day | Anyone who uses kits |
| Quartermaster | +gold | Farmers, merchants |
| Lookout | +vision; reveals hidden ships | Hunters, kelp and fog maps |
| Cook | small +damage and +HP for 30 min after leaving port | Everyone |
| Master-at-Arms | +ram damage; faster boarding | Bruisers, Plunder builds |

### 9.2 Rarity and level
| Rarity | How you get one | Bonus size | Has an ability |
|---|---|---|---|
| Common | Hire in a port for gold | small | no |
| Rare | Drops from Veterans and Elites; faction reputation | medium | no |
| Legendary | Drops from Named and bosses; the Bone Yard objective | large | yes |

Crew level 1–20. They gain crew XP from kills while on board. A level 1 crew member gives 60% of the listed bonus; level 20 gives 100%. Exact numbers in *Math* §8.3.

Crew slots by hull: 2 / 3 / 4 / 5 / 5. Crew can be moved between hulls in port. If you sink on a boss map or map 10, one crew member is injured for 24 hours (or pay gold; a Surgeon prevents one injury per day).

### 9.3 Named crew (examples)
| Name | Role | Rarity | Where |
|---|---|---|---|
| Mara Hale | Gunner | Common | Port Lowell |
| Kip Adebayo | Boatswain | Common | Mangrove Port |
| Suri Wael | Navigator | Common | Mangrove Port |
| Tomas Vey | Powder Master | Common | Cinderport |
| Anders Kell | Carpenter | Common | Bonehaven |
| Hesper Lin | Armorer | Rare | Bonehaven, Whalers reputation |
| Gideon Marsh | Quartermaster | Rare | Tidewardens reputation |
| Bastien Roux | Cook | Rare | Cinderport, Ashlords reputation |
| **Old Bram** | Boatswain | Legendary | Story reward, map 3 |
| **Nine-Finger Rook** | Gunner | Legendary | Fever Queen |
| **Coinpurse Delgado** | Quartermaster | Legendary | Slagfist |
| **Ysolde Frostwake** | Navigator | Legendary | Bone Whaler |
| **The Widow** | Lookout | Legendary | Old Lantern |
| **Ironjaw Kell** | Master-at-Arms | Legendary | Ignis Rex |

Legendary abilities examples: The Widow — 3 s stealth for the whole party (90 s). Ironjaw Kell — next ram cannot be dodged and stuns 2 s (60 s). Old Bram — next repair cannot be cancelled (45 s).

### 9.4 Crew compositions
| Ship goal | Crew | Result |
|---|---|---|
| Gunboat | Gunner, Powder Master, Cook, Lookout, Quartermaster | Max damage from crew, freeing skill points for Sails or Repair |
| Wall | Carpenter, Armorer, Boatswain, Surgeon, Cook | A tank that spends its skills on Cannons instead of Armor |
| Runner | Helmsman, Navigator, Lookout, Gunner, Cook | Fastest ship on the map |
| Boarder | Master-at-Arms, Gunner, Carpenter, Surgeon, Quartermaster | Rams and boards; wins short fights |
| Farmer | Quartermaster ×2 (different names), Salvager-friendly Lookout, Cook, Boatswain | Most gold per hour |

The point: a player who spends skill points on Sails and Repair can still hit hard by hiring Gunners; a player who fills the Cannons tree has little budget left for combat crew and should hire Quartermasters and Lookouts instead. Different roads, different ships.

---

## 10. Skill trees

Five trees, named for what they do. Skill points come from Map Rank (80) and achievements (10). Points can be in **at most three trees**, and deeper tiers cost more points per level, so filling one tree takes 52–75 points and nobody can fill two. Combat bonuses from skills also sit inside the Combat Power budget with crew and gear.

| Tree | What it does | Tier 1 | Tier 2 | Tier 3 | Capstone |
|---|---|---|---|---|---|
| **Cannons** | Hit harder, reload faster, bigger magazine | Steady Hands, Powder Monkey | Master Gunner, Quick Load, Deep Magazine, Long Nines | Extra Gun, Chain Mastery, Burning Focus | **Devastation**: next 2 volleys ignore armor |
| **Armor** | More HP, tougher faces, ramming | Hull Bracing, Iron Flanks | Reinforced Hull, Iron Bow, Sternguard, Anchor Point | Iron Flanks II, Reinforced Prow, Thick Skin | **Bastion**: all faces +25 for 6 s, cannot fire |
| **Sails** | Speed, turning, escape | Trim Sails, Tacking | Full Canvas, Sharp Rudder, Wind Reader, Evasive Roll | Racing Hull, Fine Helm, Ghost Wake, Storm Sails | **Outrun**: +50% speed, breaks slows |
| **Repair** | Heal more, heal faster, heal others | Damage Control, Fast Hands | Master Carpenter, Calm Under Fire, Field Surgeon, Cleanse | Shipwright's Touch, Steady Repair, Kit Master | **Tide Turner**: party heals 25% |
| **Plunder** | Gold, loot, cargo, boarding | Plunder, Salvager | Smuggler, Boarding Party, Quick Hands, Captain's Call | Pirate's Luck, Boarding Haul, Thrifty Gunner | **Marauder**: boarding score +25%, double haul |

### 10.1 Why every tree is worth taking
| Tree | Wins when |
|---|---|
| Cannons | You can keep a target in range. Best burst. |
| Armor | You hold objectives, beacons, and boss aggro. Best in island war. |
| Sails | You pick your fights. Best 1v1 tree in skilled hands; best for escaping ganks. |
| Repair | You fight long or in a group. Best in Guild Arena and bosses. |
| Plunder | You want gold, materials, and boarding wins. Best income; the Boarding Party silence is a real PvP tool. |

### 10.2 Example builds (90 points, three trees)
Striker (Cannons 45, Sails 30, Repair 15) · Tank (Armor 45, Repair 30, Sails 15) · Bruiser (Cannons 30, Armor 30, Plunder 30) · Healer (Repair 45, Armor 30, Plunder 15) · Merchant (Plunder 45, Sails 30, Armor 15) · Ganker (Sails 45, Cannons 30, Plunder 15).

---

## 11. Ship Configs: composing a build

A **Ship Config** saves a complete setup so a player can switch between playstyles in port:

- Hull (from the dock)
- Cannons, plates, sails, figurehead
- Crew assignment (which crew members are on board)
- Skill distribution (its own three trees and points)
- Ammo slots (4) and ability slots (4)
- Skin and flag

Three configs are free. Up to three more with Diamonds or the Sea Pass. Switching is free at any port and takes 3 seconds. Editing the skills of a config costs the normal reset fee for the trees you change.

Example: **Config 1 "War"** — Frigate War variant, 28 Obsidian Guns, Armor 45 / Repair 30 / Sails 15, crew Carpenter + Armorer + Boatswain + Surgeon + Cook. **Config 2 "Hunt"** — Frigate, Sails 45 / Cannons 30 / Plunder 15, crew Helmsman + Navigator + Lookout + Gunner + Cook. **Config 3 "Farm"** — Merchant Frigate, Plunder 45 / Sails 30 / Armor 15, crew Quartermaster ×2 + Lookout + Cook + Boatswain.

The Ship window shows the config's final numbers and its Combat Power (for example "41/45"), so a player sees exactly what each choice buys.

---

## 12. Currencies, materials, cosmetics

### 12.1 Currencies
| Currency | Earned from | Spent on |
|---|---|---|
| **Gold** | Enemies, missions, trade, PvP kills, objectives | Hulls, cannons, ammo, crew, repairs, crafting, charts, consumables |
| **Diamonds** | Real money; small amounts from achievements and events | Cosmetics, the Sea Pass, convenience. Never a combat stat. |
| **Honor** | Arena, Guild Arena, island war, bounties, objectives. Cannot be bought. | Honor shop: PvP skins, titles, season chests |

Diamonds can be sold for Gold on an anonymous order book (best price matched automatically, price band ±20% of the 7-day average, 10% tax). Buyers and sellers never choose each other.

### 12.2 Materials (one per map)
Oak (1) · Iron (2) · Kelp Resin (3) · Ironwood (4) · Temple Jade (5) · Obsidian (6) · Sulfur (7) · Whalebone (8) · Frostglass (9) · Storm Core (10) · beast parts from monsters (Chitin, Snake Scale, Wyrm Heart, Leviathan Bone). Endgame crafting needs all of them, so every map stays useful. Materials stack without limit.

### 12.3 Diamond catalog
| Category | Items |
|---|---|
| Ship looks | Ship skins (same silhouette as the hull), sail patterns, cannon fire colors, wake effects, figurehead skins |
| Identity | Flag designs, ship name colors, portrait frames, cosmetic titles, companions (seagull, parrot, ship's cat) |
| Social | Victory animations, ping sound packs |
| Guild | Guild flag effects, fort banner skins |
| Convenience | Extra Ship Configs, bank tabs, name change, skill reset (also buyable with gold) |
| Season | **Sea Pass**: 40 cosmetic tiers unlocked by playing, plus 2 configs and a free weekly skill reset |
| Market | Sell Diamonds for Gold on the anonymous order book (10% tax) |

Prices in *Math* §10.7.

---

## 13. Missions

| Type | Count | Reset | Purpose |
|---|---|---|---|
| Story | 3–5 per map | — | Teach one system each; unlock the next map |
| Daily | 3 | 06:00 server | Short goals; one is always PvP |
| Weekly | 1 | Monday 06:00 | A big goal |
| Bounty (enemy) | 5 per port board | Hourly | First to sink a listed Elite or Named |
| Bounty (player) | Player-posted | — | Gold on another player's head |

**What each map's story teaches**: 1 sailing, shooting, repair, ammo, first crew · 2 salvage, hidden monsters, night · 3 PvP flag, objectives, kill counter, island · 4 open sea, fog, currents, crafting · 5 boss fights in a party · 6 fire, vents, Black Market, bounties · 7 packs, moving terrain · 8 snow, plates, Guild Arena · 9 whiteout, island war · 10 The Eye, world bosses.

---

## 14. Events

| Event | When | What happens |
|---|---|---|
| Ghost Tide | Nightly, rotating through the three time bands | Ghosts on every map; Blessed Shot drops |
| Convoy | Every 3 hours | An NPC convoy crosses an island map; escort for Honor or rob for gold |
| Kraken Rising | Weekends, rotating bands | Abyssal Maw surfaces on a random open map |
| Hot Map | Daily | One map gives +50% gold and materials; rotates through all 10 |
| Regatta | First Sunday of the month | A race, no combat |
| Season | Every 3 months | Ranks squash, new Honor shop set, new event skin, one new boss or map |

---

## 15. Factions

| Faction | Biome | Port | Enemy of |
|---|---|---|---|
| Tidewardens | Sea | Port Lowell | Sea Dogs, Ghosts |
| Canopy Compact | Jungle | Mangrove Port | Raiders |
| Ashlords (rebel branch) | Lava | Cinderport | — (Black Market host) |
| Whalers (peace clan) | Snow | Bonehaven | Leviathans |

Reputation unlocks shop items, rare crew, ammo, and a flag. At the lowest reputation a faction closes its port to you; the Black Market still trades.

---

## 16. Guilds, islands, arena

- **Guild**: starts with 20 member slots and grows to 100 by earning **Renown** (wars, Guild Arena, group boss kills, missions, member Honor, member skill). Twenty levels unlock bank tabs, gold and material buffs, discounts, extra arena teams, alliance size, halls in more ports, and cosmetics. Never combat stats. Guilds form **alliances** of up to 20 guilds; an alliance may hold at most 2 of the 3 islands.
- **Islands**: three forts. Owning one gives a toll, a respawn point, free repairs, a vault, daily materials, weekly Honor. Never combat stats. Holding one takes work: a **Garrison Supply** meter drains daily and members refill it by playing on the map; at zero the island goes neutral.
- **Arena**: 1v1, 3v3, 5v5 on your own ship (the Combat Power cap keeps it fair). Ammo is free in the arena. Ranked by Combat Rating.
- **Guild Arena**: 5v5 guild teams on real gear, weekly league in five divisions.

- **Raid**: up to 3 parties (15 players) with one leader and shared map markers, for bosses and wars.
- **Time bands**: the server runs three daily bands (00:00, 08:00, 16:00 UTC). A guild picks a home band; its island is attacked and its Guild Arena queue opens in that band. Events rotate across bands.

Rules in *Mechanics*.

---

## 17. Legendary items

Legendary items are the rarest things in the game. One or two exist per item, in the whole world.

- **How they are sold**: a **sealed-bid Diamond auction**, once or twice a month, announced 7 days ahead. Bids are placed in a 1-hour window; nobody sees other bids. The top 1 or 2 bids win and pay; everyone else pays nothing. One legendary per account, ever.
- **Some are earned**: one copy per season goes to the top of the season's arena ladder, one to the guild with the most island-days, one to the world-boss damage leaderboard.
- **What they are**: a hull skin, a cannon set, a figurehead, or a flag with a unique look, a unique ability animation, a name plate, and a Hall of Fame entry with the owner's name.
- **Power**: a small real edge, capped (Math §2.7): at most +3 Combat Power **outside** the budget, so a legendary owner can reach a fight score of about 1.63 instead of 1.58. Never more.
- **Bound forever**: cannot be traded, sold, gifted, deleted, or moved to another account. It shows the season it was won.

## 18. Glossary

| Term | Meaning |
|---|---|
| Map Rank | Highest map unlocked (1–10). Replaces character levels. |
| Volley | One press of Q: all cannons fire together. |
| Magazine | Volleys stored and ready to fire. |
| Armor face | Front, sides, or back; each has its own armor value. |
| Combat Power | The shared budget (45) for damage, reload, HP, and armor bonuses from skills, crew, and gear. |
| Ship Config | A saved setup: hull, gear, crew, skills, ammo, abilities. |
| Harbor Protection | Beginner shield; nobody can attack you until you attack first or enter map 4. |
| PvP flag | Red = can fight other flagged players. Green = cannot. |
| Combat Rating (CR) | Personal PvP skill number. Only from open sea and arena. |
| Team Rating (TR) | Guild Arena team number. |
| Honor | PvP currency. Cannot be bought. |
| Kill counter | Sinking 50 Elites in a biome spawns its boss. |
| Damage share | Boss loot split by damage and support, not last hit. |
| Hot Map | Today's map with +50% rewards. |
| Island | A guild fort with towers, on maps 3, 6, 8. |
| Repair Kit | Stackable gold item: instant heal, 45 s cooldown. |
| Rally Beacon | Stackable gold item: party respawn at sea for 10 minutes. |
| Harbor Jump | Stackable gold item: teleport between unlocked ports. |
| Sea Pass | Seasonal cosmetic track bought with Diamonds. |
| Garrison Supply | An island's 0–100 upkeep meter; drains daily, refilled by the owner's play on the map. |
| Raid | Up to 3 parties (15) with one leader and shared markers. |
| Time band | One of three daily windows (00:00 / 08:00 / 16:00 UTC) for wars, Guild Arena, and events. |
| Legendary | A one-of-a-kind item from the sealed-bid auction or a season top spot; bound forever. |
| Alliance | Up to 20 guilds (size set by the leader guild's level) acting as one for wars and for all anti-farming rules. |
| Renown | Guild points earned by group play; sets guild level, member slots, and perks. |
| Contribution | A member's share of the guild's Renown; drives bank payouts and guild titles. |
| Related players | Players who cannot earn Honor, rating, or crates from each other (same guild or alliance, recent ex-guild, friends, party, shared device, repeated opponents). |
| Hands | A ship's fighting sailors, used for boarding. |
| Arms Locker | The weapon and guard that equip a ship's hands. |
| Duel fog | The state two duelists enter: the rest of the sea is hidden and cannot touch them; they can sail anywhere. |

---

## Appendix A. Story missions by map
Each mission names its giver, its steps, its reward, and what it unlocks. Rewards in gold use the map's base drop G (Math §10.1).

**1/1 Havenmere — Old Bram (Port Lowell)**
| # | Mission | Steps | Reward | Teaches / unlocks |
|---|---|---|---|---|
| 1 | Cast Off | Sail to the buoy; return to port | 8 × Iron Gun, 200 Round Shot | Click to move, docking |
| 2 | First Blood | Sink 3 Skiffs | 500 gold | Select, Q, magazine |
| 3 | Patch the Hull | Repair once at sea; sink 3 Reef Crabs | 300 gold, 3 Repair Kits | R, kits |
| 4 | Two Kinds of Shot | Buy Chain Shot; sink 2 Fancies using Chain | 800 gold | Ammo slots 1–4 |
| 5 | A Pair of Hands | Hire Mara Hale; take part in sinking Red Mary | Chart to 1/2, first skill point spent | Crew, Named enemies, Map Rank |

**1/2 Gull Rocks — message in a bottle**
| 1 | Wreckers | Salvage 3 wrecks | 1,500 gold | Objectives, Salvage Crates |
| 2 | Something in the Kelp | Sink 3 Kelp Eels | 1,500 gold, Lantern ×3 | Hidden enemies, Lookout |
| 3 | Night Watch | Sink 5 Fancies after dark; survive Old Lantern for 60 s | Chart to 1/3 | Night, ghosts, Blessed Shot |

**1/3 Brine Fields — Saltwind fort keeper**
| 1 | Colours | Turn the PvP flag on and off once | 2,000 gold | The flag, Harbor Protection dialog |
| 2 | The Cache | Take the Kelp Cache once (flagged) | 5 Kelp Resin | Objectives need the flag |
| 3 | Count the Dead | Sink 5 Drowned; watch the counter | 3,000 gold | Kill counter |
| 4 | The Harbormaster | Take part in the boss kill | Old Bram (legendary Boatswain), Chart to 2/1 | Bosses, damage share, Raid |

**2/1 Fever Delta — Mangrove harbormaster**
| 1 | Open Water | Read the open-sea notice; enter 2/1 | 4,000 gold | Open PvP, Map Rank window |
| 2 | Blind Sailing | Cross the delta in fog; sink 4 Raiders | 4,000 gold, Sonar Charm ×2 | Fog, currents |
| 3 | Ironwood | Craft one Ironwood Gun | 20 Ironwood | Crafting, blueprints |
| 4 | Fever Queen | Take part in sinking the Fever Queen | Chart to 2/2 | Named, Ship Configs |

**2/2 Sunken Temple** · 1 Priests First (sink 2 Jade Priests) · 2 Yaxal (boss, in a Raid) → Chart to 3/1. Teaches Raids and boss phases.
**3/1 Ash Shoals** · 1 Vents (survive 3 eruptions) · 2 Fire (buy Fire Shot; sink 5 Ash Cutters) · 3 Black Market (sell anything once) · 4 Slagfist → Chart to 3/2. Teaches hazards, Fire, bounties.
**3/2 The Caldera** · 1 Packs (sink a Dreadnought pack) · 2 Moving Walls (reach the center after a shift) · 3 Ignis Rex → Chart to 4/1.
**4/1 Floe Road** · 1 Whiteout (sink 5 Hunters in a storm) · 2 Plates (craft a Whalebone Plate) · 3 League (register or join a Guild Arena team, or win 3 solo arena matches) · 4 Bone Whaler → Chart to 4/2.
**4/2 Black Under** · 1 Under the Ice (sink 3 Deep Hunters) · 2 Garrison (turn in 20 Whalebone at any fort, or 40 at a port if guildless) · 3 Mother Frost → Chart to 5/1.
**5/1 The Eye** · 1 The Pull (reach the Pupil) · 2 The Rotation (take part in a world boss) · reward: title "Stormrider".

## Appendix B. Trade goods
Base price in gold. Each port produces two goods cheaply (−30%) and pays more for two others (+40%). Prices drift ±40% with supply and demand. Weight is cargo units per item. Cargo is lost on sinking (Math §10.4) and simply disappears.

| Good | Base | Weight | Produced at | Demanded at |
|---|---|---|---|---|
| Rum | 60 | 1 | Port Lowell | Bonehaven |
| Salt Fish | 40 | 1 | Port Lowell | Cinderport |
| Timber | 80 | 2 | Mangrove Port | Port Lowell |
| Spice | 150 | 1 | Mangrove Port | Bonehaven |
| Iron Ingots | 120 | 2 | Cinderport | Mangrove Port |
| Powder | 200 | 1 | Cinderport | Port Lowell |
| Furs | 180 | 1 | Bonehaven | Cinderport |
| Whale Oil | 220 | 2 | Bonehaven | Mangrove Port |

A full Merchant Frigate (150 cargo) of Spice bought at Mangrove (105) and sold at Bonehaven (210) nets about 15,000 gold before risk, about one hour of farming at that rank.

## Appendix C. Crafting recipes
All at the port named. Blueprints drop from Elites and above (Appendix E). Crafting is instant.

| Item | Port | Blueprint | Materials | Gold |
|---|---|---|---|---|
| Ironwood Gun | Mangrove | Ironwood Gun | 10 Ironwood, 5 Iron | 15,000 |
| Obsidian Gun | Cinderport | Obsidian Gun | 10 Obsidian, 5 Sulfur, 2 Wyrm Heart | 50,000 |
| Frostglass Gun | Bonehaven | Frostglass Gun | 10 Frostglass, 5 Whalebone, 2 Leviathan Bone | 150,000 |
| Oak Plate | Port Lowell | — | 20 Oak | 5,000 |
| Ironwood Plate | Mangrove | Ironwood Plate | 20 Ironwood, 5 Chitin | 30,000 |
| Obsidian Plate | Cinderport | Obsidian Plate | 20 Obsidian, 5 Wyrm Heart | 100,000 |
| Whalebone Plate | Bonehaven | Whalebone Plate | 20 Whalebone, 5 Leviathan Bone | 300,000 |
| Reef Sails | Port Lowell | — | 10 Kelp Resin | 10,000 |
| Storm Sails | Bonehaven | Storm Sails | 15 Kelp Resin, 5 Frostglass | 200,000 |
| Trade Sails | Mangrove | — | 15 Kelp Resin, 10 Ironwood | 60,000 |
| Kraken / Serpent / Phoenix / Frost Wyrm figurehead | Port of the boss | drops from the boss | 5 Temple Jade, 1 Storm Core | 500,000 |
| Blunderbuss locker | Bonehaven | Blunderbuss | 20 Frostglass, 20 Sulfur | 1,000,000 |
| Repair Kit | any port | — | — | 10 × G(MapRank) |
| Rally Beacon | any port | — | 5 Oak | 50 × G(MapRank) |

## Appendix D. NPC abilities and behavior values
Abilities NPCs use, with numbers. Speed and turn are given as a fraction of the base player ship of the map.

| Ability | Effect | Duration | Cooldown | Tell |
|---|---|---|---|---|
| Chain Volley | Chain Shot volley (−30% speed) | 4 s | 12 s | none |
| Fire Volley | Fire Shot volley (burn) | 5 s | 12 s | none |
| Harpoon | Slow −40% and pull 1 square/s toward the NPC | 4 s | 15 s | rope animation 1 s |
| Poison Cloud | Area 3 squares, burn 0.006/s | 6 s | 30 s | green ring 1.5 s |
| Ram | 0.15 × NPC HP as damage | — | 20 s | charge 1.5 s |
| Dive | Untargetable | 4 s | every 10 s | bubbles 1 s |
| Burrow / Erupt | Vanishes; erupts under target for 0.10 × target MaxHP | — | 25 s | shadow 2 s |
| Tentacle Grab | Root target | 2 s | 30 s | tentacle 1.5 s |
| Call Help | Spawns 2 Commons | — | once | horn 1 s |
| War Song (Jade Priest) | Nearby NPCs +20% damage | 10 s | 40 s | glow |
| Field Repair (Forge Warden) | Heals an ally NPC 15% | — | 30 s | sparks |
| Lightning (Wraith, Eye) | 5% MaxHP to the highest-HP ship within 6 squares | — | 30 s | flash 1.5 s |
| Ghost Fleet (Hollow King) | Spawns 6 Drowned | — | phase 2 | bell 3 s |
| Wind Shift (Tidecaller) | Changes the wind | — | 30 s | horn 3 s |

| Tier | Speed | Turn | Aggro range | Behavior |
|---|---|---|---|---|
| Common | 0.8 × player | 0.8 × | 4 squares | Patrol; Sea Dogs flee under 25% |
| Veteran | 0.9 × | 0.9 × | 5 | Roam; Ashlords in packs |
| Elite | 0.8 × | 0.7 × | 6 | Guard a spot |
| Named | 0.9 × | 0.8 × | 8 | Guard; uses 2 abilities |
| Boss | 0.7 × | 0.6 × | 12 | Stationary center; phases |
| Hulk | 0 | 0 | 0 | Never moves; passive |
Leash: 12 squares from spawn. NPC range = player cannon range of the map. Armor per §7.1.

## Appendix E. Loot tables
Gold per Math §10.1. Item rolls by tier (chance per kill, per eligible player):

| Tier | Ammo (20–50 of the map's type) | Map material (1–3) | Beast part (monsters only, 1) | Blueprint | Rare crew | Legendary crew | Skin |
|---|---|---|---|---|---|---|---|
| Common | 0.30 | 0.50 | 0.30 | — | — | — | — |
| Veteran | 0.50 | 0.80 | 0.50 | 0.02 | 0.01 | — | — |
| Elite | 0.80 | 1.00 (2–5) | 0.80 | 0.10 | 0.03 | — | — |
| Named | 1.00 | 1.00 (5–10) | 1.00 | 0.50 | 0.10 | 0.05 | 0.02 |
| Boss (top 3) | 1.00 | 1.00 (10–20) | 1.00 | 1.00 epic | 0.20 | 0.15 | 0.05 |
| Boss (share ≥ 0.05) | 1.00 | 1.00 (5) | 1.00 | 0.25 | 0.05 | 0.02 | 0.01 |
| World Boss (top 5) | — | Storm Core ×1 | — | 1.00 | 0.30 | 0.25 | 0.10 |
Which blueprint: the map's craftable gun or plate (Appendix C). Which crew: a random role of that rarity from the map's pool.

## Appendix F. Achievements
Ten give a skill point; each has a solo route (**either** line counts). The rest give titles, Diamonds, or frames.

| # | Skill-point achievement | Solo alternative |
|---|---|---|
| 1 | Take part in a boss kill | Sink 50 Elites |
| 2 | Win an arena match | Win 5 duels |
| 3 | Reach Map Rank 5 | — |
| 4 | Reach Map Rank 10 | — |
| 5 | 100 rule-passed PvP kills | 100 arena wins |
| 6 | Reach Captain rank | Reach 1,300 Combat Rating |
| 7 | Complete every daily for 7 days in a row | Complete 60 dailies |
| 8 | Fight in a contested island war | Take 30 objectives |
| 9 | Guild Arena team reaches Gold | Reach Lieutenant in 3v3 or 5v5 arena |
| 10 | Collect all ten materials | — |

Other achievement families (titles and cosmetics): kills per family, boss kills, duel streaks, arena streaks, island days held, Renown milestones, trade profit, boarding wins, distance sailed, every map visited, every port visited, every crew role hired, seasons played.
