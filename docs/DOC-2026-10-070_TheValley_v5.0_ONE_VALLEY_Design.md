# Executive Summary

THE VALLEY 5.0 "ONE VALLEY" turns the game from many private copies of the same map into one shared world, and gives a new player a reason to stay past the first ten minutes and a veteran a reason to come back tomorrow. Four pillars were chosen on 30 October 2026 from a six-point recommendation: (1) one shared world on the World Server, (3) the first ten minutes, (4) the endgame loop around the King of the North, and (5) a visual pass that brings the water, the icons, the map and the HUD to a 2026 standard. Guilds (2) and the build pipeline (6) are deferred to 5.1, though the pipeline items that unblock 5.0 are listed in the build order.

Every pillar builds on what already ships in 4.19: the VAL2 World Server (TCP 7777) with its shared Titans, the CLOCK and POINT lines, the per-client King of the North and his waves, Paragon levels, Legacy items, and the procedurally drawn icon set. Nothing here needs a new engine, a new server host or a paid asset.

Governance: Policy 986 AED, License AED 900. Author: Johan Wilhelm van Antwerp, Ionity (Pty) Ltd, AEDI. Reference site: www.ionity.co.za.

## Where 4.19 stands

- Live: 4.19.2 "Iron Crown" (30 October 2026). 4.19.3 in build: the King of the North, Legacy of the North, .ampies, the guardians' hunt, player glow, minimap zoom, fair-fight creeps, PvP level protection, Town Portal, calm shadows, capture rewards, lasting blessings.
- Shared on the server today: players (S / E state lines, the Act field), PvP hits (DMG), duels, chat, parties, Titans (TITAN / MHP / MOWN / MPOS / MDEAD), towns (POINT / CAPT), the clock and the weather.
- Per client today: the King of the North and his host, player-built towers, every ordinary mob, gather nodes, loot, rare spawns, the horde's scaling.

## What "done" means for 5.0

- Two players standing together see the same King, the same wave, the same tower, the same mob at the same health.
- A new player reaches level 5, captures a town and reads the map inside ten minutes without asking anyone.
- A party of three can clear the northern raid in 25 to 40 minutes and each leaves with a Legacy piece; the raid resets weekly.
- The river, the sea, the icons, the realm map and the HUD skin pass a screenshot review at 1080p and 4K.

# Pillar 1 - One Valley: the shared world

## Principle

The World Server owns everything that two players can both see and fight: it decides when a thing exists, where it is, how much health it has and when it dies. Each game draws the thing and drives it only when the server names that game the owner (the Titan pattern from 4.17: owner elected every 2 s, MPOS 5x a second within 300 m). Loot and experience stay per player, as with Titans: everyone on the hitters list is paid in his own game.

## Scope

- The King of the North and his camp guards: one per world, spawned by the server at the camp, health on the server, abilities driven by the owner's game and mirrored by the Act field.
- The host of the North: each wave is a server spawn (WAVE line), its six bodies shared (MOB lines), its target town chosen by the server from the live POINT table.
- Towers: a player-built tower becomes a server object (TOWER line) so every game shows it, its shots and its fall. Town and citadel towers already exist in every client and keep their local target filter; their health moves to the server.
- Ordinary mobs: shared in zones where players meet - the 20 roads' creep packs, the camps around towns, the Grotte, the north. Wildlife, far-sleep fauna and quest cast stay local (they never fight two players at once).
- Gather nodes: a node's respawn clock moves to the server so two players cannot both harvest it; the harvest itself stays local.

## Wire protocol (additions to VAL2)

- `MOB|id|kind|x|y|z|level|hp|maxhp|owner|seed|tag` - a shared body exists (kind = ModelId, tag = king / guard / wave / pack / tower).
- `MPOS|id|...` and `MHP|id|hp|by`, `MOWN|id|sid`, `MDEAD|id|killer|hitters` - reused unchanged from Titans.
- `MACT|id|seq|idx|rate|school|x|y|z` - the body's current action (the Act field of a player, applied to a mob) so the King's smash and toss show in every game.
- `WAVE|id|from_x|from_y|town_id|level|bodies` - a wave is born; the bodies follow as MOB lines.
- `TOWER|id|owner_sid|faction|tier|x|y|z|hp|maxhp` and `TDEAD|id`.
- `NODE|id|ready_at` - a gather node's clock. Interest management as for Titans: lines within 300 m of the receiver, plus the King and towers at any range.

## Server (ValleyServer.cs)

- A `MobDirector` per world, modelled on `TitanDirector`: the King's spawn and 30-minute respawn, the wave clock (every 3 minutes), owner election, hit capping (6 % of max per blow, as Titans), death and payout lists.
- A `TowerTable` per world: towers persist until destroyed or the round ends.
- Round reset: everything shared is cleared with the world; the King's camp is rebuilt at the gate.
- Tests: `test_mob_50.py` (spawn, owner election, hit cap, death, interest radius), `test_wave_50.py`, `test_tower_50.py` - all must pass before deploy, as `test_titan_417.py` does today.

## Client

- `ValleyShared.cpp`: one registry of shared bodies by id (today split between ValleyTitans and ValleyNet); `AValleyMob::bNetShared` / `bNetOwner` already exist and are reused.
- `ValleyTitans::KingTick` becomes server-driven: the owner's game runs the abilities and sends MACT; the others play them.
- Towers: `AValleyTower` gains `NetId` and a mirror path; `PlaceTower` sends `TOWER`, every game builds it on receipt.
- Fallback: with no World Server (offline, LAN) everything behaves as 4.19 does today.

## Risks

- Bandwidth: 50 players x 6 wave bodies x 5 positions a second is too much without interest management - the 300 m rule is not optional.
- Owner hand-off mid-fight: the Titan path already handles it; the King's telegraphs must carry their start time so a new owner continues the cast.
- Cheating: hits stay capped per blow and the server refuses hits from a game that is not within range of the body.

# Pillar 3 - The first ten minutes

## Problem

A new champion spawns in a harbour on a 1,200 m map with forty systems, twenty keys and no guide. The fun (a capture, a Titan, a duel) is fifteen minutes away and nothing points at it.

## The landing (0 - 3 minutes)

- A guided first quest chain from the harbour elder, one step at a time, each step a single key: move (WASD), look (right mouse), fight a training dummy (left mouse / 1), loot a chest (F), drink the potion it gives (R), open the bag (I), open the map (M).
- Each step highlights the key on the bar and the target in the world (the Watchstone ring already exists; reuse it).
- The chain ends at the harbour gate with the horn: "The valley is yours. Take the road north and capture Kranskop Uitkykpos."

## The first town (3 - 10 minutes)

- The nearest neutral town to each harbour is guaranteed neutral at the gate and lightly held (two creeps of the player's level).
- Capturing it pays the +2 levels of 4.19.3 and opens a second chain: talk to the vendor (the shop), craft a potion at the brew table, read the Scroll of Travel, join World chat (/w).
- A first Titan warning is scheduled 10 to 12 minutes after a new champion's gate so the first big fight happens near the first town.

## Settings on first run

- The graphics preset is chosen from the GPU name and VRAM (RTX 40 / 50 = Epic, 30 = High, 20 / GTX = Medium, below = Low) and shown once with "Change in Settings".
- Resolution = desktop in borderless; mouse look lock on; the tutorial can be skipped and re-run from Settings.

## Measures

- Log `[THE VALLEY] v5.0 tutorial step n at t s` for every step; the first-run log is sent with the HELLO as `tut=n` so the server can count how many finish.
- Target: 80 % of first runs reach the gate step; 60 % capture a town inside ten minutes.

# Pillar 4 - The endgame loop

## The Frozen Court (the northern raid)

The King's camp (about 600, 1110 m) becomes a raid zone with three bosses, shared on the server (Pillar 1):

- The Warchief of the Dead (level 150): a Sevarog body; summons thrall packs, a fear howl, a soul-drain beam the party must break with damage.
- The Frost Witch (level 175): a Morigesh body; blizzard pools that move, a frost nova that freezes whoever stands still, heals the Warchief if both live.
- The King of the North (level 200): as in 4.19.3 (Frost Smash, Glacier Toss, Blizzard Aura, Enrage), plus in 5.0 a throne phase at 50 %: he sits, the camp's frozen guards wake, and Legacy shards on the ground must be carried to the throne to break the ice.

## Lockout and loot

- One Legacy piece per boss per week per champion (reset Wednesday 06:00 SAST, by the server clock): the Warchief drops hands / feet / ring, the Witch head / shoulders / neck, the King weapon / chest / legs / off hand / trinket.
- Legacy tiers: a piece dropped again upgrades to tier 2, then 3 (+25 % on its stats and its special each tier; the name gains "of the Deep Winter", "of the Endless Winter").
- The King's chest also holds a Paragon shard: +1 Paragon level, once a week.

## The Sunken Court (the southern raid, 5.0 stretch or 5.1)

- A wreck off the Lake Coast with a kraken (a Fab sea-creature body if one is free; else a built mesh); two bosses; water-themed Legacy pieces that share slots with the north so the two sets compete.
- The two factions race: the first side to clear a court in a week earns a town buff for both of its harbours for the next week.

## Weekly ladder

- Per champion and per side: towns held (minutes), Titans slain, bosses slain, Paragon levels gained; shown on a LADDER tab in the scoreboard and in the harbour on a stone board.
- Resets with the lockout; top three names carved on the harbour board for the week.

# Pillar 5 - The valley in 2026

## Water

- The Ionflow, the streams and the ponds today are three flat meshes with a single cyan colour, visible seams and steps between them, and a hard line against the bank.
- 5.0: one water material with depth colour (dark green-blue in the channel to clear at the edge), a depth fade against the bank, shore foam, flowing normals along the river's direction, and reflections at Epic. The side streams and ponds are rebuilt to sit a hair below the river where they join; the placeholder cones in the water are removed.
- Option checked first: Unreal's Water plugin (already present in 5.8) for the river spline and the lake - used if it survives a performance test at Medium; else the custom material.

## Icons

- The 127 procedural icons of 30 October serve as the talent-tree and placeholder set; the spell, item and buff icons are redrawn as painted art (Canva once the connector is connected, else hand-painted in the pipeline) at 256 x 256, in one style: dark fantasy, gold frame, school colour.
- The HUD takes icons by name from Content/Valley/UI/Icons so a redrawn icon replaces the old one without code.

## The realm map

- A hand-drawn fantasy map of the valley (parchment, inked mountains and forests, the river in blue wash) replaces the satellite-style capture; the overlay (roads, towns, quests, allies) stays dynamic on top.
- The legend text fixed in 4.19.3 stays; the minimap gets the same art at its zoom levels.

## Sky and light

- The day is one real hour (4.19.3); 5.0 adds a Highveld sky: a sun that rises orange over the Ashclaw east, a golden hour, a Milky Way at night with the 25 % lighter floor of 4.19.3, and weather that reads at a glance (mist in the valley floor at dawn, a storm front that darkens the west first).
- Shadows: the 45-second glide stays; cascade distances by preset so Low never renders shadows beyond 80 m.

## HUD skin

- One skin for every panel (bag, shop, map, scoreboard, dialog): the stone-and-gold frame of the action bar; the class-themed Canva HUD of 4.14 is kept as the colour accent.
- Scale: the 720p width cap is replaced by a two-row top layout so small screens read.

# Build order (two sprints)

## Sprint 1 (weeks 1-2): the shared world and the raid skeleton

- Server: MobDirector + TowerTable + tests. Client: ValleyShared registry; the King and the waves on the server; towers shared. Offline fallback checked with -nonet.
- The Frozen Court layout: the camp, the three boss positions, the lockout record on the server (per account, from the Valley account id).
- Pipeline items that unblock this: chunk 2 for all new content, the cook never ignores errors (4.19.2 lesson), a nightly autoshot on E:\ValleyTest418.

## Sprint 2 (weeks 3-4): the first ten minutes, the loot, the look

- Tutorial chain and first-run settings; the first-Titan schedule; the tutorial counters on HELLO.
- The Warchief of the Dead and the Frost Witch; Legacy tiers; the Paragon shard; the ladder tab.
- Water material and the river rebuild; realm map art; HUD skin; the icon pipeline.

## Release

- 5.0.0 ships as a full install (the water, map and icon content change chunk 0) with a patch chain from 4.19.x for the exe and chunk 2; the World Server deploys first with `ClientVersion = "5.0.0"`.
- Verification: two-player session on the live server (same King, same wave, same tower), the raid cleared once by a party of three, the tutorial run by one person who has never played, screenshot review of water / map / HUD at 1080p and 4K.

# Deferred to 5.1

- Guilds, the guild hall and the guild bank (Pillar 2).
- The Sunken Court if it slips from Sprint 2.
- Housing, more mounts, a third faction, new classes.
- Trading, the gold economy changes (gold 4x harder, 15 % lost on death), the Ore and Gold Mine camps and the Stable requested on 30 October ship in 4.19.4, before 5.0.

# Appendix - what exists to build on

- Server: ValleyServer.cs (TitanDirector, world clock, POINT / CAPT, DMG, DUEL), ValleyRealm.cs; deploy_world_server.ps1 pushes to the valley-world VM (34.35.99.132:7777, africa-south1).
- Client: ValleyNet.cpp (peers, S / E lines, Act field), ValleyTitans.cpp (shared bodies, the King), ValleyConquest.cpp (towns, towers), ValleyRealmClient.cpp (POINT, pickups), ValleyItems.cpp (Legacy table), ValleyHUD*.cpp.
- Content: pakchunk2 for new Fab packs; Content/Valley/UI/Icons for the icon set; E:\TheValley_UE5\Art\Icons\v1 for the procedural set and its generator.
- Scripts: Scripts\v416 (patch_*.py exact-anchor patches, package_*.ps1, publish_patch.py, server_*.ps1, test_*.py).
