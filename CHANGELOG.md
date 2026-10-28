# THE VALLEY — Changelog
> DOC-2026-10-056 · V1.0 · INTERNAL · Johan Wilhelm van Antwerp · Ionity (Pty) Ltd — AEDI · Policy 986 AED

## v4.14 "TITANS & ULTIMATES" / "FIELD OF HONOUR" — 28 Oct 2026
**Titans (a)** A massive world boss rises every 5 minutes (first after 2 minutes): 2.5x a man's height, champion level + 3 (max 999), 18x the champion's health. It pays out 3 Epics, a 35% chance of a Legendary at item level + 3, gold and XP, and is announced in /w on a World Server. Item and hero levels are capped at 999. `-titannow` and `-notitans` control it.
**No free spells (a)** Every skill costs 8–30% of the pool (at least 15). Ranged basics cost 3%; melee swings stay free. Friendly NPCs speak through the AI companion.
**Looks (b, c)** Male rogue keeps Feng Mao. Female rogues wear the hooded Kallari (no space-age skins). Paragon humans are sized by the head bone. The Serath wing bones are hidden.
**Music (d)** Every track in the music folder is used. A discovery stinger plays on entering a new zone (at most once a minute).
**Text (e)** Personal motto removed from the title and loading screens ("Building Tomorrow, Today.").
**Warlock (f)** Howl of Terror replaces Shadowfury: instant AoE fear within 8 m for 5 s (2 s on bosses). Drain Life is a 3 s channel. Shadow Bolt can be held up to 3 s longer for a bigger throw, +70% damage and a cleave onto 2 more foes.
**War and craft (h)**
- **Warrior Rage:** 0–100, rests at 30 out of combat. Hits build it, faster the longer the streak (3 + 1 per hit within 2.5 s, up to +8), and blows taken add more. Charge is free and gives +18. Other skills cost 8–40 (Cleave 9, Thunder Clap 14, Whirlwind 26, Shield Wall 28, Earthquake 38). Red rage orb and bar.
- **Whirlwind** applies a stacking Bleed (6 s, up to 3x).
- **Charge** is a sprinting rush with dust and a triple trail, ending in a heavy overhead blow with a shockwave, harder knockback and a bigger shake.
- **Rogues** do +50% damage from behind, with +25% crit chance there.
- **Casters** walk at 35% while casting. No spell can be cast while backing away, and backing away cancels a cast or a standing channel.
- A skill you can't afford now says NOT ENOUGH RAGE / MANA.

**Whirlwind holds (i)** A moving channel is no longer cut by another key press or a quick double-tap.
**Ultimates — level 25, key X, gilded button above the bar (j)**
- **Warrior – Warsong:** +25% attack speed and +10% damage for you and allies within 40 yd, until death or 10 min. 5 min cooldown.
- **Warlock – Felhound (passive):** a fel-green hound pet that fights your foe and silences a random enemy caster for 3 s every 30 s. X sends it at your target. It returns 20 s after it falls.
- **Paladin – Guardian's Light (passive):** a blow that would drop you below 1% instead makes you immune for 10 s, heals you fully and burns foes within 30 yd for 30% weapon damage every 2 s. An instant rising on the spot, nothing lost, once every 20 min.
- **Ranger – Sniping Shot:** 75 yd, 450% weapon damage, 2 min cooldown, 5% chance to cost no cooldown. Rangers also get a passive +5% run speed.
- **Rogue – Avoid-Dance:** spells reflected for 4 s, attacks parried with 50% recoil for 6 s, +10% attack and run speed for 15 s. 3 min cooldown.
- **Cleric – Raindance:** 15 s of acid rain within 40 yd while moving freely. Foes lose 2% of their health a second and allies heal 2% a second. 3 min cooldown.
- **Mage – Pyroblast:** instant, 220% damage, 12 s cooldown, 5% chance to cost no cooldown.

**Class-themed HUD (j)** Unit frames, spell slots, action bar, panels and the minimap ring wear the calling's own metal and ornaments (Content/Valley/UI/Hud/class). Each class has its own ultimate icon and frame. `-classichud` keeps the bronze. The art is a placeholder pass; a Canva art pass replaces it file for file.
**Server and download** The World Server runs on Google Cloud, the client download is on Google Drive (4 GB parts with JOIN.bat), and ionity.fun links the client only.

**Specials — level 16, key H, the button left of the ultimate (m–o)**
- **Warrior – Berserker Rage:** 12 s of double rage, and stuns can't hold you. 60 s cooldown.
- **Paladin – Divine Ward:** absorbs every spell for 3 s. 30 s cooldown.
- **Rogue – Killing Spree:** blinks behind up to 5 foes within 25 m and Ambushes each for 225% weapon damage. 45 s cooldown.
- **Mage – Mana Shield:** toggle; 2 mana absorb 1 damage, and it drops when your mana runs dry.
- **Ranger – Spirit Wolf:** pet that does 1/5 of the ranger's damage and taunts what it bites. H sends it at your target or calls it back; it returns 20 s after it falls.
- **Cleric – Fusion:** chain heal on up to 5 allies (−20% per jump) plus chain lightning on up to 5 foes (−18% per jump). 22 s cooldown.
- **Warlock – Shadow Clone:** mirrors every spell on the same target for 50% damage and follows you as you move; lasts 1 min or until slain. 2 min cooldown.

**Balbyter (m)** The warlock's level-25 pet is now Balbyter: Paragon's spirit-wolf body in fel green, with bite and leap animations. Its bite cleaves 20% to foes beside the target. The Spirit Wolf uses the same body in silver.
**Canva art (j–m)** Ultimate and special icons and the class frame materials are painted with Canva AI. Every frame (portrait rings, spell slots, plates, action bar, panels, minimap ring) wears its calling's Canva material. Sources are in Scripts/v414/canva_source.
**Tuning (k)** Blows taken give 0.5–6 rage; Whirlwind pulses build a third of a normal hit.
**Tests** Every class's level-16 and level-25 skills were tested in game (`-fightshot -chanfight -chanfaction=F -chanclass=N -ulttest`).

**Roads, grips and setup (p–u)**
- Roads are never walled: 86 palisade stakes standing on roads are cleared at load, and town gates are as wide as the road.
- Drain Life heals the caster for 100% of the damage done. DRAW DISTANCE slider in the settings.
- The runner is server-only: PLAY ALONE and HOST A WORLD SERVER are gone (account data comes from Firebase through the server).
- Portable `TheValley_Setup.exe` on Drive: joins the 4 GB parts, unpacks the game, makes a desktop link and starts it (no .bat).

**Titans (s–v)** A titan rises every 4–6 minutes, one at a time, with warnings 30 s and 10 s ahead sent to both sides. Every 25 minutes the Mega Titan "Oerklou" rises (4x size, 60x health, 5 Epics + a Legendary). Titans show on the minimap (on the rim with their distance when out of range) and on the map. Titan pathing fixed: they walk the terrain and no longer catch on walls.

**Field of Honour (w, x)**
- **Top of the screen:** compass, war bar, wave strip, town panel and the titan's boss bar share one centre line (the screen's centre whenever the unit frames leave room). The war bar sits in a bronze frame with faction gems and a ROUND plate. The boss bar hangs under the stack and never covers the town panel.
- **Kill feed** on the left: KILLER > VICTIM in faction colours (x and +5 for a player kill), fading after 10 s.
- **Scoreboard and MVP** beside the minimap: a player kill is worth 5 points and any other kill 1. Columns: player kills / other kills / deaths, then the score.
- **Slain by:** when you fall, your killer laughs in their own voice and a SLAIN BY plate slides in on the left with their name and level. Online, every death is shared on a hidden channel, so all feeds and boards agree.
- **Death costs a level** (health −40, damage −4, mana −8). `-nolevelloss` turns it off for tests.
- **Battle barks:** your champion says "That hurt!", "I need healing!", "Stay down!" and "I feel stronger!". Your troops answer G orders with "Yes, commander!" and "Okay, let's go!". The Kernel camp folk speak Afrikaans ("Ja, kommandant!", "Reg so, kom ons gaan!"), and orcs growl.
- **Wolf Shot:** the ranger's Spirit Wolf shot is renamed. It keeps the same wolf icon, howl and bite.
- **Male ranger:** the longbow is in the outstretched right hand, gripped in the palm along the knuckles. The sword, shield and the Ashclaw hunter's twin axes are gone.
- **Female paladin:** the mace stands upright in her fist.
- **Ashclaw war-woman:** Terra in Mountain Forge armour with an orc's green skin. Tusks are placed from the actor's own facing (they floated beside the head before).
- **Story folk** who spawned inside a wall or door step out to the nearest free ground (Elixir Peddler Mira at the Crossroads).
- **Fix:** the bow and tusk parts no longer reuse stale engine assets or component names (a crash when switching callings in the forge).

**Updater (client)**
- The runner checks Firebase (`release/client`), then GitHub (`Ionity-Global/the-valley-releases/latest.json`), for a newer Valley.
- UPDATE downloads the patch chain (Drive first, then GitHub Releases), checks each SHA-256, and installs only after the whole patch is verified. It then restarts itself.
- A World Server running a newer version, or a client below `minVersion`, must update before PLAY.
- `version.txt` beside the runner records the installed version (the setup writes it). The release tool `tools/valley_release.py` builds and publishes patches, and the GitHub Action validates every release.

## v4.13 "GATHERING" — 28 Oct 2026
**World Server (server side)** `The Valley Server.exe` - dedicated TCP server (VAL2 protocol): champions 10x/s per world, /s say cut to 40 m on the server, /w, /p, /t whisper, parties of 10, Google check of every Valley account, flood / chat / number limits, bans, idle time-out, public SERVERS listing, LAN discovery (UDP 7778), painted admin window or headless (Windows, mono, Docker). 25-check protocol test.
**Runner (client side)** `The Valley.exe` - painted server browser (public / LAN / recent), TCP ping, PLAY ON SERVER, PLAY ALONE, HOST A WORLD SERVER, windowed + log options, server password.
**Game (patch a)** ValleyNet: World Server client (-server=, JOIN address, /connect), reconnect, refused / kicked messages; chat commands /invite /accept /decline /leave /kick /party /who /t /world /disconnect /ping; whisper channel; SERVERS tab joins dedicated servers by address. v4.12 Firebase presence kept behind -presence.

## v4.11 "FIRST LIGHT" — 27 Oct 2026
**Combat (j)** Channelled signature skill per class (Whirlwind 3 s spin and more), SIGNATURE row-4 talents that reshape
them, parry for every class (12% cap) with Riposte / Deflection, PARRY text and chat lines, parry on the character sheet.
**HUD (j)** Health and mana orbs, darker action bar with an ember seam, draining channel bar, four signature talent rows.
**Smarter foes (l)** Kick / counter-spell long casts, wounded fall back to their healer, melee spread round the target.
**The Sage (l)** `/ai` AI companion: Ollama (local, default) or the player's own Claude / Gemini key via ValleyAI.txt.
**Host mode (l)** `-valleyhost` lists a listen host on the SERVERS tab every 60 s. Version 4.11 on the title, credits, footer, project.
**Release (n)** Cook-everything packaging, `package_v411.ps1`, Play / Host launchers, SETUP.txt.

## v4.10 "THE HOUR" — 26–27 Oct 2026
**Match (a)** One-hour rounds, 25 s harbour gate and horn, level-999 Commander and Warchief guarding sealed harbours,
war resources with income per second at the top centre, early end at 0, world reset with levels kept, auto-continue
into the next round. Scrolls of Travel (75 gold) for fast travel and the road home; SHOP and ARMOUR badges over
vendors; Piet and Gorrum sell armour.
**Accounts (b)** Firebase sign-in, cloud saves for four champions (every 60 s and on quit), local saves uploaded on first
sign-in, session refresh. Offline mode removed from the title screen. Firestore rules deployed.
**Chat (c, d)** Chat line on Enter or `/`; `/s` `/w` `/p` `/help`; nine emotes using each model's own clips.
**Settings and title screen (e–h)** Per-group graphics, GPU page (AA method, render resolution, frame limit, texture
memory, Lumen, motion blur, FPS counter), display (resolution, gamma, FOV), HUD size 70–160 %; NEWS / SERVERS / LOBBY
column with live server list and lobby chat; lobby Firestore rule; menushot covers every new page; footer v4.10.
**HUD layout (i)** Top-centre column stacked (compass → war bar → wave strip → town panel) and kept between the target
frame and the minimap; HUD scale capped to the screen width; FPS counter above the menu bar.

## v4.9c "STILL WATERS" — 26 Oct 2026
Road-aware pond shores, Meuldam moved and joined to the Wesstroom, fishing water height fixed, waterfall foam and
shimmer discs removed, Unreal Water plugin wired in (opt-in).

## v4.1 – v4.9
Stable ground, classic MMO HUD, voices, fire and light, warbands, war path, boons, scaling, coats for the wildlife —
see the release notes in the project docs.

---
**BUILDING TOMORROW, TODAY**
