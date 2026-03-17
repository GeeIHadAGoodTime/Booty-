Booty! A Pirates Rise – Phase 1 Sub-PRD (Pre-Alpha Vertical Slice)

Phase: Pre-Alpha – Vertical Slice (P1)
CURRENT_PHASE literal: Pre-Alpha (Vertical Slice)
Master PRD: Booty! A Pirates Rise – Master PRD v1.3 (source of truth for invariants & schemas)

1. Phase Goal & Success Criteria
1.1 Goal (What P1 Delivers)

Deliver a vertical slice that proves the core fantasy is fun and readable:

Isometric arcade ship handling.

Naval combat with broadside arcs and basic AI.

Port capture that clearly changes ownership and income.

Simple income loop with at least one meaningful gold sink.

Player experience:

Within ~30–60 minutes, a player can Sail → Fight → Capture a Port → Get Paid → Repair/Resupply → Repeat in a small but coherent sea region.

1.2 Exit Criteria (Hard Gates)

P1 is done only when all of the following are true.

Playable Loop

At least one region with 3–4 ports.

Player has one ship and can:

Sail freely in an isometric world.

Engage in naval combat with basic enemy ships.

Capture at least one port via naval combat.

See that port change ownership and start paying income.

Economy Loop

Port(s) under player control generate recurring income in gold.

Player can:

View gold total in UI.

Spend gold on at least one meaningful sink (repairs/resupply).

Basic balance feels non-broken (no infinite money with zero effort, no impossible grind).

Progression

Renown (or equivalent fame metric) exists and:

Increases from combat and/or port capture.

Is visible in a simple UI.

Has at least one small gameplay effect (e.g., higher-tier enemies, different encounter chances, or higher bounties).

Technical Stability

Game can be:

Started from a fresh install.

Played for 30–60 minutes.

Saved and loaded at least once.

Without:

Crashes.

Softlocks that prevent continuing the loop.

Critical desyncs between map/port/ownership state.

Visual & UX Readability

Isometric camera is:

Stable.

Readable for navigation and combat.

Player can clearly see:

Which ports are enemy vs friendly vs player-owned.

Basic health/HP or ship status during combat.

Stretch content (bounties, tavern NPC, simple trade) is nice-to-have and not required to call P1 “done”.

2. Scope Summary (Phase 1 Feature Set)
2.1 World & Map

Core P1

World scale:

Single sea region.

3–4 ports (towns/outposts) with simple visual differentiation.

Limited open water allowing short sail times (no long empty travel).

Map representation:

Isometric world, with:

Simple ocean shader/plane.

Landmasses as low-detail islands or coastlines.

Ports represented as:

3D scenes or simplified dioramas (engine-dependent).

On-world map icons or markers visible from camera height.

Ownership:

Each port has:

faction_owner: enemy_faction or player_faction.

A simple defense_rating or tier.

One port starts as:

Player’s initial “home” (friendly port).

Others start as enemy or neutral.

Time:

Abstracted or simple tick-based time for:

Income generation.

Potential respawn timers.

No full day-night or calendar required in P1.

Encounters:

Ships appear in the world near:

Ports.

Defined “lanes” or spawn regions (if feasible).

2.2 Player & Ships

Core P1

Player:

Single captain identity:

Name.

Portrait (placeholder is acceptable).

No full character creator required.

Ship:

Exactly one controllable player ship in P1.

Ship stats:

hull_hp (hit points).

speed.

turn_rate.

broadside_damage or gun_slots.

cargo_capacity (minimal use in P1, mostly future-proof).

Crew:

Abstracted as a single scalar or ignored in P1.

No detailed crew management or morale.

Ship progression:

Optional small upgrades (e.g. “better guns” or “reinforced hull”) if cheap to implement.

Different hull/speed/turn_rate profiles (e.g. glass cannon vs tanky).

2.3 Sailing & Combat

Core P1

Movement:

Input: keyboard + mouse.

WASD or equivalent for thrust/turn; optional mouse-steering.

Movement constrained to 2D nav-plane.

No wind mechanics in P1 (wind scalar may exist in schema but inactive).

Combat Model:

Broadside arcs as 2D cones/sectors off port/starboard.

Simple 2D projectile travel (raycast or short-lived bullets).

Hit resolution:

Hull damage mandatory.

Optional sail damage if cheap to implement.

AI:

Enemy ships:

Simple state machine:

Patrol / defend near ports.

Aggro when player within range.

Chase and circle while firing broadsides.

No advanced maneuvers required (no tack against wind, etc.).

Enemy variety:

At least 1–2 archetypes (e.g. small fast, medium tough).

UI:

Player ship HUD:

Hull HP.

Maybe simple “reload” or “guns ready” indicator.

Crosshair or ghost arcs showing broadside firing sectors.

2.4 Ports & Ownership

Core P1

Port roles:

Each port has:

A faction_owner.

An income profile (e.g. base_income).

A simple defense_rating (used for combat difficulty or garrison size).

Capture flow:

Player engages enemy forces in naval combat near port.

Upon victory, player is offered to capture the port.

Capture may be:

Immediate after battle.

Or conditional (e.g. port defense meter reduced to zero).

Post-capture effects:

faction_owner updated to player’s faction (e.g. player_pirates).

Visual change on map (flag color, icon, banner).

Port begins paying recurring income to player.

2.5 Economy & Income

Core P1

Currency: Gold.

Prices:

Use base_price only from port schema.

No supply/demand dynamics, no event modifiers.

Income Streams:

Port income:

Each player-owned port generates gold per tick (e.g. per in-game day or week).

formula: base_income * port_level_modifier * global_scalar (all definable in data).

Combat spoils:

Gold reward from sinking or capturing enemy ships.

Spending (Minimum):

Repairs/resupply as the mandatory gold sink.

Optionally a simple “crew upkeep” scalar or “ammo purchase” if cheap.

P1 Stretch – Goods & Trade

Minimal goods system:

2–4 goods defined with id, name, base_price.

Each port shows a simple static buy/sell table.

Trade impact:

Profits are small relative to port income, primarily flavor for P1.

No dynamic prices, no events—just static base_price ± fixed scalar per port.

Implementation note (for AI/tools): Do not implement this stretch feature unless an explicit task says “Implement P1 Stretch – Goods & Trade” **and** all M0–M4 core milestones are complete and stable.

2.6 Progression & Renown

Core P1

Renown:

Single scalar: player.renown.

Increases from:

Sinking enemy ships.

Capturing ports.

Displayed in:

Simple UI element (e.g. under gold).

Effect:

At minimum, used as a gating or weighting factor for:

Encounter difficulty (optional).

Flavor text (optional).

Tiers (optional):

Simple thresholds like “Unknown”, “Notorious”, “Feared” if cheap.

Rank & Titles:

player.rank:

May exist as a simple string or enum but remains “nobody” or equivalent.

vassalage_contract:

Field present (per schema) but null/inactive in P1.

2.7 Quests & Content

P1 Stretch – Vertical Slice Polish

Quests:

1 bounty-style quest:

Example: “Sink/disable pirate ship X near port Y.”

Rewards: gold + renown.

1 rumor-style hook:

Example: “Rich merchants around Port Z” – mainly flavor, may slightly bias encounters or just serve as guidance.

Tavern:

Simple menu:

“Talk” → flavor text.

“Take bounty” → starts the bounty quest.

No mini-games, no complex dialog tree—just 2–3 lines and 1–2 choices.

If schedule slips, the bounty & rumor are the first stretch items to cut without violating P1 exit criteria.

Implementation note (for AI/tools): Treat this entire subsection as stretch-only. Do not implement it unless a task explicitly says “Implement P1 Stretch – Quests & Content” **and** the core Sail → Fight → Capture Port → Get Paid loop is stable.

2.8 Saves & Persistence

Core P1

Save/Load:

UX: Single slot + “Continue” is enough.

No manual save naming.

Auto-save on key beats (e.g. after port capture, on exit).

Data persisted (minimum):

Player location and ship state.

Player gold.

Player renown.

Port ownership.

Port income state (or derivable from base formulas).

Key port data used by P1 (e.g., faction_owner, defense_rating).

Format:

Single JSON-based save file per slot (no additional binary or engine-native format in P1).

Must not contradict Master schemas (fields are a subset, not a fork).

3. Explicit Non-Goals for Phase 1

These may exist in data or editor, but must not ship as live features in P1.

Present-but-inactive schema fields (e.g., wind_scalar, tax_policy, stability, war_state, vassalage_contract, player.rank, etc.) are **read-only scaffolding** for later phases. Tools may define them in data, but P1 gameplay code must not:
- branch on them,
- write to them at runtime, or
- include them in gameplay formulas.

The following systems are explicitly out of scope for P1:

No dynamic wars or war_score mechanics.

No tax_policy or stability gameplay (values may exist but are constants).

No fleet system (player controls exactly one ship in battle).

No convoys, blockades, or systemic trade routes.

No independence/kingdom mode.

No vassalage UI or real contracts.

No procedural quest framework:

Only up to 1 handcrafted bounty + 1 rumor (stretch).

No modding surface exposed to players (JSON/schema internal only).

No multiplayer, no LLM-driven dialogue.

No land battles, boarding minigames, or walkable interiors.

These mirror Master PRD non-goals but are repeated here as phase-level guardrails.

4. Detailed Requirements by Pillar (Phase 1)
4.1 Sailing & Combat (Core Pillar)

Camera & Controls

Camera:

Isometric, slightly top-down perspective.

Follow player ship with:

Soft follow or simple lock.

Optional mouse edge-pan or constrained camera if needed.

Zoom:

Fixed or small zoom range is acceptable.

Controls:

Keyboard:

W/S for throttle (forward/back or accelerate/decelerate).

A/D for turn left/right (yaw).

Optional strafe or fine-turn keys if helpful.

Mouse:

Optional aiming for broadsides (e.g., align ship relative to cursor).

Physics:

Arcade-style:

No realistic buoyancy, drag, or sail rig simulation.

Turning and speed:

Simple linear or curve-based acceleration.

Cap max speed and turn_rate.

Collision:

Ship vs terrain:

Simple collision volume around islands/ports.

Ship vs ship:

Optional bump/ram if cheap; otherwise avoid overlapping by simple separation.

Combat

Weapons:

Broadside cannons on port and starboard.

Fire arcs:

Defined as 2D sectors relative to ship heading.

UI:

Ghost arcs or targeting aid showing when enemy is in broadside range.

Firing:

Input:

Left/right click (or Q/E) for left/right broadside.

Cooldown:

Simple global or per-side cooldown.

Damage:

Hull HP reduced when hit.

Optional: different damage if hit in bow/stern vs broadside, if cheap.

Feedback:

Hit FX:

Simple splash or impact FX.

Sound:

Basic cannon fire and impact sounds.

Enemy AI

Behavior:

States:

Patrol:

Move along simple waypoints near ports.

Engage:

When player in detection radius.

Chase & Circle:

Try to maintain some distance and angle suitable for firing broadsides.

Disengage:

Optional; may fight to the death in P1.

Target selection:

Always target player in P1 (no friendly/enemy interactions needed).

Difficulty scalar:

Can be edited via enemy archetype stats (hp, damage, speed).

Spawning:

Enemy ships spawn near:

Enemy ports.

Defined “danger zones” between ports.

No complex spawn system required; simple timed spawns or seeded placements.

Death & Loot

On enemy death:

Ship sinks with simple animation.

Reward:

Gold reward (fixed or small range).

Optional chance to drop extra gold or flavor loot.

4.2 Ports & Ownership / Governance

Port Data

Each port has core fields (subset of Master PRD):

id, name.

faction_owner.

defense_rating (used for encounter toughness).

base_income.

port_level (optional scalar I–V for later phases; may be fixed in P1).

Optional: visual_style / biome tags.

Ownership States

States:

Enemy:

Hostile to player; enemy ships spawn nearby.

Friendly:

Starting port(s) that are safe and do not spawn hostiles.

Player-owned:

Captured from enemy; provide income.

Transitions:

Enemy → Player-owned via successful naval battle + capture.

Friendly → Player-owned:

Either considered equivalent or optional “formal capture” at start.

UI Representation

On world/map:

Port icons:

Color-coded by owner:

Enemy (e.g. red).

Friendly/neutral (e.g. gray).

Player-owned (e.g. gold).

Hover or click:

Shows:

Port name.

Owner.

Defense_rating (e.g. stars or numeric).

Income value (base or effective).

Capture Flow

Battle trigger:

Player enters port’s “defense radius” or interacts with “Attack Port” UI.

Battle instance:

Spawns appropriate enemy ships based on defense_rating.

Win condition:

Destroy/capture all defenders.

Post-battle:

If win:

Prompt: “Capture Port?” Yes/No.

“Yes”:

Set faction_owner to player faction.

Apply visual/UI changes.

Initialize income tick.

If lose:

Game over or respawn flow (simple for P1).

Garrison Abstraction

No land garrison system in P1.

Defense_rating stands in for garrison strength.

Higher defense_rating:

More or tougher ships in naval battle.

4.3 Economy & Trade

Port Income

Mechanics:

Each player-owned port generates gold per in-game tick:

gold_per_tick = base_income * port_level_modifier * global_scalar.

Income accumulated into player.gold.

Tick timing:

Simple global tick every X seconds or in-game hours.

UI feedback:

Income ticker:

Small message or icon when income arrives.

Optional ledger:

Simple panel showing:

Per-port income.

Total per tick.

Repairs & Resupply

At any friendly or player-owned port:

Dock option:

“Repair Ship”:

Restores hull HP to full.

Cost scales with:

Missing HP.

Global scalar.

No detailed breakdown needed (no individual hull planks or cannon repairs).

“Resupply”:

Optional: small cost and simple effect (e.g. ensure ammo is always available; may be abstracted away).

Trade (Stretch – see 2.5)

If implemented:

Simple buy/sell UI:

Shows list of goods with static prices.

Player can buy/sell within cargo_capacity.

No dynamic prices, no events.

4.4 Factions, Wars, Politics

Factions

At least two:

player_pirates (or equivalent).

enemy_empire (or equivalent).

Optional neutral faction.

Relations:

Simplified for P1:

player vs enemy: always hostile.

Neutral ports (if exist):

Either non-hostile or simple logic.

Wars & Politics

No dynamic wars in P1.

Wars are assumed static:

Player is “at war” with enemy empire.

No treaties, alliances, or vassal interactions.

This pillar is mostly schema scaffolding for later phases (see Master PRD), with P1 using a minimal subset.

4.5 Progression & Vassalage

Player Progression

Renown:

See 2.6.

Thresholds:

Optional tier labels.

Effects in P1:

Can influence:

Encounter difficulty (slight increase in enemy quality).

Flavor text (if stretch dialog exists).

Gold & ports:

Owning more ports naturally increases income; no direct renown → income tie required in P1.

Rank & Titles

player.rank:

Optional field; P1 can fix it to “Free Captain”/“Nobody”.

No Vassalage Yet

vassalage_contract and player.kingdom objects:

Exist (per Master schema) but remain null/inactive.

4.6 Quests & Dialog (P1 Stretch)
Functional

Quest Structure

Minimal quest structure sufficient for:

One bounty quest:

Target: specific ship or ship type in region.

Steps: accept → go to area → sink/disable target → return to giver.

Reward: gold + renown.

Simple quest journal:

List of active quests (max 1–2 in P1).

Basic state: Not taken → Active → Completed → Turned in.

Rumors:

Non-binding hints about:

Danger zones.

Wealthy trade routes (flavor).

Tavern & NPC

At least one NPC (tavern keeper or governor):

2–3 lines of dialog.

Options:

“Talk” (flavor).

“Any work?” → offer bounty quest if not yet taken or completed.

Again: this whole section is stretch. Core P1 loop must stand without it.

Implementation note (for AI/tools): Do not implement section 4.6 unless explicitly instructed (e.g., “Implement P1 Stretch – Quests & Dialog”) and only after core P1 systems (4.1–4.5) are implemented and stable.

4.7 Tech & Infra for P1
Engine & Settings

Engine selected and project initialized in Unity 6 LTS with URP, C# only, per Master PRD A2 and docs/ImplementationTopology.md.

Basic options:

Sound volume.

Fullscreen/windowed toggle.

Basic keybind remap (optional; hard-coded keys are acceptable for P1).

Save System

One working save-load path for:

Player state.

World state (ports ownership).

Single-slot or “Continue”:

No UI for multiple manual saves required in P1.

Debug Tools (Mandatory)

A small but mandatory debug surface (single console or F1 panel module) providing at least:

teleport_to_port(port_id) – move player to given port.

give_gold(amount) – add gold.

set_port_owner(port_id, faction_id) – flip ownership.

spawn_enemies(count, archetype_id) – spawn test enemies nearby.

All debug helpers must be wired through this single debug module. Tools must not create ad-hoc debug hotkeys or cheats scattered through gameplay systems; new debug actions, if needed, extend this surface.

These tools are required to quickly test capture, income, and combat loops.

World bootstrap

Core scene:

Isometric camera.

Placeholder ocean plane.

Data layer:

Initial schemas wired for ports, factions, player, ships (subset only).

Minimal UI:

Gold display.

Basic HP bar for ship.

Port ownership indicators.

Tech Implementation – P1 Architecture

P1 uses a single-scene, single-composition-root pattern: BootyBootstrap + GameRoot.

All core systems (Player, Economy, Ports, Save, Combat) expose explicit Initialize/Configure methods for wiring.

No system exposes public fields for cross-system dependencies; wiring is done in bootstrap only.

Any AI-driven changes to wiring must preserve this pattern and must not move gameplay rules into bootstrap.

5. Milestones / Work Breakdown

Note: Milestones are logical groupings for dev & AI planning, not marketing beats.

M0 – Engine & Core Loop Skeleton

Deliverables

Engine project initialized.

Isometric camera prototype working.

Player ship placeholder model.

Basic movement controls (sail around test scene).

Simple enemy ship that:

Spawns.

Chases player.

Fires dummy shots.

No damage or UI required yet.

M1 – Combat & Death

Deliverables

Combat loop:

Broadside firing implemented.

Hit detection and hull damage.

Enemy ship sinks when HP <= 0.

Player HP & death:

Player ship can be sunk.

Basic “restart battle” or respawn near port.

Enemies:

1–2 archetypes defined in data.

Minimal combat UI:

HP bar.

Reload/ready indicator.

M2 – Ports & Capture

Deliverables

World map:

3–4 ports placed.

Ownership state implemented (enemy/friendly/player-owned).

Port icons and simple on-world indicators.

Capture flow:

Naval battle near enemy port.

On victory, prompt to capture.

On capture:

faction_owner updated.

Visual change.

M3 – Economy & Income

Deliverables

Port income:

Tick system in place.

Player-owned ports generate gold.

UI shows:

Current gold.

Simple log or indicator of income gained.

Repairs:

At least one port offers “Repair ship for X gold”.

Gold cost scales with damage.

M4 – Renown & Loop Stabilization

Deliverables

Renown:

player.renown tracked and displayed.

Effects:

Optional small scaling of encounter difficulty.

Loop:

Player can:

Fight.

Capture ports.

Earn gold.

Repair/resupply.

See renown change.

M5 – Content & Vertical Slice Polish (Stretch-heavy)

Deliverables

Bounty quest + rumor/hook (if not cut).

Tavern NPC + dialog.

FX/audio passes for:

Guns, hits, and port capture.

Stability and feel pass:

30–60 minute loop tested for crashes/blockers.

Demo: new game → fight → capture port → earn/spend → complete bounty (if present) → save/load → continue loop.

Implementation note (for AI/tools): Treat M5 as stretch-heavy. Do not prioritize or implement M5 content unless explicitly requested and after M0–M4 exit criteria are already satisfied.

6. Risks & Guardrails
6.1 Main Risks

Scope creep into later phases:

Starting dynamic wars, fleets, convoys, etc.

Physics/sim creep:

Adding wind vectors, buoyancy, or detailed sailing behavior.

UI rabbit holes:

Overbuilding port/economy/quest UIs beyond what P1 needs.

6.2 Guardrails

Any feature not required for P1 exit criteria is:

Either explicitly tagged as P1 Stretch or

Deferred to later phases.

Any mechanic that would require:

Significant new AI,

Complex new UI flows, or

Schema changes beyond Master PRD,
→ must be deferred to Alpha or later.

Master PRD invariants (A-section, schemas) override this document if in conflict.

7. AI / Implementation Guardrails (Cursor, LLMs, Copilots)

This section specializes A7 (AI / Implementation Guardrails) from the Master PRD for Phase 1. If any text here seems to conflict with A7, treat the Master PRD as authoritative.

7.1 Scope of Work for P1

Tools (Cursor, LLMs, copilots) must:

- Only modify code, assets, and data needed to satisfy the **Phase Goal & Exit Criteria (1.1–1.2)** and the **Core P1** items in section 2.
- Treat everything in **3. Explicit Non-Goals** as a **hard ban** for P1, unless a future phase explicitly redefines the scope.
- Treat all items marked **P1 Stretch** and **Stretch-heavy** as off-limits unless a task explicitly instructs to implement that stretch item and core M0–M4 milestones are already met.

7.2 No New Parallel Systems

Tools must **not** introduce parallel or “v2” systems for:

- Saves / persistence.
- Ship movement or combat.
- Port or faction state.
- Quests or progression.

If new behavior is needed, it must extend the **existing** systems defined by the Master PRD schemas and this Sub-PRD, not create new code paths like `PortV2`, `ShipSave`, `CombatAlt`, etc.

7.3 Schema Discipline

- All game data types for P1 (player, ship, port, faction, quest) must use the **Master PRD schemas** as the single source of truth.
- Tools must **not** create schema variants or forks (e.g., `PortLite`, `PortP1`, `ShipSaveState`, etc.) in P1.
- New fields may only be added when:
  - The work order explicitly requests the new field, **and**
  - The change is mirrored back into the Master PRD schema definitions.
- Any field marked “present but inactive” in this document:
  - May exist in data structs.
  - Must be treated as **read-only scaffolding** (see section 3).
  - Must not have gameplay logic, runtime writes, or branching behavior in P1.

7.4 Save System (Single Pipeline)

- P1 implements a **single JSON-based save pipeline** as described in **2.8 Saves & Persistence** and **4.7 Tech & Infra**.
- Tools must not:
  - Add a second “temporary”, “debug”, or alternate save format.
  - Implement engine-native saves in addition to JSON in P1.
- Any change to save format is a **future-phase decision** and must be accompanied by an update to the Master PRD and this Sub-PRD.

7.5 Debug Surface

- All debug helpers (`teleport_to_port`, `give_gold`, `set_port_owner`, `spawn_enemies`, etc.) must be implemented through the **single debug module** described in **4.7 Tech & Infra**.
- Tools must not introduce extra top-level debug entrypoints (random hotkeys, hidden cheats in gameplay systems) that bypass this module.
- New debug actions, if required, should extend the same debug surface rather than adding new mechanisms.

7.6 Interaction with .cursor/rules.json and Tooling

- .cursor/rules.json and other tool guardrail configs must enforce:
  - Scope rules from **sections 2 (Core P1 vs Stretch)** and **3 (Non-Goals)**.
  - No-parallel-systems and schema-discipline rules from **7.2–7.3**.
- In case of any conflict:
  - **Master PRD invariants (A-section, schemas)** remain the ultimate source of truth.
  - This Sub-PRD defines the binding interpretation for Phase 1 behavior and implementation boundaries.
