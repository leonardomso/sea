# SEA — Technical Reference
**Document 4 of 4** · Version 2.2 · September 2026

The stack, the architecture, and the data model. Everything here implements *Knowledge*, *Math*, and *Mechanics*; where they disagree, those documents win and this one is updated.

---

## Contents
1. Stack decisions
2. Architecture
3. What lives where (SpacetimeDB vs PostgreSQL vs Redis)
4. Game module: tables
5. Game module: reducers
6. Scheduled tables
7. Subscriptions and client data flow
8. Stat computation and the balance tests
9. Accounts, payments, admin
10. Anti-cheat implementation
11. Environments, deployment, observability
12. Build order

---

## 1. Stack decisions

| Layer | Choice | Why |
|---|---|---|
| Game state and logic | **SpacetimeDB**, C# module | All game state in one place; logic runs in the database as transactional reducers; clients subscribe to row changes; no separate game server to keep in sync. |
| Client | **Unity**, C#, **WebGL** export | One language with the module; strong 2D/3D tooling for sea, weather, effects; runs in the browser. Cost: a 15–30 MB first download. |
| Client ↔ game | SpacetimeDB **C# SDK** | Generated bindings from the module; typed tables and reducers. |
| Accounts, payments, support, analytics | **PostgreSQL** | Relational, durable, auditable. Not game state. |
| Sessions, rate limits, matchmaking queues, cache | **Redis** | Fast, ephemeral. |
| Website (`sea.com`) | **TanStack Start + TanStack Router + TanStack Query + React** (TypeScript), shadcn/ui, Tailwind, Drizzle ORM on PostgreSQL | Player-facing: sign up, login, account security, store, market, profile, guild pages, leaderboards, news, support. Hosts the Unity WebGL build at `play.sea.com`. |
| Admin app (`admin.sea.com`) | Same stack, **separate application, separate deployment, separate domain** | Staff only. Not reachable by players: separate build, separate database role, allow-listed, 2FA required. Reads PostgreSQL and reads the game live through the SpacetimeDB TypeScript SDK with an admin identity. |
| Payments | Stripe (web); Apple/Google if native apps ever ship | Diamonds are final; store policy handles refunds; chargebacks remove Diamonds and lock purchases. |
| SMS verification | Twilio Verify (or equivalent) | Required for arena above Mate and for the Diamond market. |
| Auth | **Better Auth** on the web backend: email + password with required email verification, Sign in with Google, passkeys (WebAuthn), TOTP two-factor, password change and reset, session management. SpacetimeDB identity is issued from the Better Auth session (OIDC token). | One login for site, admin, and game. |

Language everywhere is C#, except the web app (TypeScript).

---

## 2. Architecture

```
 play.sea.com (Unity WebGL) ──WebSocket──►  SpacetimeDB (game module, C#)
        │                                          ▲   ▲
        │ HTTPS (auth token)                       │   │ admin identity, live views
        ▼                                          │   │
 sea.com  (website, TanStack) ──► PostgreSQL ◄──┐  │  admin.sea.com (admin app, TanStack)
        │                            ▲          │  │        │
        └──────── Redis ◄────────────┴── Worker service (C#) ┘
                                  (Stripe webhooks, matchmaking, metrics, admin reducers)
```

- **Game module** owns every player, ship, map, NPC, guild, island, match, rating, and ledger row. Clients call reducers and subscribe to public tables.
- **Website** (`sea.com`, TanStack Start) owns accounts and login (Better Auth), account security settings, Diamond purchases (Stripe), the Diamond→Gold order book front end, profile and guild pages, leaderboards, news, and support tickets. It serves the game at `play.sea.com`.
- **Admin app** (`admin.sea.com`, TanStack Start) is a separate application for staff. It reads PostgreSQL through a read-mostly database role and the game live through the SpacetimeDB TypeScript SDK with an admin identity, and performs actions only through admin reducers via the worker. Players have no route to it: different domain, different build, IP allow-list or VPN, 2FA required, no shared session cookies with the website.
- **Worker service** (C#, runs beside the module) bridges the two worlds: grants Diamonds to the game after a Stripe webhook, pulls ledgers out of the module into PostgreSQL for analytics, runs matchmaking for arena and Guild Arena over Redis queues, sends SMS codes, and executes admin actions through admin-only reducers.
- Nothing about combat, gold, Honor, or rating ever runs outside the game module.

---

## 3. What lives where

| Data | Store | Notes |
|---|---|---|
| Accounts, email, password hash, OIDC links, phone verified | PostgreSQL | Never in the module |
| Purchases, Diamond grants, chargebacks, refunds | PostgreSQL (source of truth) → module (`GrantDiamonds` reducer) | The module holds the spendable balance |
| Support tickets, ban records, appeals | PostgreSQL | Admin panel |
| Analytics warehouse (ledgers, kill records, economy) | PostgreSQL, filled nightly from the module | Read-only copies |
| Sessions (Better Auth), CSRF, rate limits, email throttles | Redis | |
| Arena and Guild Arena queues | Redis (worker) → module (`StartMatch` reducer) | Queue entries are ephemeral; matches are module rows |
| Everything else in the game | SpacetimeDB | Content, players, ships, world, guilds, islands, ratings, ledgers, anti-cheat |

---

## 4. Game module: tables

Conventions: `[SpacetimeDB.Table(Accessor = "...", Public = true)]` for tables clients may subscribe to; no `Public` for private. Enums and nested structs are `[SpacetimeDB.Type]`. `Identity` is the player key. Timestamps are `Timestamp`. Money is `ulong`. "None" is 0 or `Timestamp.UNIX_EPOCH`.

### 4.1 Content tables (public, seeded by `Init` from embedded JSON)
| Table | Key | Main fields |
|---|---|---|
| `Map` | `MapId` (1–10) | `Code` ("1/1"), `Name`, `Biome`, `Width`, `Height`, `PvpMode`, `PortX/Y/Radius`, `IslandId`, `ChartCostGold`, `UnlockNpcDefId`, `MaterialDefId`, `Neighbors` (List<byte>), `EntryPoints` |
| `Sector` | (`MapId`, `X`, `Y`) | `Terrain` (Water, Shallow, Land, Kelp, Ice, Lava, Sandbar, Vent), `Depth`, `CurrentDx/Dy/Strength`, `Stealth` |
| `SpawnPoint` | `Id` | `MapId`, `X`, `Y`, `Radius`, `NpcDefId`, `Count`, `RespawnSecs`, `Condition` |
| `Hazard` | `Id` | `MapId`, `Kind`, `Area`, `Damage`, `WarningMs`, `CycleSecs`, `Moves` |
| `ObjectiveDef` | `ObjectiveId` | `MapId`, `Kind`, `X`, `Y`, `RespawnSecs`, `ChannelMs`, `LootTableId`, `HonorReward` |
| `NpcDef` | `NpcDefId` | `Name`, `Kind`, `Tier`, `MapId`, `Family`, `Behavior`, `SpeedMult`, `TurnMult`, `Aggro`, `AbilityIds`, `ResistAmmo`, `WeakAmmo`, `LootTableId`, `Phases`, `Announce`, `BoardingGuard` |
| `AbilityDef` | `Id` | `Name`, `Effect`, `Value`, `DurationMs`, `CooldownMs`, `Target`, `TellMs` — `Init` throws if `CooldownMs < 4 × DurationMs` |
| `HullDef` | `Id` | tier, variant, HP, armor F/S/B, slots (cannon/sail/plate/crew), cargo, speed, turn, draft, magazine, hands, cost, `MapRankRequired` |
| `CannonDef`, `AmmoDef`, `SailDef`, `PlateDef`, `FigureheadDef`, `WeaponDef` (locker), `GuardDef` (locker) | `Id` | stat fields per Math §2–5 |
| `CrewDef` | `Id` | `Name`, `Role`, `Rarity`, `Stat`, `ValueAt20`, `AbilityId`, `HirePortMapId`, `HireCostGold`, `Portrait` |
| `SkillDef` | `Id` | `Tree` (Cannons, Armor, Sails, Repair, Plunder), `Name`, `Tier`, `MaxLevels`, `Stat`, `BonusPerLevel`, `AbilityId`, `RequiresTreePoints` |
| `MissionDef`, `MissionStepDef` | `Id` | type, map, giver, steps, rewards, unlocks (Knowledge Appendix A) |
| `EventDef` | `Id` | schedule, band rotation, map rule, spawns, modifiers |
| `GoodDef`, `MaterialDef`, `RecipeDef`, `LootTable` | `Id` | Knowledge Appendices B, C, E |
| `IslandDef` | `IslandId` | `MapId`, `TowerHp`, `TowerDps`, `TowerPositions`, `FlagX/Y`, `StagingX/Y` |
| `CosmeticDef` | `Id` | kind, price in Diamonds or Honor, season, `Legendary` flag |
| `StatCaps` | `Id` = 1 | every cap and constant from Math §13.2, loaded once |

### 4.2 Player tables
| Table | Public | Key | Fields |
|---|---|---|---|
| `Player` | yes | `Identity` | `Name` (unique), `MapRank`, `Gold`, `Diamonds`, `Honor`, `HarborProtection`, `PvpFlag`, `Wanted`, `GuildId`, `ActiveConfigId`, `Online`, `PlayTimeSecs`, `Title`, `CreatedAt`, `LastSeen` |
| `PlayerPrivate` | no | `Identity` | `AccountId` (PostgreSQL), `PhoneVerified`, `FirstAttackConfirmed`, `PvpFlagChangingUntil`, `CombatRating`, `TrustScore`, `ShadowFlagged`, `DeviceHash`, `IpHashes`, `GuildHistory`, `Friends`, `LegendaryOwned` |
| `Hull` (dock) | yes | `HullId` | `Owner`, `HullDefId`, `Name`, `SkinId`, `Hands`, `WeaponDefId`, `GuardDefId` |
| `GearItem` (bank + equipped) | yes | `ItemId` | `Owner`, `Kind`, `DefId`, `EquippedHullId` (0 = bank), `Slot` |
| `Crew` | yes | `CrewId` | `Owner`, `CrewDefId`, `Level`, `Xp`, `AssignedHullId`, `InjuredUntil` |
| `ShipConfig` | yes | `ConfigId` | `Owner`, `Slot` (1–6), `Name`, `HullId`, `CrewIds` (≤5), `AbilityIds` (4), `AmmoIds` (4), `SkinId`, `FlagId` |
| `SkillPoint` | yes | `Id` | `Owner`, `ConfigId`, `SkillDefId`, `Levels` — server enforces ≤ 3 trees per config and point costs |
| `ShipStats` | yes | `ConfigId` | cached final stats: `VolleyDamage`, `ReloadMs`, `Magazine`, `MaxHp`, `ArmorF/S/B`, `Speed`, `Turn`, `Range`, `RepairAmount`, `RepairChannelMs`, `CombatPowerUsed`, `CombatPowerInactive`, `BoardAttack`, `BoardDefence`, `FightScore` |
| `Stack` (consumables, materials) | yes | (`Owner`, `DefId`) | `Count` (ulong, no limit) |
| `MissionProgress` | yes | `Id` | `Owner`, `MissionDefId`, `Step`, `Count`, `State` |
| `Achievement` | yes | `Id` | `Owner`, `AchievementDefId`, `UnlockedAt` |
| `Keybind` | yes | `Id` | `Owner`, `Profile` (1–3), `Action`, `Key1`, `Key2` |
| `Cosmetic` | yes | `Id` | `Owner`, `CosmeticDefId`, `SeasonWon`, `Legendary` (bound: no reducer can move or delete it) |

### 4.3 World (hot) tables — clients subscribe `WHERE MapId = X`
| Table | Key | Fields |
|---|---|---|
| `ShipState` | `ConfigId` | `MapId`, `Owner`, `X`, `Y`, `Heading`, `TargetX/Y`, `Hp`, `MagazineReady`, `NextReloadAt`, `LastFireAt`, `SelectedTarget`, `InCombatUntil`, `RepairFatigueN`, `LastHealAt`, `RepairCooldownUntil`, `RepairingUntil`, `HpAtRepairStart`, `KitCooldownUntil`, `BoardCooldownUntil`, `BoardedLockUntil`, `AbilityCooldowns` (4), `PvpFlag`, `Protected`, `DuelId`, `RaidId`, `PartyId`, `DisplayName`, `MapRank`, `GuildId`, `Wanted`, `Hands` |
| `NpcState` | `NpcId` | `MapId`, `NpcDefId`, `SpawnPointId`, `X`, `Y`, `Heading`, `Hp`, `MaxHp`, `Dps`, `Target`, `Phase`, `DamageBy` (≤ 20 entries), `Submerged`, `LeashHome` |
| `Effect` | `Id` | `MapId`, `Target`, `Kind`, `Value`, `Source`, `EndsAt` |
| `Wind` | `MapId` | `Angle`, `ChangesAt` |
| `ObjectiveState` | `ObjectiveId` | `MapId`, `AvailableAt`, `HeldBy`, `Progress` |
| `Beacon` | `Id` | `MapId`, `X`, `Y`, `PartyId` or `GuildId`, `ExpiresAt`, `Kind` (Rally, WarStaging) |
| `Marker` | `Id` | `RaidId`, `Number`, `MapId`, `X`, `Y`, `Label`, `AssignedPartyId` |
| `Ping` | `Id` | `MapId`, `From`, `X`, `Y`, `Kind`, `Audience` (Party, Raid, Guild), `ExpiresAt` |
| `Tower` | `Id` | `IslandId`, `MapId`, `X`, `Y`, `Hp`, `MaxHp` |
| `BossCounter` | `Biome` | `ElitesSunk`, `LockoutUntil` |

### 4.4 Social tables
| Table | Fields |
|---|---|
| `Guild` | `GuildId`, `Name`, `Tag`, `Leader`, `Level`, `Renown`, `RenownThisWeek`, `RenownLastWeek`, `MemberSlots`, `BankGold`, `BankTabs`, `Buffs`, `HomeBand`, `AllianceId`, `FlagId`, `Notice`, `CreatedAt` |
| `GuildMember` | `GuildId`, `Identity`, `Role` (Leader, Officer, Veteran, Member, Recruit), `JoinedAt`, `ProbationUntil`, `Contribution`, `ContributionWeek`, `ContributionSeason` |
| `GuildMission` | `Id`, `GuildId`, `Week`, `MissionDefId`, `Progress`, `Done` |
| `Alliance` | `AllianceId`, `LeaderGuildId`, `MaxGuilds`, `GuildIds`, `CreatedAt` |
| `Party` / `PartyMember` | `PartyId`, `Leader`, `RaidId`; members |
| `Raid` | `RaidId`, `Leader`, `PartyIds` (≤ 3) |
| `GuildTeam` | `TeamId`, `GuildId`, `Name`, `Roster` (5 + 2), `TeamRating`, `Division`, `MatchesThisWeek`, `AllowFamily`, `InheritedFromTeamId`, `FrozenUntil` |
| `LeagueMatch` | `Id`, `TeamA`, `TeamB`, `Games` (≤ 3 results), `Band`, `StartedAt`, `Family` (bool), `Forfeit`, `Disconnects` |
| `Division` | `SeasonId`, `Name`, `TeamIds` |
| `ArenaMatch` | `Id`, `Mode`, `Teams`, `Ranked`, `MapId` (arena map), `Winner`, `DurationSecs`, `ReplayRef`, `RatingDelta` |
| `Duel` | `Id`, `A`, `B`, `StartedAt`, `EndsAt`, `AHpBefore`, `BHpBefore`, `AEffectsBefore`, `BEffectsBefore`, `Result` |
| `Island` | `IslandId`, `OwnerGuildId`, `Supply` (0–100), `SupplyTodayFromMaterials/Elites/Missions/Convoy`, `CapturedAt`, `CooldownUntil`, `TollPct`, `Neutral`, `WeakGarrison` |
| `IslandVaultItem` | `IslandId`, `DefId`, `Count`, `GraceUntil` |
| `WarDeclaration` | `Id`, `AttackerGuildId`, `IslandId`, `DeclaredAt`, `WindowStart`, `WindowEnd`, `Band`, `Contested` (computed at close), `Result`, `PresenceSecsLoser`, `TowerHpDestroyedPct`, `AttackersSunkPct` |
| `Bounty` | `Id`, `Target`, `Poster` (0 = automatic), `Gold`, `CreatedAt`, `ExpiresAt`, `ClaimedBy` |
| `RankEntry` | `Owner`, `SeasonId`, `Mode`, `Points`, `Rank`, `Wins`, `Losses`, `LastMatchAt` |
| `Season` | `SeasonId`, `StartsAt`, `EndsAt`, `HonorShopSetId` |
| `MarketOrder` | `Id`, `Seller` (private), `Diamonds`, `PricePerDiamond`, `Remaining`, `ExpiresAt` — buyers never see `Seller` |
| `MarketStats` | `Day`, `VolumeWeightedAvg7d` |
| `LegendaryAuction` / `AuctionBid` | `Id`, `CosmeticDefId`, `OpensAt`, `ClosesAt`, `Winners`; bids private: `Bidder`, `Diamonds`, `PlacedAt` |

### 4.5 Ledgers and anti-cheat (private)
| Table | Fields |
|---|---|
| `Fight` | `A`, `B`, `StartedAt`, `LastHitAt`, `AHpPctStart`, `BHpPctStart`, `AVolleys`, `BVolleys` — ends 20 s after the last hit |
| `KillRecord` | `Killer`, `Victim`, `MapId`, `FightId`, `CrAwarded`, `HonorAwarded`, `Reason` (enum of every rule in Mechanics §12 and §19A) |
| `HonorLedger` | `Identity`, `Source`, `Opponent`, `Amount`, `RulesPassed` (bitmask), `RulesFailed`, `At` |
| `RenownLedger` | `GuildId`, `Identity`, `Source`, `Amount`, `At` |
| `PairResult` | (`A`, `B`), rolling 14-day list of paid results with direction — feeds the reciprocity list |
| `ReciprocityList` | (`A`, `B`), `Until`, `Reason` |
| `RelatedCache` | (`A`, `B`), `Related` (bool), `Reason`, `ComputedAt` — recomputed on guild/party/friend changes and nightly |
| `TrustEvent`, `TrustScore`, `InputSample`, `DeviceLink`, `DailyEarnings` | per Mechanics §19 |
| `AdminAction` | `Admin`, `Action`, `Target`, `Payload`, `At` |

---

## 5. Game module: reducers

Every reducer checks `ctx.Sender` owns the thing it touches, or is the module (scheduled), or is an admin identity (admin reducers).

**Movement and combat**: `MoveTo`, `SelectTarget`, `Fire` (range, magazine, 1 s interval, attack permission, duel rules, LOS not required), `SetAmmo`, `UseAbility`, `StartRepair`, `UseKit`, `Ram`, `Board` (Math §5.7 roll happens here, hands and gold updated), `TogglePvpFlag`, `ConfirmFirstAttack`, `AcknowledgeOpenSea`.
**Travel**: `EnterMap` (edge transition), `Dock`, `Undock`, `HarborJump`, `PlaceBeacon`, `Respawn(choice)`.
**Economy**: `BuyItem`, `SellItem`, `BuyHull`, `SellHull`, `Equip`, `Unequip`, `SetLocker`, `HireCrew`, `AssignCrew`, `HealCrew`, `Craft`, `BuyChart`, `TradeGood`, `BlackMarketSell`, `PostMarketOrder`, `BuyDiamonds(market)`, `CancelMarketOrder`.
**Build**: `SetSkillLevel`, `ResetTree`, `CreateConfig`, `EditConfig`, `SwitchConfig`, `SetKeybind`.
**Missions**: `AcceptMission`, `AbandonMission`, `RerollDaily`, `TurnInMission`, `ClaimDailyChest`.
**PvP**: `SendDuel`, `AnswerDuel`, `ConcedeDuel`, `PostBounty`, `QueueArena`, `LeaveQueue`.
**Groups**: `InviteParty`, `LeaveParty`, `FormRaid`, `SetMarker`, `SendPing`.
**Guild**: `CreateGuild`, `Invite`, `Accept`, `Leave`, `Kick`, `SetRole`, `SetNotice`, `SetHomeBand`, `Donate`, `SetPayoutPct`, `RegisterTeam`, `SetRoster`, `QueueLeague(allowFamily)`, `DeclareWar`, `SetToll`, `VaultDeposit`, `VaultWithdraw`, `TurnInSupply`, `FormAlliance`, `JoinAlliance`, `LeaveAlliance`.
**Auction**: `PlaceBid` (sealed; one per account; blocked if `LegendaryOwned`).
**Internal (scheduled or module-only)**: `Init`, `ClientConnected`, `ClientDisconnected`, `Tick`, `ExpireEffect`, `FinishRepair`, `RespawnNpc`, `RespawnPlayer`, `ApplyFlagOff`, `ChangeWind`, `ResetObjective`, `OnNpcKilled` (damage share, counters, missions, supply, earnings cap), `OnPlayerSunk` (KillRecord, CR and Honor rules, Wanted), `EndFight`, `EndDuel`, `OpenWar`, `CloseWar` (contested check), `SupplyDrain` (hourly), `NeutralizeIsland`, `OpenLeagueQueue`, `CloseLeagueQueue`, `EndWeek` (promotion, payouts, upkeep), `DailyReset`, `WeeklyReset`, `SeasonEnd`, `RecomputeStats`, `RecomputeRelated`, `RecomputeReciprocity`, `CloseAuction`, `BanWave`.
**Admin-only**: `GrantDiamonds` (from the worker after a Stripe webhook), `RemoveDiamonds` (chargeback), `ShadowFlag`, `Ban`, `Unban`, `ClawbackHonor`, `RetireLegendary`, `SetAnnouncement`, `SpawnEvent`, `HotfixConstant` (writes `StatCaps`, then `RecomputeStats` for all).

---

## 6. Scheduled tables

| Schedule | Interval / one-shot | Reducer |
|---|---|---|
| `TickSchedule` (one row per active map) | every 100 ms | `Tick`: movement, currents, NPC AI, aggro, leash, fight timeouts, magazine refill, tower fire, hazard timers |
| `EffectSchedule` | at `EndsAt` | `ExpireEffect` |
| `RepairSchedule` | channel end | `FinishRepair` |
| `RespawnSchedule` | one-shot | `RespawnNpc`, `RespawnPlayer` |
| `FlagSchedule` | 60 s | `ApplyFlagOff` |
| `WindSchedule` (per map) | 3–5 min | `ChangeWind` |
| `ObjectiveSchedule` | one-shot | `ResetObjective` |
| `DuelSchedule` | 180 s | `EndDuel` |
| `WarSchedule` | window start / end | `OpenWar`, `CloseWar` |
| `SupplySchedule` | hourly | `SupplyDrain` |
| `BandSchedule` | 00:00 / 08:00 / 16:00 UTC and +4 h | `OpenLeagueQueue`, `CloseLeagueQueue`, event rotation |
| `DailySchedule` | 06:00 UTC | `DailyReset` |
| `WeeklySchedule` | Sunday 23:00 UTC | `EndWeek`, `WeeklyReset`, `BanWave`, `RecomputeReciprocity` |
| `AuctionSchedule` | close time | `CloseAuction` |
| `MagazineSchedule` | 15 s after last combat | `RefillMagazine` |
| `HandsSchedule` (per ship at sea) | every 60 s | `RegenHands` |

Scheduled reducers check `ctx.Sender == ctx.Identity` so clients cannot call them. Maps with no players skip ticks (the schedule row is removed on last leave and re-inserted on first enter).

---

## 7. Subscriptions and client data flow

| When | Client subscribes to |
|---|---|
| On connect | All content tables (cached, versioned by a `ContentVersion` row). Own `Player`, `Hull`, `GearItem`, `Crew`, `ShipConfig`, `SkillPoint`, `ShipStats`, `Stack`, `MissionProgress`, `Achievement`, `Keybind`, `Cosmetic`. Own guild rows. |
| On entering map X | `ShipState`, `NpcState`, `Effect`, `Wind`, `ObjectiveState`, `Beacon`, `Tower`, `Ping WHERE MapId = X`; `Marker WHERE RaidId = mine`. Unsubscribe the previous map. |
| In a duel | Client renders only the two `ShipState` rows with `DuelId = mine`; the server still sends the map's rows but marks the duelists; the client hides everything else (fog). Server-side, `Fire` rejects any target outside the duel. |
| In arena | `ArenaMatch WHERE Id = mine` plus the arena map's rows. |
| Never | Private tables. Other players' `Player` rows are not needed: `ShipState` carries name, rank, guild, flag, Wanted. |

Interest management is by map only. If one map exceeds about 200 concurrent ships, split the map into 2–4 subscription cells by sector range.

---

## 8. Stat computation and the balance tests

`RecomputeStats(configId)` runs when gear, crew, skills, or hull change, and after `HotfixConstant`:
1. Sum every bonus per stat from hull variant, plates, sails, figurehead, crew (with level scaling, one per role), skills, active buffs — in that order.
2. Clamp each stat to its cap (Math §2.2).
3. Compute Combat Power; if over 45, remove bonuses from the end of the order until ≤ 45; record `CombatPowerInactive`.
4. Add legendary edge (≤ 3 CP, outside the budget).
5. Write `ShipStats`, including `FightScore` against the Map Rank baseline.

**Balance tests** (Math §12) run in CI against the content JSON before every publish, using the same C# code as `RecomputeStats`: fight length per tier, exhaustive fight-score search (≤ 1.60; ≤ 1.63 with a legendary), ability ratio, ammo DPS, NPC solo safety, sea-effect bound, tree mixing and crew, economy drift, tower siege time (7–9 min), and Garrison pace. A failing test blocks publish.

---

## 9. Accounts, security, payments, admin

### 9.1 Authentication (Better Auth)
The web app runs **Better Auth** with these plugins and rules. Everything is required, not optional.

| Feature | Rule |
|---|---|
| Email + password | Argon2id hashes; minimum 10 characters checked against a breached-password list; **email verification required** before the account can log into the game or buy anything |
| Sign in with Google | OAuth; links to an existing account only after the email is verified on both sides; a Google-only account may add a password later |
| Passkeys (WebAuthn) | Any number per account; a passkey can be the only login method once email is verified |
| Two-factor (TOTP) | Optional for players, **required for admins** and for accounts with a legendary item or above Admiral rank; 10 one-time backup codes; a 2FA change requires re-authentication |
| Password change / reset | Reset by verified email link, 15-minute expiry, single use; change requires the current password or a fresh passkey/2FA step; all other sessions are revoked on change |
| Sessions | Server sessions in Redis via Better Auth's session store, 30-day sliding expiry, per-device list in the account page with "sign out everywhere" |
| Rate limits | Login 10 / 15 min per IP and per account; verification and reset emails 3 / hour; enforced in Redis |
| Email change | Requires verification of the new address and a notice to the old one; 24-hour undo link |
| Account deletion | Self-service with 14-day grace, then erased in PostgreSQL and anonymized in the game module (name → "Deleted Pirate", ledgers kept) |
| Phone (SMS) | Twilio Verify; stored as a flag; required for arena above Mate and for the Diamond market |
| Game identity | On game launch, the web app issues a short-lived OIDC token from the Better Auth session; SpacetimeDB derives the `Identity` from it. Revoking the session disconnects the client within one minute. |

PostgreSQL tables owned by Better Auth (`user`, `session`, `account`, `verification`, `passkey`, `twoFactor`) plus ours: `player_link` (user → SpacetimeDB identity), `phone_verification`, `device`, `purchase`, `purchase_lock`, `support_ticket`, `admin_audit`, `admin_role`.

### 9.2 Payments (Stripe)
Stripe Checkout → webhook (`checkout.session.completed`) → `purchase` row → worker calls `GrantDiamonds`. Idempotent on the Stripe event id. Chargeback (`charge.dispute.created`) → `RemoveDiamonds` (balance may go negative) + `purchase_lock` until settled; a second chargeback bans purchases permanently. Diamonds are final; refunds only through store policy. Sea Pass is a one-time purchase per season, not a subscription. Age gate at checkout per local law. Receipts and invoices from Stripe; VAT handled by Stripe Tax.

### 9.3 Diamond market
The order book lives in the module (`MarketOrder`). The web app displays it and lets a verified account post or buy; matching is inside `BuyDiamonds` (best price first, partial fills, seller identity never returned, ±20% band from `MarketStats`).

### 9.4 Website (`sea.com`)
Pages: landing and news · sign up / login (Better Auth) · account security (email, password, passkeys, 2FA, sessions, devices, phone verification, deletion) · store (Diamond packs, cosmetics, Sea Pass, Stripe Checkout) · Diamond market (post and buy orders) · player profile (public stats, ranks, duel record, Honor items, Hall of Fame) · guild pages (roster, level, islands, league standing) · leaderboards (players, guilds, alliances, world boss) · legendary auction page · support tickets · legal (terms, privacy, refund policy) · `play.sea.com` launches the Unity WebGL client with the session token.
Everything a player needs outside the client is here; nothing here can change combat, gold, Honor, or rating except through the module's own reducers (purchases via `GrantDiamonds`, market via `PostMarketOrder`/`BuyDiamonds`).

### 9.5 Admin app (`admin.sea.com`)
A separate TanStack Start application, deployed on its own, on its own domain, with its own database role. Access: staff accounts only (`admin_role` in PostgreSQL), mandatory 2FA, IP allow-list or VPN, short sessions (8 hours), and no cookie or session sharing with `sea.com`. It has two data sources:
- **PostgreSQL** (Drizzle): accounts, purchases, tickets, audit, the nightly analytics warehouse.
- **SpacetimeDB live** (TypeScript SDK with an admin identity): the admin app subscribes to public tables and to the private ledger and anti-cheat tables through admin-only subscription views, so every number is the real one, not a copy.

| Area | What it shows / does |
|---|---|
| Overview | Online players, ships per map, tick time per map, reducer latency, queue times, fights in progress, median fight length by tier (target 35–50 s), Honor paid today by source, gold created vs destroyed today, market average price |
| Players | Search by name, email, identity; full public state, configs, ShipStats with Combat Power breakdown, ledgers (Honor, Renown, kills with rule reasons), trust score and events, related-player list with reasons, sessions and devices, purchases; actions: ban, unban, shadow flag, clawback, Diamond adjust with reason, force logout, retire legendary |
| Live map | Every map as a live minimap: ships, NPCs, fights, beacons, towers, supply levels; click a ship to open the player |
| Economy | Gold in/out by source, ammo consumption, kit usage, hull and cannon sales, market volume and price band, daily-cap hits, per-map NPC gold, inflation indicators |
| PvP | CR distribution, rank counts, arena queue health, Guild Arena divisions and matches, family-match share, reciprocity list, blocked payouts by rule |
| Guilds | Renown, levels, members, contribution, island ownership and supply, war history with contested flags, alliance graph |
| Anti-cheat | Trust score histogram, honeypot hits, input-timing outliers, device links, pending reviews with replay viewer, weekly ban wave preview and approval |
| Content | Hotfix `StatCaps` constants (runs the balance tests first and shows results), spawn an event, set the hot map, post an announcement, set up a legendary auction |
| Support | Tickets linked to player pages; canned actions |
| Audit | Every admin action from `AdminAction` and `admin_audit`; two-person approval queue for clawbacks above 10,000 Honor and Diamond grants above 5,000 |

Metrics that need history (tick time, fight length, Honor per day, supply) are sampled every minute by the worker from the module into PostgreSQL `metrics_*` tables so the dashboards have charts, while the current value always comes live from SpacetimeDB.

Admin actions never call the module from the browser directly: the admin app posts to the worker, which validates the admin role and approval rules, calls the admin reducer, and writes `admin_audit`.

### 9.6 Replays
The module writes a compact per-fight event log (positions every 500 ms, shots, heals, abilities) to a blob store keyed by `ReplayRef`; kept 30 days, 90 days if referenced by a report, ban, or ticket. The admin replay viewer and the player's own "last 5 fights" viewer are the same React component.

## 10. Anti-cheat implementation

- All rules from Mechanics §19 and §19A are reducers or scheduled reducers; nothing runs client-side.
- `Fire`, `MoveTo`, `SelectTarget` append to `InputSample` (interval since last input, per session, rolling 200). `DailyReset` computes `TrustScore`.
- Phantom NPC: `Tick` inserts an `NpcState` with `NpcDefId = 0` for 5 s on a random map every few minutes; the Unity client never renders def 0; `SelectTarget` on it writes `TrustEvent(Honeypot)`.
- `RelatedCache` is the single function every payout calls: `IsRelated(a, b)` → bool + reason. It reads guild, alliance, party, friends, guild history (14 days), device links, invitations, and the reciprocity list.
- `HonorLedger.RulesPassed/RulesFailed` make every payment explainable in the client ("No reward: related · probation").
- Soft checks are missions of type `SoftCheck` generated by `DailyReset` for low-trust accounts.
- Shadow flag: `OnNpcKilled`, `OnPlayerSunk`, and all Honor/CR paths check `ShadowFlagged` and pay nothing while still returning normal client messages.

---

## 11. Environments, deployment, observability

- **Environments**: `dev` (local SpacetimeDB), `staging` (SpacetimeDB cloud or self-hosted, PostgreSQL, Redis, Stripe test), `prod`. Four deployables: game module, Unity WebGL build (CDN at `play.sea.com`), website (`sea.com`), admin app (`admin.sea.com`), plus the worker service.
- **Deploy**: content JSON + C# module → `spacetime publish` after CI passes the balance tests and schema migration checks. Unity WebGL build → CDN. Website, admin app, and worker → separate containers; the admin app is never exposed on the public load balancer.
- **Migrations**: additive table changes only during a season; breaking changes at season boundaries with a maintenance window.
- **Metrics**: tick duration per map, reducer latency, subscription fan-out, concurrent ships per map, fight-length distribution by tier (should center on 35–50 s), Honor paid per source per day, related-blocked payouts, market average price, supply levels per island, queue times.
- **Alerts**: tick > 80 ms sustained; fight length median outside 30–55 s; Honor paid per day > 2× the previous week; any balance test regressions after a hotfix.

---

## 12. Build order

**Phase 1 — one map, one fight (1/1)**
Content tables + `Init`; `Player`, `Hull`, `ShipConfig`, `ShipStats`, `RecomputeStats`; `Tick` at 100 ms with wind; `MoveTo`, `SelectTarget`, `Fire`, `StartRepair`/`FinishRepair`, `UseKit`, `Effect`; Unity client: sea, ship, click, Q, R, ammo slots, HP bars, keybinds. Test: base 1v1 lasts 33 ± 4 s.

**Phase 2 — progress and safety (1/2, 1/3)**
`SpawnPoint`, `OnNpcKilled` with damage share, missions, charts, Map Rank; Cannons and Armor trees; crew hiring; Harbor Protection dialog, flag, duels with fog; `Fight`, `KillRecord`, Combat Rating rules; `IsRelated` and the Honor ledger from day one.

**Phase 3 — competition and trust**
Arena on own ships with free ammo; worker matchmaking over Redis; `TrustScore`, phantom NPCs, `DailyEarnings`, `BanWave`; Sails, Repair, Plunder trees; Ship Configs; boarding with hands and lockers.

**Phase 4 — guilds and world**
Guilds, Renown, levels, contribution payouts; Guild Arena league with family matches; islands with towers, war windows in bands, Garrison Supply; alliances; maps 2/1 → 5/1 one biome at a time with NPCs from the derivation table.

**Phase 5 — money and operations**
Stripe, Diamonds, cosmetics, Sea Pass, anonymous market, legendary auction, admin panel (live SpacetimeDB views, metrics history, approvals), replay viewer.

Better Auth with email verification, Google, passkeys, and 2FA is part of **Phase 1**, because the game identity depends on it.
