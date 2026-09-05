# SEA — Mechanics Reference
**Document 3 of 4** · Version 2.0 · September 2026

This document describes **how everything behaves**: what a player can do, what happens when they do it, and what is not possible. Numbers live in *Math*; content lives in *Knowledge*. Where this document says "see Math §X", that section holds the formula.

---

## Contents
1. Controls and interface
2. Sailing
3. Targeting and firing
4. Repair, ram, board
5. Abilities and effects
6. The sea (wind, night, weather, hazards)
7. Enemies and bosses
8. Progress: Map Rank and missions
9. Ports, shops, crafting, trade
10. Beginners and protection
11. PvP: flag, duels, open sea, objectives
12. Combat Rating
13. Sinking and respawn
14. Arena
15. Guild Arena (league)
16. Guilds and islands
17. Bounties and Wanted
18. Events and seasons
19. Bot and cheat protection
19A. Honor and rating integrity (anti-farming)
20. Fairness rules (what money cannot do)
21. What is not possible (summary)
22. Groups: party and raid
23. Pings and markers (no chat)
24. Legendary auction
25. Time bands

---

## 1. Controls and interface

### 1.1 Default keys
| Key | Action |
|---|---|
| Left click on water | Sail there |
| Left click on a ship or enemy | Select it as target |
| Q / Space | Fire one volley at the selected target |
| Tab | Select the nearest enemy |
| Esc | Clear target / close window |
| R | Repair |
| E | Board (when allowed) |
| F | Ram (lock heading at full speed) |
| 1 2 3 4 | Ammo slots |
| Z X C V | Ability slots 1–4 |
| W A S D | Manual steering (optional) |
| Shift (hold) | Full speed (bigger wind penalty against the wind) |
| P | PvP flag on/off |
| M | Map · I Ship · K Skills · G Guild · L Loadouts |

These are only **defaults**. Every action in the game can be bound to any key or mouse button, and one action can have two bindings. Settings → Controls lists every action (fire, repair, each ammo slot, each ability slot, each config, ping wheel, every window) with a "press a key" field, a reset button, and three saved control profiles. Bindings are stored per account and apply on every map and in the arena.

### 1.2 Screen
- Your ship in the center. Minimap in the corner with NPC names on hover.
- Wind arrow and strength, day/night clock, magazine dots (ready volleys), reload bar, repair cooldown.
- Selected target: HP bar, name, Map Rank, PvP flag color, guild tag, Wanted stars.
- Kill feed for the current map (last 5 events).
- Boss counter for the biome (for example "Elites 37/50").

### 1.3 Windows
Ship (gear, crew, stats sheet with every final number), Skills, Loadouts, Inventory, Missions, Map, Guild, Arena, Honor Shop, Diamond Shop, Settings. Only one window open at a time; windows never block the sea view fully.

---

## 2. Sailing

- Click a point: the ship turns toward it at its Turn Rate and sails at its Speed. It stops on arrival.
- Ships cannot enter land or a sector shallower than their hull's draft (T4–T5 hulls cannot enter shallow water).
- Wind changes Speed by up to ±10% depending on heading vs wind (see §6).
- Currents add a push vector every tick.
- Collision with land stops the ship. Collision with another ship: both stop for 0.5 s unless it is a Ram (§4.2).
- Map edges: sailing to an exit point on the edge moves you to the connected map. You appear at that map's entry point, never inside tower range or a port.
- Port: entering the port circle makes you invulnerable, clears effects, and opens shops. You cannot fire from inside a port. Leaving takes 3 seconds (a "casting off" bar) so a port cannot be used to dodge shots mid-fight.

---

## 3. Targeting and firing

### 3.1 Selecting
- Click or Tab selects. The selection stays until you clear it, select something else, the target leaves range + 6 squares, or the target becomes invisible.
- You can select anything. Whether you can **fire** on it is decided at fire time (§11.4).

### 3.2 Firing
- Q fires one volley if the magazine has at least one ready volley and the target is within Range. Nothing happens otherwise (a short "out of range" or "reloading" text).
- Firing is in **any direction**. No arc, no broadside bonus.
- A shot is resolved instantly on the server. The client animates the cannonball. There is no dodge stat and no hit chance: in range = hit, unless the target is under Evasive Roll. **Nothing blocks a shot**: islands and ice floes block movement only.
- Minimum 1.0 s between volleys.
- Holding Q fires as soon as each volley is ready (auto-fire while held). There is no auto-fire toggle: if you let go, you stop.

### 3.3 Magazine
- Starts full when leaving port. Refills one volley every Reload seconds, always. Refills fully after 15 s with no shot fired or taken.
- Firing non-stop means you fire once per Reload. Holding fire banks volleys for a burst (see Math §3).

### 3.4 Ammo
- Four ammo slots from the active loadout. Switching is instant; the next volley uses the new type.
- Each volley consumes one unit of that ammo from the inventory. Ammo stacks without limit; a player can carry millions. With 0 units the slot is greyed out and Q does nothing.
- Effects do not stack with themselves; they refresh. Different effects stack.

### 3.5 Facing
The face that takes damage is decided by the angle between the target's heading and the shooter (Math §5.1). Front takes the least, back the most.

### 3.6 What a fight looks like (worked example, two equal Frigates)
Both start with a full magazine (3 volleys), 20,000 HP, Round Shot.

| Time | Player A (Striker) | Player B (Tank) |
|---|---|---|
| 0 s | Selects B, holds fire, circles to get B's back | Selects A, fires volley 1 (A takes 1,768 × 0.86 = 1,520 on sides) |
| 2 s | Still holding | Fires volley 2 (A at 16,960) |
| 4 s | Swaps to Chain, dumps 3 volleys over 2 s: B takes 3 × 1,237 × 0.94 (back) ≈ 3,490 and is slowed | Fires volley 3, magazine empty, now 1 volley per 2.7 s |
| 8 s | Back to Round, fires as volleys come, keeps B's back | Presses R (channel 3 s); A's next volley lands 1,662 = 8.3% — under the 15% cancel line |
| 11 s | | Repair completes: +4,000 |
| 18–26 s | Uses Devastation: two volleys ignore armor, 3,536 | Fires Fire Shot to stop A's repair; uses Bastion at 40% HP |
| 30 s | Presses R while B is in Bastion and cannot fire | Bastion ends; presses R again: fatigue 0.6, +2,400 |
| 35–45 s | Fight ends: whoever kept the better facing and timed repairs between volleys wins | |

Every number in that table comes from *Math* §3, §5, §6. The point: with equal gear, the winner is the one who managed magazine, facing, ammo, and repair timing.

### 3.7 Timing summary
| Thing | Time |
|---|---|
| Minimum between volleys | 1.0 s |
| Reload (T1 → T5, base) | 3.0 → 2.6 s |
| Magazine refill to full when idle | 15 s |
| Repair channel / cooldown | 3.0 s / 15 s |
| Repair Kit cooldown | 45 s |
| Ram cooldown | 8 s |
| Boarding channel | 2 s |
| Effect durations (Chain, Fire, Grape, Frost) | 4 / 5 / 3 / 4 s |
| PvP flag turning off | 60 s |
| Combat timer after the last hit (log-out lock, fight end) | 20 s |
| Respawn / beacon respawn | 8 s / 20 s |
| Spawn shield | 10 s |
| Port cast-off | 3 s |
| Boss phase warning / big attack tell | 3 s / 1.5 s |

---

## 4. Repair, ram, board

### 4.1 Repair (R)
- Starts a channel (3.0 s base). A bar shows over the ship for everyone to see.
- Cancelled if you take 15% or more of Max HP during the channel, or get hit by Fire Shot. One volley from an equal ship is about 7–9%, so one volley does not cancel; two do. Cancelled repairs still start the cooldown.
- Heals when the channel ends. Each repair or kit within 60 s of the last heals 40% less (fatigue), and healing is halved while burning. Up to four repairs fit in a minute.
- Cooldown 15 s. Cannot repair inside a boss "no repair" zone (Sunken Temple phase 3 only).
- Free at the port and at a guild island fort for members, still channelled.

**Repair Kit** (gold item, hotbar): instant heal with no channel, cannot be interrupted, 45-second cooldown separate from R. It counts as a repair for fatigue, so spamming R and kits together gives less each time. Kits stack without limit; the 45-second cooldown is the only limiter. Bought with gold only, never Diamonds.

### 4.2 Ram (F)
- Locks your heading toward the selected target and sets full speed. Any click cancels.
- On contact at ≥ 90% of max speed: damage to target and to self (Math §5.4), both ships stop for 1 s.
- Ram cooldown 8 s. Ramming a port, tower, or protected player does nothing.

### 4.3 Board (E)
Boarding is its own small game: your fighting hands against theirs.

- **Hands**: every hull carries fighting sailors (10 to 50 by tier) plus 2 per crew member on board.
- **Arms Locker**: one weapon type and one guard type equip all your hands (Cutlass → Boarding Axe → Pistol → Musket → Blunderbuss; None → Leather → Buff Coat → Breastplate). Bought or crafted in port, gated by Map Rank, kept per ship. Not part of the Combat Power budget.
- **Boarding Score**: attack = hands × weapon × your HP factor × bonuses; defence = hands × weapon × guard × their HP factor × bonuses. Your chance is attack ÷ (attack + defence), clamped between 5% and 90% (Math §5.7). The Ship window shows your attack and defence scores, and when you select a target the target frame shows your **estimated boarding chance**.
- **How**: within 4 squares of a valid attack target that is at or below half its Max HP, press E. 2-second channel (1 s with Quick Hands, −1 s with a Master-at-Arms), cancelled by any hit; you cannot fire during it. The distance and the hit-point gate are SEA_5_PHYSICS §9.1; below that gate, low HP on either side further shifts the odds.
- **Success**: you receive a **Boarding Haul** paid by the game (gold based on the map and multiplied by how much stronger your score was, 0.5× to 2×, plus an item roll). The victim loses nothing to you. The target's cannons are silenced 3 s, it loses 10% of Max HP and 10% of its hands, and it is not sunk. You lose 5% of your hands; a fight always costs sailors. Against an NPC you get a bonus loot roll and the NPC keeps its HP.
- **Fail**: boarding is a gamble with a price. You lose **10% of Max HP**, you **pay gold** (25 × the map's base gold drop, capped at 5% of what you carry), and **some of your hands die**: 30% × (1 − your chance), so a long shot that fails costs more sailors than a fair one. You cannot board again until the cooldown for that target type is over (SEA_5_PHYSICS §9.3).
- **Hands recover** 1 per minute at sea and fully in any port or guild fort. Under 50% hands you cannot board at all, so a boarder who keeps failing has to go home.
- Cooldowns: 60 s after boarding a player, 15 s after boarding an NPC (SEA_5_PHYSICS §9.3, which replaces the old 30 s success / 60 s fail pair). A player can be boarded at most once every 5 minutes; that is a separate timer on the victim and still applies.
- The Ship window shows current hands ("34/40") next to the boarding scores.
- Boarding is allowed in open sea, island war, and Guild Arena. Not in duels or ranked arena.

---

## 5. Abilities and effects

- Four ability slots from the loadout. Sources: tree capstones and active skills, figurehead, one legendary crew member.
- Each ability has an effect, a duration, and a cooldown; cooldown is always at least 4× duration (Math §8.4).
- Abilities cannot be used in port or while docked. Ghost Wake breaks on firing or repairing.
- Effects on you are shown as icons with timers. Cleanse removes burn, slow, freeze. Anchor Point blocks slow, pull, and root but not burn.
- Captain's Call's damage bonus counts toward the damage cap and the Combat Power budget; a ship already at the cap gains nothing.

---

## 6. The sea

| System | Behavior |
|---|---|
| **Wind** | One direction per map. Changes every 3–5 minutes with a 10-second warning arrow. With the wind: +10% speed. Against: −10%. Across: 0, and it scales smoothly in between. Navigator crew softens the headwind; Wind Reader removes it. Wind never changes damage, range, reload, or turning. Wind, storm, and current together can never move speed more than 25% from base (Math §5.6). |
| **Day/night** | 60-minute cycle, same on all maps (not tied to real time). Night: vision −30%, ghost ships spawn, some Veterans become Elites. Lantern item (port, cheap) restores 15% vision. |
| **Fog** (Jungle) | Patches move slowly. Inside: vision −40%. Lookout crew reduces to −20%. |
| **Currents** (Jungle, Eye) | Push every tick, only in marked sectors, at most 0.3 squares per second. Heavy hulls are pushed less; light hulls more. |
| **Vents** (Lava) | Fire every 45 s in a marked circle. A red ring appears 5 s before. Inside at eruption: burn 5 s. |
| **Lava flows** (Map 7) | Walls move every 20 minutes; the new layout is shown 60 s ahead on the minimap. |
| **Ice floes** (Snow) | Move slowly and block movement (not shots). T5 hulls take 1% HP/s on marked thin ice. |
| **Snow storm** (Snow) | 2 minutes every 10. Vision 15%. Lookout: 35%. |
| **Storm** (Map 10) | Permanent. Speed −15% (Storm Sails skill ignores it). Current pulls to the center, stronger near it. Lightning every 30 s hits the ship with the highest HP in the center ring for 5% Max HP. |
| **Kelp** (Sea) | Ships inside kelp are hidden from players more than 4 squares away. Lookout reveals within 2 squares. |

Rule: every sea effect has a build that turns it into an advantage.

---

## 7. Enemies and bosses

- NPCs spawn at spawn points, respawn on their timers, and patrol, guard, ambush, or stay still depending on their behavior.
- **Aggro**: an NPC attacks the nearest flagged-or-not player inside its aggro range who is not protected by Harbor Protection (protected players are never attacked by NPCs above Common tier; Commons attack everyone so beginners learn to fight).
- **Leash**: an NPC pulled more than 12 squares from its spawn point stops, sails back, and heals to full. NPCs cannot be dragged into ports or tower range.
- **Difficulty by design**: a Common takes a solo player about 17 seconds and cannot sink a player who repairs. A Veteran takes about 35 seconds and needs one repair. An Elite is meant for two players (about 40 seconds) and is risky alone. A Named is for three. A boss is for six. Every NPC's HP and damage are computed from the base player ship of that map (Math §7), so they never drift when player numbers change.
- **Kill credit on every NPC**: damage share (Math §7.4). More damage, more share. Party damage pools and splits equally. Healing and debuffs count. Last hit does not matter. Item rolls need at least 5% share.
- **Boss counter**: every Elite sunk in a biome adds 1 to the biome's counter (server-wide). At 50 the boss spawns on the biome's boss map with a server announcement. The counter shows on screen for players in that biome.
- **Boss rules**: three phases at 100/60/25% HP, 3-second warning before each phase. Every big attack has a 1.5-second tell. HP scales with players beyond the third (cap 12; world boss 30). Boss loot goes by damage share; top 3 get the guaranteed roll.
- **World boss**: Map 10, every 6 hours, rotating through four bosses. Cross-server leaderboard by damage.
- **Ghosts** ignore Fire Shot and take double from Blessed Shot. Serpents can only be hit while surfaced. Leviathans and Barnacle Titans have armor segments that must be shot off before the core takes damage.

---

## 8. Progress: Map Rank and missions

### 8.1 Map Rank
- Everyone starts at Map Rank 1 on map 1.
- To unlock the next map: finish the current map's story missions, take part in one kill of that map's Named or Boss (any damage share), and buy the chart at a port.
- Map Rank never goes down. You can always sail back to lower maps.
- Map Rank sets which hulls and cannons you can equip, how many skill points you have, who you can attack, and which objectives you can take (Math §9).

### 8.2 Story missions
- Given by port NPCs or by a "message in a bottle" on maps without ports.
- Each teaches one thing and points at the next place to go with a marker on the minimap.
- Story missions are the only way new systems appear. A player never sees the Guild Arena button before the map-8 mission that introduces it.

### 8.3 Daily and weekly
- 3 dailies at 06:00 UTC; one is always PvP-related (duel, arena, or objective). A protected player gets a duel or arena daily instead of an open-sea one.
- 1 weekly on Monday. Rewards Honor and gold.
- Missions can be abandoned and re-rolled once per day.

### 8.4 Skill points, trees, crew, and Ship Configs
- Points come from Map Rank and achievements only. They are spent in the Skills window.
- **Three-tree rule**: points can be in at most three trees. The other two are greyed with "Reset a tree to unlock this one".
- Deeper tiers cost more points per level (Tier 1 = 1, Tier 2 = 2, Tier 3 = 3, capstone 5 with 30 points already in the tree). Filling one tree costs 52–75 points; no player can fill two.
- **Skills alone never reach the caps.** A full Cannons tree stops at +20% damage and −15% reload; the last steps come from crew and gear. This is deliberate: it makes crew a real part of the build.
- **Crew**: each crew member gives one bonus that counts toward the same caps and the same Combat Power budget as skills. Two crew of the same role do not stack. Crew level up from kills while on board. Crew can be moved between hulls in port.
- **Combat Power**: the Ship window shows "Combat Power 41/45". When bonuses from skills, crew, and gear pass 45, the extra is greyed with "not active" so the player moves points or swaps crew instead of wasting them.
- **Ship Configs**: a config saves hull, cannons, plates, sails, figurehead, crew assignment, skill distribution, ammo slots, ability slots, skin, and flag. Three free; up to three more with Diamonds or the Sea Pass. Switching is free at any port and takes 3 seconds. Editing a config's skills costs the normal reset fee for the trees changed. A player can own several hulls (the dock) and each config points at one.
- Reset: 10,000 × Map Rank gold per tree (Diamonds also accepted). Resetting one tree refunds that tree only.

### 8.5 Achievements
Award titles and 10 skill points total. Each skill-point achievement has a solo alternative (Knowledge Appendix F), so a player who never joins a guild can still reach 90 points. Some are seasonal and expire.

---

## 9. Ports, shops, crafting, trade

- **Shipwright** sells hulls up to your Map Rank tier. A player can own several hulls; unused ones sit in the dock. Selling a hull refunds 50%.
- **Gunsmith** sells cannons T1–T2 and ammo. T3–T5 cannons are crafted from blueprints at the biome's port.
- **Crew Hall** hires common crew and heals injured crew.
- **Crafting**: blueprint + materials + gold → item. Only at the port of the item's biome. Crafting takes no time.
- **Trade**: each port buys and sells goods with prices that move with supply and demand (±40% around base). Cargo takes cargo space and is lost on sinking (Math §10.4). Merchant hull and Trade Sails make trade routes worthwhile.
- **Black Market** (Cinderport): buys anything at 70% value, sells stolen goods, needs no faction reputation, and hosts the bounty-hunter board.
- **Diamond → Gold market**: an anonymous order book. Sellers post Diamonds at a price inside ±20% of the 7-day average; buyers are matched to the best price automatically and never choose a seller. 10% tax on the seller, orders expire in 7 days. This is the only way value moves between players, and nobody can pick who they trade with.

---

## 10. Beginners and protection

### 10.1 Harbor Protection
- New players start with it. A blue ring shows around the ship.
- While protected: no player can attack you, towers and guards ignore you, NPCs above Common ignore you, you cannot attack players, you cannot take PvP objectives, you earn no Combat Rating.
- **Ends only when**: (a) you confirm your first attack on a player, or (b) you enter map 4.
- **First attack dialog**: the first time you press Q with a player selected, the shot does not fire. A dialog appears: "Attack this pirate? This will remove your Harbor Protection. Other pirates will be able to attack you." with Attack / Cancel. Only Attack fires and ends protection. Pressing P while protected shows the same dialog.
- **Map 4 notice**: the first time you sail to map 4's edge, a full-screen notice explains open sea and asks to Continue or Turn Back. Continue ends protection.
- Protection cannot be lost by hovering, selecting, being hit, a misclick, or any other side effect.

### 10.2 Attack windows
You can only attack, and be attacked by, players within 2 Map Ranks of you (Math §9.3). On maps 1–3, guards sink any attacker 3 or more ranks above the map who fires on a player.

### 10.3 Why bought accounts are worthless
Map Rank 10 takes about 30 hours. Gear is capped and reachable in about 40. Combat Rating decays and is squashed each season. Honor items show the season they were earned. One account per email; phone verification for arena above Mate rank. Gold cannot be sent between players.

---

## 11. PvP

### 11.1 The flag (P)
- Green sword = off. Red sword = on. Shown next to your name for everyone.
- On maps 1–3: only red players can attack red players. Green players cannot attack and cannot be attacked.
- On maps 4–10: the flag is forced on for everyone on entry.
- Turning the flag off takes 60 seconds and is cancelled if you deal or take damage in that time. You cannot log out to escape: a ship in combat stays at sea for 20 seconds after disconnect.
- Red players get +10% gold from NPCs on maps 1–3 (small reward for risk).

### 11.2 Duels
A duel is a private fight between two ships. Nothing else can touch it, and nothing restricts where it goes.

**Starting one**: right-click a player → Duel. Works on any map (not inside a port), with any flag, even under Harbor Protection, across any Map Rank gap. The other player accepts or declines; no answer in 20 s is a decline. A 5-second countdown follows.

**Duel fog**: when the countdown ends, both ships enter the duel state:
- Each duelist sees the map, the sea, and the other duelist. Every other ship, NPC, tower, objective, and hazard is hidden in fog and cannot be targeted. They cannot damage or affect you, and you cannot damage or affect them. NPCs drop aggro. Towers ignore you.
- To everyone else, the two ships show as ghosted with a crossed-swords icon and cannot be selected. Anyone can spectate from the map view.
- There is **no circle and no boundary**. Duelists can sail anywhere, including across map edges; the fog follows them. Chasing and running are part of the duel.
- Everything is usable: ammo, Repair Kits, abilities, crew abilities, Ship Config switching is not (no port). Items are consumed normally, so a duel costs what you fire.
- Both start at full HP with a full magazine and no effects, and both are restored to their **pre-duel HP and effects** when it ends, so a duel cannot be used to heal.
- **Forfeit**: entering a port, using a Harbor Jump, or disconnecting for more than 20 s.

**Ending**: first ship to reach 1 HP loses. Time limit **3 minutes**; at the limit the higher HP percentage wins (equal is a draw). Either player can concede. When it ends, the fog lifts and both ships are untargetable for 5 s so nobody gets jumped at the moment it finishes.

**What you get**
- Winner: 5 Honor for each of the first 10 duel wins per day. The loser loses nothing.
- Both: crew on board earn double crew XP for the duel's duration.
- A duel record (wins, losses, streak) on the profile, and a daily mission "Win 2 duels".
- No Combat Rating, no boarding, no bounty, no Wanted. A duel is pure skill: same rules, same sea, and whoever sails and shoots better wins.

**Not possible**: wagering gold or items on a duel (it would be a gold transfer), dueling from inside a port, boarding during a duel, a third ship interfering in any way.

### 11.3 Open sea
- Uses your real ship and gear (within the caps).
- Sinking a player pays Honor (3), Combat Rating (if the rules pass), and mission progress. Nothing drops; the victim's cargo is simply lost.
- Kills may give Combat Rating if the rules in §12 hold.
- Kill feed shows kills on the map. Replays of your last 5 fights are available from the Ship window.
- **Revenge**: after you sink, you can see who sank you and their position for 10 minutes if they are still on the map.

### 11.4 Can I fire on this target? (decided at fire time)
1. Target is an NPC → yes.
2. I am protected → no (dialog on first attempt).
3. Target is protected → no.
4. Either flag is off on maps 1–3 → no.
5. |Map Rank difference| > 2 → no.
6. Target is in a port or on a Regatta → no.
7. Same party or same guild → no (duels excepted).
8. Otherwise → yes.
The reason is shown as short text under the target when Q is refused.

### 11.5 Objectives
- Every open map (and maps 2–3 for flagged players) has one objective: a resource node, timed chest, wreck field, or control point (Knowledge §3).
- Only players with Map Rank ≤ map number + 2 can take it, and only flagged, unprotected players.
- Taking a node is a 10-second channel that is cancelled by damage. Control points are held by standing on them; 30 seconds uncontested captures.
- Rewards: materials and Honor. Never a combat stat.

---

## 12. Combat Rating

- One number per player per season (Math §11.1). Shown as a rank name on the flag.
- Sources: arena matches (always valid) and open-sea kills (valid only if all 10 rules pass). Duels, Guild Arena, and island war never change it.
- When an open-sea kill gives no rating, the killer sees "No rating: [reason]". Reasons are the rule names, never hidden.
- Arena: leaving or idling 20 s counts as a loss and a 10-minute queue ban (three in a day → 24 h). The same two players matched more than twice in a day get no rating on the third. A loss with under 10% damage dealt is flagged; five flags in a week → review.
- Decay above 1500 CR when not playing. Season end squashes toward 1000.

---

## 13. Sinking, respawn, and getting back to the fight

### 13.1 Sinking
- At 0 HP the ship sinks. Cargo and gold loss depend on the map (Math §10.4). Gear, crew (except one injury on boss maps and map 10), skills, and Map Rank are never lost.
- In arena, sinking ends the game. In Guild Arena, you respawn for the next game of the best-of-3.

### 13.2 Respawn
- After **8 seconds** you choose where to respawn (or the default is used if you do not choose in 5 s): the nearest port on your map, your guild's fort if it is on the same map, or your party's Rally Beacon if one is active. 10-second spawn shield (cannot attack or be attacked).
- Repairing to full at a port costs gold; free at T1 so beginners never get stuck. Free at a fort or beacon.

### 13.3 Getting back to the fight (the regroup problem)
In Seafight, wars stalled because sunk ships took too long to sail back and regroup. Four tools fix this:

| Tool | How it works |
|---|---|
| **Rally Beacon** | A party leader places it at sea (gold). It lasts 10 minutes and shows on the party's map. Party members respawn at the beacon 20 seconds after sinking, full HP, magazine full. Cannot be placed inside enemy tower range, in a port, or on the arena. One active beacon per party. |
| **War staging beacon** | During an island war window, both guilds get a free permanent beacon at their staging point on the war map. Everyone in the war respawns there with full HP at no cost. |
| **Harbor Jump** | From any port, teleport to any other port you have unlocked (gold, 5-minute cooldown, not while in combat). Cuts the sail from map 1 to map 8 from 10 minutes to 3 seconds. |
| **Muster** | During an island war window, members of the two guilds get +25% speed while not in combat on the war map and the two maps next to it. Shown as a horn icon. |

Together: sink → 20 s → back at the beacon with full HP → 30 s sail → fighting again. A war keeps its shape instead of dissolving.

## 14. Arena

- Modes: 1v1, 3v3, 5v5. Open from Map Rank 1, even under Harbor Protection.
- **Your own ship**: you fight with your real hull, gear, crew, and skills. The Combat Power cap keeps the gap between any two ships under 1.6×, and matchmaking is by Combat Rating, so gear differences are small and skill decides. Ammo and Repair Kits are **free inside the arena** (nothing is consumed).
- Matchmaking by Combat Rating only; party queue matches parties only. Target queue time under 30 s.
- Map: 15×15 with a few islands and wind. Three arena maps rotate.
- Win: sink the enemy (1v1) or all enemies; at 5 minutes, higher total damage wins.
- Rewards: Combat Rating, Honor (win or lose), a little gold. Season rewards by final rank.
- **Unranked arena**: same modes, no rating, small Honor. For testing builds and warming up.
- Spectating: any arena match can be watched with a 30-second delay.

---

## 15. Guild Arena (league)

### 15.1 Teams
- A guild registers up to 3 teams. Roster = 5 starters + 2 substitutes. A player is on one team per season; leaving locks them out for 7 days. Roster changes only between Sunday 23:00 and Monday 00:00 UTC (the start of the first band).

### 15.2 Match
- 5v5, own ships within the caps, own configs, free ammo. Best of 3 games, sides switch. Arena 20×20 with 4 islands and wind; three maps rotate weekly.
- A game ends when a team is fully sunk, or at 6 minutes by total damage.
- A match starts only with 5 rostered players per side. A disconnect in the first 30 s of game 1 cancels the match with no penalty. Later disconnects pause once for up to 90 s, then the game continues short-handed.

### 15.3 Week
- Queue windows: every day in all three time bands (00:00–04:00, 08:00–12:00, 16:00–20:00 UTC).
- A team queues when 5 rostered players are online; the matchmaker pairs it with the nearest Team Rating in its division, widening after 3 minutes. Teams never pick opponents.
- **Family matches**: a team can tick "allow same guild / alliance" when it queues. Then it may be matched against a team from its own guild or alliance. Family matches count for Team Rating and for the 5 weekly matches, but pay **no Honor and no Renown**, and the reciprocity checks (§19A.6) watch them. This is the only place in the game where guild mates fight each other for a result.
- 5 matches per week are expected. Unplayed matches count as losses at Sunday 23:00 UTC (rating unchanged if the team played at least 3).

### 15.4 Divisions
Bronze, Silver, Gold, Platinum, Diamond per server. New teams start in Bronze. Each week the top 20% move up and the bottom 20% move down. Diamond holds at most 16 teams.

### 15.5 Rewards and abuse
- Honor per match by division, doubled on a win; guild bank gold weekly by division; season titles, flag effects, and a Diamond-only ship skin.
- Team Rating is inherited by any new team with 3+ members of a disbanded team.
- Three straight losses under 10% damage freezes the team for review. A player disconnecting in over 30% of games in a week sits out the next week. Shared-IP accounts cannot be on opposing teams.

---

## 16. Guilds and islands

### 16.1 Guilds: a guild grows by doing things together

A new guild is small. It earns its size and its perks through **Renown**, which comes only from things the game wants guilds to do: fight wars, win Guild Arena, kill bosses together, finish missions, and have members who play well. Renown cannot be bought with gold or Diamonds.

**Basics**
- Create at any port for 50,000 gold. Starts at Guild Level 1 with **20 member slots**. Roles: Leader, Officer, Veteran, Member, Recruit (Recruit = in 72-hour probation).
- Guild bank (gold and items), guild missions, one flag, a guild hall in Port Lowell.

**Renown (guild points)** is earned by the guild as a whole (numbers in Math §11.7):
| Source | What counts |
|---|---|
| Guild missions | 5 weekly missions posted Monday; e.g. "Sink 500 Elites", "Win 10 Guild Arena games", "Hold an island 3 days", "Members complete 200 dailies", "Kill 3 bosses with 4+ members in the top shares" |
| Member play | Every daily mission a member completes; every 10 Honor a member earns (only Honor that passed the §19A rules) |
| Islands | Capture, successful defence, and each full day held (contested wars only) |
| Guild Arena | Each match win, scaled by division; each promotion |
| Bosses | Each boss or world boss where 3+ members are in the top damage shares |
| Player scores | At season end, the average Combat Rating of the guild's top 20 members pays a season bonus |

Per-member daily Renown is capped, so a small guild of active players can out-grow a big guild of idle ones.

**Guild Level 1–20.** Renown thresholds in Math §11.7. Levels never go down. Each level adds member slots (20 → 100) and unlocks perks:

| Level | Member slots | Unlocks (never combat stats) |
|---|---|---|
| 1 | 20 | Guild missions, bank tab 1, 1 Guild Arena team, alliance of 2 guilds |
| 2 | 24 | Guild buff: gold +2% |
| 3 | 28 | Bank tab 2, fort respawn for allies |
| 4 | 32 | Guild buff: materials +2% |
| 5 | 36 | Alliance of 5 guilds, 2 Guild Arena teams, Rally Beacons −25% for members |
| 6 | 40 | Guild buff: crew XP +10% |
| 7 | 44 | Bank tab 3, guild hall in Mangrove Port |
| 8 | 48 | Guild buff: gold +4% (replaces +2%) |
| 9 | 52 | Harbor Jump −25% for members |
| 10 | 56 | Alliance of 10 guilds, 3 Guild Arena teams, guild emblem effect, one extra daily mission for members |
| 11 | 60 | Guild buff: materials +4% |
| 12 | 64 | Bank tab 4, guild hall in Cinderport |
| 13 | 68 | Vault +50% when holding an island |
| 14 | 72 | Guild buff: crew XP +20% |
| 15 | 76 | Alliance of 14 guilds, 4 Guild Arena teams, port repair −25% for members |
| 16 | 80 | Guild buff: gold +6% |
| 17 | 84 | Bank tab 5, guild hall in Bonehaven |
| 18 | 88 | Fort banner slot (cosmetic), member title "of [Guild]" |
| 19 | 92 | Guild buff: materials +6% |
| 20 | 100 | Alliance of 20 guilds, 5 Guild Arena teams, guild ship skin, Muster on all three island maps |

Guild buffs apply to gold, materials, crew XP, and prices only. They add to the same gold bonus cap (+50%) as Plunder and Quartermasters. No guild perk ever changes damage, HP, armor, reload, speed, or Honor.

**Upkeep.** Perks (not slots, not level) are active in a week only if the guild earned at least a small Renown minimum in the previous week (Math §11.7). A dead guild keeps its size and level but its buffs sleep until members play again.

**Contribution (per member).** Every Renown a member causes is also logged to that member as Contribution. It drives:
- A guild leaderboard (all time, this season, this week) that officers and members see.
- The weekly guild bank **payout**: the leader sets a percentage of bank gold to share; it is split by this week's Contribution share among members past probation.
- Contribution ranks with cosmetic titles ("Deckhand of Saltwind" → "First Mate of Saltwind") and a portrait frame for the top 10 each season.
- Officers can see inactive members (14 days) and their Contribution before deciding who keeps a slot.

**Guild leaderboard (server)** ranks guilds by four things shown side by side: Renown, island-days held this season, best Guild Arena team rating, and the average Combat Rating of the top 20 members. Season end pays Renown and a guild flag effect to the top 10 in each column, so a guild can be known for wars, for arena, for skilled players, or for sheer activity.

**Alliances**
- An alliance is a set of guilds up to the leader guild's alliance size (2 at Level 1, 20 at Level 20). The founding guild's level sets the size; joining guilds can be any level.
- Allied members cannot attack each other, can fill each other's war sides, respawn at each other's forts (from Level 3), and count as one guild for every anti-farming rule (§19A).
- An alliance can hold at most **2 of the 3 islands**. If it holds two, its guilds cannot declare on the third; the third island is always open to outsiders.
- Alliance leaderboard: total island-days and total Renown. Guilds can change alliance once per 30 days; leaving keeps the relation for 14 days.

**Why this makes people care**
A guild that wants 100 slots has to win wars, place in Guild Arena, kill bosses as a group, and keep its members playing; there is no other road. Members see exactly how much they contributed and get paid from the bank for it. The best players raise the guild's season score just by being good. And because perks are money and convenience rather than power, a Level 20 guild is richer and larger, not stronger in a fight.

### 16.2 Islands
Three forts: Saltwind (1/3), Cinder (3/1), Glacier (4/1). A guild can hold **one** island; an alliance at most **two**.

**Benefits of owning** (never combat stats): toll of 0–5% on NPC gold earned by non-members on the map; fort respawn for members; free repairs inside the fort; a vault (guild storage tab); 50 units of the map's material per day into the vault; 5 Honor per member per full day held; the guild flag shown to everyone on the map.

**Garrison Supply (keeping an island takes work)**
Every island has a supply meter from 0 to 100, shown on the map to everyone. It drains 10 a day. Owner members refill it only by playing on that map: turning in the map's material at the fort, sinking Elites and Named, doing the daily "Garrison" missions, and escorting the Convoy. Supply drives everything the island gives:
- Tower max HP: 100% at full supply, 50% at zero. Towers regenerate only while supply is 60 or more.
- Toll, daily materials, and daily Honor scale with supply.
- Under 30: the island shows a **weak garrison** flag on the map and can be declared on with 12 hours' notice instead of 24.
- At 0 for three days in a row: the island turns **neutral**. Towers sit at half HP, no declaration is needed, and the first guild to hold the flag point for 10 minutes takes it.
- Capture sets supply to 60; a successful defence adds 20.
Numbers in Math §10.8. A guild of 30 that plays on its island keeps it near 100 without thinking about it; a guild that takes an island and leaves loses it in about two weeks.

**Towers**: three per fort, fixed HP (500k / 1.0M / 1.8M) and damage equal to the map's base ship DPS, range 6 squares. Tuned so 20 attackers at the map's rank clear all three in about 8 minutes under fire (Math §10.8). They fire only at flagged players who are attacking the fort or a member of the owning guild inside range. They never fire at protected or unflagged players, do not move, and cannot be pulled. Spawn points and map entries are at least 10 squares from any tower.

**War**
1. **Declare**: an attacking guild declares war in the Guild window. One declaration per attacker per island per week. The defenders and everyone in both guilds get a notice.
2. **Window**: opens at least 24 hours after the declaration (12 hours if the island has a weak garrison), inside the **defender's home time band**, and lasts 2 hours.
3. **Fight**: up to 20 per side on the map; more wait outside. To capture, attackers must sink all three towers and then hold the flag point for 10 minutes with at least one attacker on it and no defender on it. Defenders can repair towers (5-second channel, 10% tower HP, cancelled by damage).
4. **Outside the window**: towers have 3× HP and the flag cannot be taken.
5. **After**: the winner holds the island. 7-day cooldown before the island can be attacked again. The loser's vault contents move to their guild bank over a 7-day grace period; materials produced during the grace period go to the new owner.
6. **Rewards**: 5 Honor per kill, 50 per participant for a capture or successful defence. No Combat Rating, no cargo loss during a war.
7. **Regroup**: both sides respawn free at their staging beacon and get the Muster speed buff (§13.3), so a war does not stall while ships sail back.

---

## 17. Bounties and Wanted

- **Enemy bounties**: 5 Elite or Named targets on every port board, refreshed hourly. First to sink one claims the bonus.
- **Player bounties**: any player can post gold (minimum 10,000, 20% tax) on another player. The target sees it. Up to 3 active bounties per target. The killer of the target collects; the kill must be a valid open-sea kill (Map Rank window, flags). Bounties expire after 7 days; expired gold returns minus the tax.
- **Wanted**: attacking lower-rank players who did not fire first raises your Wanted level (Math §11.5). At 3 stars you show on every map, guards target you, and an automatic bounty is posted. Wanted decays with time.

---

## 18. Events and seasons

- Events run on the server clock (Knowledge §11). Each is announced 5 minutes before with the map shown.
- **Ghost Tide** and **Kraken Rising** add spawns; they never remove normal spawns.
- **Convoy** is an NPC group that crosses an island map on a visible route. Escorting (staying within 6 squares until it docks) pays Honor; robbing it (sinking the lead ship) pays gold to the damage-share group. The island owner's towers defend it.
- **Hot Map** rotates daily through all ten maps in order.
- **Regatta** is no-combat: firing is disabled on the regatta map for its duration.
- **Seasons** last 3 months. At season end: Combat Rating squashes, ranks reset, a new Honor shop set arrives, the previous set is retired, one new event skin, and one new boss or map. Seasonal titles expire.

---

## 19. Bot and cheat protection

### 19.1 Server authority
Every hit, movement, timer, reward, and rating change is computed on the server. The client sends only inputs (move here, select that, fire, repair, ability). A modified client cannot deal damage, teleport, see hidden ships, or skip cooldowns.

### 19.2 Trust score
- The server records the timing between inputs, reaction time from "enemy visible" to "selected", path repetition, session length without breaks, and whether menus are ever opened.
- A trust score 0–100 is computed hourly. Under 40: soft check. Under 20: review.

### 19.3 Honeypots
- The server sometimes sends a phantom NPC that the real client never draws. Selecting it is a flag.
- Some spawns are announced in the data one second before they are drawn. Selecting them before they are visible is a flag.

### 19.4 Soft checks
Instead of a captcha, the game asks the player to do an in-game thing a script does not expect: sail to a lighthouse at a random spot for a free chest, or answer a plain-language question in a mission dialog. Two failures in a row → review.

### 19.5 Accounts
- One account per email. Phone verification for arena above Mate rank.
- Accounts on the same device or IP cannot party, cannot trade Diamonds with each other, cannot be on opposing Guild Arena teams, and give each other no Combat Rating. These limits are shown, not hidden.
- Accounts under 3 days or 2 hours of play cannot use the Diamond market.
- No gold transfer between players exists.

### 19.6 Economy
Daily NPC gold soft cap (Math §10.5) so 24-hour farming is not worth it.

### 19.7 Enforcement
- Suspected bots are **shadow-flagged**: they keep playing but earn nothing and gain no rating.
- Bans go out in **weekly waves** with the flagged session replay attached. One appeal, reviewed by a human.
- Player reports open the replay for review. Three rejected reports in a row remove reporting for 30 days.
- Ranked and league rewards are by division, so one cheater cannot block everyone.

---

## 19A. Honor and rating integrity (anti-farming)

Honor cannot be bought, so people will try to farm it. These rules decide, for every reward, whether it is paid. They are written as tests the server runs; the player always sees the result and the reason.

### 19A.1 Related players (the core definition)
Two players A and B are **related** if any of these is true:
1. Same guild.
2. Same alliance (up to 20 guilds, set by the leader guild's level; alliance members count as one guild for every rule here).
3. Same party, or in the same party within the last 60 minutes.
4. On each other's friends list, or were within the last 14 days.
5. Either was in the other's guild or alliance within the last **14 days** (leaving a guild does not clear the relation for two weeks).
6. Shared an IP or device fingerprint within the last 30 days.
7. Either has a **pending or recent invitation** from the other's guild (last 24 h).
8. The pair is on the **reciprocity list** (§19A.6).

Related players can still fight, duel, and sink each other. They simply earn **nothing** from it: no Honor, no Combat Rating, no Boarding Haul, no bounty payout, no mission progress. The kill feed shows the kill with a grey "no reward: related" tag.

### 19A.2 Guild membership timing
- Joining a guild starts a **72-hour probation** for guild rewards: no island Honor (kills, capture, defence, daily hold), no Guild Arena Honor, no share of guild bank payouts. Probation also applies when rejoining a guild you left.
- Leaving a guild keeps you related to it for **14 days** (rule 5) and locks you out of any Guild Arena roster for 7 days.
- A player who has been in **3 or more guilds in 30 days** is flagged "guild hopper": probation becomes 7 days, and a note appears in their profile for guild leaders.
- Guilds record every join and leave. Two guilds whose members overlap by more than **20% over the last 30 days** are treated as related guilds.

### 19A.3 Per-source rules
| Source | Paid only if | Daily cap |
|---|---|---|
| **Duel win** | opponent not related; opponent account ≥ 2 days and ≥ 1 h played; duel lasted ≥ 30 s; opponent dealt ≥ 15% of your Max HP; opponent did not concede in the first 30 s; first paid win against this opponent today | 10 wins (50 Honor) |
| **Arena match** | matchmade (never chosen); opponent not related; not the same opponent more than twice today; loss pays only if you dealt ≥ 10% of enemy HP | 30 matches |
| **Guild Arena match** | teams not from related guilds; team has 5 rostered players present; both teams dealt ≥ 10% of enemy HP in each game | 15 matches per player |
| **Island war kill** | victim not related; victim's guild is the declared enemy or its alliance; victim fired ≥ 3 volleys in the war; first paid kill of this victim this war | 30 kills per war |
| **Island capture / defence** | war was **contested**: the losing side had ≥ 5 members on the war map at some point, and ≥ 25% of tower HP was destroyed (attack) or ≥ 25% of attackers were sunk (defence); winner's guild not related to loser's; member passed probation | 1 per member per war |
| **Island held (daily)** | member passed probation; member played ≥ 30 min that day; island was not won from a related guild in the last 7 days | 5 per day |
| **Objective taken** | flagged, unprotected, Map Rank within N + 2; no related player within 12 squares was the only "contest" | 10 per day |
| **Bounty claim** | poster not related to claimant or target; target not related to claimant; kill passes the Combat Rating validity rules | 5 per day |
| **Daily PvP mission** | the qualifying kills or wins each passed their own rules above | 1 per day |

### 19A.4 Diminishing returns on the same opponent
Across all sources in one day: the first paid result against a given player pays 100%, the second 50%, the third and later 0%. Applies per pair, both directions, and resets at 06:00 UTC.

### 19A.5 Contested-war rule in detail
An island war pays capture or defence Honor only if it was a real fight. The server checks all three at the end of the window:
1. **Presence**: the losing guild had at least 5 members on the war map for at least 5 minutes in total.
2. **Damage**: attackers destroyed at least 25% of total tower HP, or defenders sank at least 25% of the attackers who entered.
3. **Independence**: the two guilds are not related (§19A.1, §19A.2).
If any check fails, the island still changes hands normally, but no Honor is paid to anyone, and the war is marked "uncontested" on both guild records. Three uncontested wars between the same two guilds in a season flag both guilds for review.

### 19A.6 Reciprocity detection (win trading)
The server keeps a rolling 14-day ledger of paid PvP results between each pair of players (duel, arena, open sea, bounty). A pair goes on the **reciprocity list** when:
- they have ≥ 6 paid results between them, and
- results alternate direction (A beats B, then B beats A) in at least 60% of consecutive pairs, or
- results between them make up more than 40% of either player's paid results in the window.
While on the list, the pair is treated as related (§19A.1). The list is recomputed nightly and shown to both players ("No reward: repeated opponent"). It clears after 14 days with no results between them.

### 19A.7 Group and guild patterns
- **Guild ring**: three or more guilds whose wars, arena matches, and kills in 30 days are more than 50% against each other, with alternating winners, are flagged as a ring; all Honor between them is frozen pending review.
- **Alliance limit**: alliance size is set by the leader guild's level (2 to 20); an alliance holds at most 2 islands; a guild can change alliance once per 30 days; leaving keeps the relation for 14 days.
- **Throw detection**: an arena or Guild Arena participant whose damage dealt is under 10% of enemy HP in 5 losses within 7 days is flagged; in Guild Arena the team is frozen (§15.5).

### 19A.8 Enforcement
- Every Honor payment is written to a **ledger** with the source, the opponent, and which rule tests passed. Nothing is paid silently.
- Flags go to review with the replay. Confirmed farming leads to **clawback**: the farmed Honor is removed; if the balance goes negative, Honor-shop items bought during the window are removed; the season's Honor shop is locked for the account.
- Both sides of a farming pair are treated the same; "I was the loser" is not a defence.
- Guild leaders can see their guild's uncontested-war and probation status so they know what will and will not pay before they declare.
- Ranked and league rewards are by division, so a farmed rank hurts nobody else's payout.

### 19A.9 What honest players will notice
Almost nothing. A player who duels different people, queues arena normally, fights in real wars, and stays in one guild never hits these rules. The only visible ones are the 72-hour guild probation, the 14-day relation after leaving a guild, and the per-opponent diminishing returns.

---

## 20. Money: what it can and cannot do

The game earns revenue from cosmetics and convenience. The full catalog and prices are in *Math* §10.6.

| Money can | Money cannot |
|---|---|
| Buy ship skins, sails, fire colors, wakes, flags, frames, companions, animations | Buy hulls, cannons, ammo, plates, crew, Repair Kits, Beacons, Harbor Jumps |
| Buy the Sea Pass (cosmetic track + loadout slots + weekly reset) | Buy skill points, Map Rank, or extra daily missions |
| Buy skill resets, loadout slots, bank tabs, name changes | Buy Honor, Combat Rating, or Team Rating |
| Sell Diamonds for Gold on the market (10% tax) | Send Gold or items to another player |
| Buy guild flag effects and fort banners | Raise any stat past its cap or the Combat Power budget |

Rules that keep this true: per-stat caps and the Combat Power budget (Math §2.2, §2.6), the fight-score test (Math §12.2), Honor from PvP only, one account per email, no player-to-player Gold.

**Skins never change the silhouette** of a hull tier, so a player always knows what they are fighting.

## 21. What is not possible (summary)
22. Groups: party and raid
23. Pings and markers (no chat)
24. Legendary auction
25. Time bands

- Attacking a protected player, an unflagged player on maps 1–3, or a player more than 2 Map Ranks away.
- Losing Harbor Protection without pressing Attack in the dialog or Continue at map 4.
- Firing from inside a port, at a ship in a port, at a duelist from outside the duel (they are in fog), or during a Regatta.
- Logging out to escape combat (the ship stays 20 s).
- Turning the flag off while in combat.
- Dragging an NPC or boss away from its spawn area, into a port, or into tower range.
- Stacking the same effect, or any bonus above its cap or the Combat Power budget.
- Putting skill points in more than three trees, or filling two trees. Stacking two crew of the same role.
- Buying Repair Kits, Beacons, or Harbor Jumps with Diamonds.
- Any ability with a cooldown under 4× its duration.
- Getting Combat Rating from duels, Guild Arena, island war, guild mates, friends, party members, shared-IP accounts, or repeated kills of the same player within 24 h.
- Earning Honor, rating, or crates from a related player (same guild or alliance, ex-guild within 14 days, friends, party, shared device, reciprocity list), or from an uncontested island war.
- Earning guild Honor in the first 72 hours after joining a guild.
- Choosing your opponent in arena or Guild Arena; two teams from one guild meeting.
- Owning more than one island per guild or more than two per alliance; taking an island outside its war window; being attacked by a tower while unflagged.
- Buying guild Renown, member slots, or guild level with gold or Diamonds.
- Choosing who you sell Diamonds to; moving gold or items to another player by any route.
- Blocking a cannon shot with an island or ice.
- Keeping an island with nobody playing on it (supply reaches zero and it turns neutral).
- Owning more than one legendary item, or transferring one.
- Buying power with Diamonds; sending Gold to another player; trading items between players.
- Losing gear, crew (beyond one injury), skills, or Map Rank when sinking.
- A teleport, an instant repair, or a damage number that the client decided.

---

## 22. Groups: party and raid

**Party** (up to 5)
- Invite by right-click. Members cannot damage each other. Damage on NPCs pools into the party and is split equally for gold; item rolls go to each member with a party share ≥ 5%.
- Party members see each other on the map at any distance, with HP bars.
- One party leader: can place a Rally Beacon, mark a target, and set the party's ammo suggestion (a small icon, not a command).

**Raid** (up to 3 parties, 15 players)
- The raid leader invites whole parties. Raids are for bosses, world bosses, and island wars.
- The raid leader can place up to 5 numbered **map markers** (attack here, hold here, regroup) visible to all raid members, and assign each party a marker.
- Boss loot is per player by damage share (Math §7.4); the raid changes nothing about loot, only coordination.
- Island war: the war side is filled from raids first, then individuals, up to 20.

---

## 23. Pings and markers (no chat)

There is no text chat. Coordination uses a **ping wheel** (hold the middle mouse button or the bound key) with 12 pings that show on the map and as a short line above your ship for party, raid, and guild members within range:

Attack · Defend · Retreat · Help · Regroup here · Enemy spotted · Boss soon · Objective · Repairing · Out of ammo · Good fight · Thanks

Pings are rate-limited to 3 per 5 seconds. Raid and party leaders also place numbered map markers (§22). Guild leaders can post a **notice** (one line, editable) that members see on login and in the guild window. Duel and party invites are buttons, not messages.

---

## 24. Legendary auction

- Once or twice a month, announced 7 days ahead in the news window and the Diamond shop.
- **Sealed bids in Diamonds** during a 1-hour window. Minimum bid 5,000 Diamonds. A player can bid once and cannot see others' bids.
- When the window closes, the top 1 or 2 bids (stated in the announcement) win and pay their bid; all other bids are released. Ties go to the earlier bid.
- One legendary per account, ever. Winners are named in the Hall of Fame with the season.
- Bound forever: no trade, no sale, no gift, no deletion, no account transfer. If an account is banned for cheating, the item is retired.
- Edge: at most +3 Combat Power outside the budget (Math §2.7).
- Earned legendaries: one per season each to the top of the solo arena ladder, the guild with the most island-days, and the top of the world-boss damage board.

---

## 25. Time bands

One global server. Three bands each day: **00:00–04:00, 08:00–12:00, 16:00–20:00 UTC**.
These schedule what people turn up for. The weather bands that rotate wind and
storms are a different thing on a different clock; see SEA_5_PHYSICS §12.5.
- A guild chooses a home band at creation (changeable once per 30 days). Its island can only be attacked inside that band, and its Guild Arena weekly matches are usually played there (queues are open in all bands).
- Ghost Tide, Convoy, and Kraken Rising rotate through the bands day by day so every band sees each event at least twice a week.
- Daily resets stay at 06:00 UTC; weekly at Sunday 23:00 UTC.
