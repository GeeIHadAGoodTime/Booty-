Booty! A Pirates Rise – Master PRD v1.3 (AI-Hardened)

A. Vision & Invariants
A0. Working Title & Logline

Working Title
Booty! A Pirates Rise

Logline
An isometric naval action–strategy RPG where you rise from nobody captain to vassal lord or independent ruler, commanding ships, capturing ports, shaping regional politics, and carving a maritime kingdom on a living strategic map.

A1. Product Fantasy

Naval Mastery (Isometric)
Expressive, arcade-style ship handling in an isometric view with readable, satisfying naval gun combat.

Territory & Ports
Ports you capture matter: income, defense, strategic map control.

Politics & War
Factions wage wars, offer vassalage, pressure you diplomatically, and betray you and each other.

Rise to Power
Start with one ship → end with a small fleet and a recognized (or reviled) maritime kingdom.

Living Strategic Map
Wars, blockades, convoys, and port ownership shifts redraw the map over time.

No New Pillars Pre-1.0 (Hard Rule)
No new core pillars may be added before 1.0. All features must slot into existing pillars and schemas. Cursor/AI must never introduce features that effectively constitute a new pillar (e.g. land combat, interior simulation, character action combat) or promote a non-goal to “pillar” status without an explicit, human-authored change to this section and a PRD version bump.

A2. Target Platform

Platform: PC (Steam), Windows-only for v1.

Engine: Unity 6 LTS, URP, C# only. No alternate engines or languages for the core game.

Engine is chosen: Unity 6 LTS, URP, C# only. The Implementation Topology document defines the canonical Unity project and folder layout.

See docs/ImplementationTopology.md for the canonical Unity project layout (folders, scenes, and where gameplay code lives).

Input:

Keyboard + mouse first.

Gamepad-friendly UI as stretch for EA / 1.0.

A3. Visual & Camera Constraints (Isometric Commitment)

These are non-negotiable scope constraints, not “features”:

Camera

Fixed or gently-following isometric camera.

No free third-person orbit, no behind-the-ship chase cam.

World

3D or 2.5D assets rendered to an isometric plane.

Ships move on a 2D navigation plane.

Water & Physics

Water = visual/shader-based only.

No physical waves or buoyancy simulation.

No complex ocean physics.

Sailing Model

Arcade movement: speed/turn-rate curves on the 2D plane.

Optional “wind” = at most a light scalar on speed, not a full simulation.

Combat

Broadside arcs as 2D cones/sectors.

Projectiles travel in 2D; no ballistic 3D curvature required.

These constraints prevent a stealth pivot into “ship sim” and keep combat readable.

A4. Pillars (Immutable)

Pillars are constant; only depth changes per phase:

Sailing & Combat – Isometric, arcade naval battles.

Ports & Ownership / Governance – Capture, income, defense, governance scalars.

Factions, Vassalage, Diplomacy – Attitudes, wars, contracts.

Economy & Trade – Prices, convoys, blockades.

From Captain → Ruler – Renown, rank, vassalage, (light) kingdom.

Playable Sandbox – Non-linear, emergent loops.

Modding & Extensibility – Config-first, meta-fields, later API.

No new pillars before 1.0.

A5. Release Strategy (Solo-Dev Reality)

Phases:

Pre-Alpha: Vertical slice (1 region, 1 ship, 3–4 ports).

Full Alpha (EA v1): Small but real sandbox (1–2 regions, basic politics, vassalage).

Pre-Beta: Depth pass (fleets up to 3 ships, convoys, independence flag & light effects).

Full Beta: Integrated core game (wars, blockades, legitimacy as a simple pressure knob, more content).

1.0 Gold: Polish, content, documented config-mod support.

Practical Solo-Dev Note

For a solo dev, 1.0 can honestly ship at Pre-Beta or a trimmed Full Beta.

Remaining Beta / DLC / stretch features can be shipped as post-launch updates.

Full DLC/stretch features (deep laws, rebellions, big fleets, multiplayer, LLM layer) are contingent on success and/or extra resources.

Scope Softening (Systemic Depth)

“Bannerlord-lite” systemic wars + convoy-driven economy + deep independence are schema-level ambitions, not obligations for 1.0.

Early implementations may be:

Simple thresholds plus scripted overrides for wars.

Mostly event-driven prices with convoys as light modifiers.

Independence as cosmetic + scalar effects, not a full new mode.

A6. Tech & Infra Constraints

To protect solo-dev scope and avoid backdoor complexity:

Single-player only for 1.0.

Offline-first:

No server infrastructure.

No always-online requirements.

Saves:

Local saves only for 1.0.

No cloud-sync or account systems.

Netcode:

No engine-native multiplayer integration in 1.0.

Multiplayer experiments only on a separate stretch track.

Platforms:

Windows PC only at launch.

Consoles are future ports, not part of this PRD’s 1.0 scope.

Performance target:

Reasonable mid-tier PC hardware (exact spec TBD in a separate tech note, but assume “not potato, not ultra high-end”).

A6.x Code Architecture & Composition Root

BootyBootstrap + GameRoot are the only composition root for runtime wiring in P1–Alpha.

Gameplay logic (movement, combat, economy, ports, save/load) must live in dedicated systems (EconomySystem, PortSystem, PortInteractionSystem, SaveSystem, etc.), not in BootyBootstrap or GameRoot.

Cross-system dependencies are wired via explicit Initialize/Configure methods on each system, with dependencies stored in [SerializeField] private fields.

No public fields for dependency wiring; read-only properties are allowed for data access.

Static helpers are allowed only for pure utilities (e.g., ShipFactory for prefab-free ship creation) and must not become god-objects.

A7. AI & Implementation Guardrails (Cursor/AI Contract)

This section defines how Cursor/AI must interpret and act on this PRD to avoid drift, duplicate systems, and tech debt.

A7.1 Authority & Mutability

Sections A0–A6, B, C, D, E, and Appendix A are the authoritative design and data spec.

Cursor/AI must treat these sections as read-only; only a human designer may change them.

Any change that contradicts these sections requires a human-edited PRD update and version bump.

D. Roadmap – Phase Unlock Table is the canonical phase spec for what exists in each phase.

E. Phase Specs may elaborate with examples and narrative but must not contradict D. If they differ, D wins for Cursor/AI and work-orders.

A7.2 Active Phase & Feature Gating

The project maintains an explicit CURRENT_PHASE value (e.g., "Pre-Alpha", "Full Alpha") in .cursor/rules.json or docs/project_state.md.

For any task, Cursor/AI must treat CURRENT_PHASE as the upper bound on what can be implemented.

Cursor/AI must not:

Implement or wire systems intended only for later phases than CURRENT_PHASE, unless the work-order explicitly names that later phase.

“Upgrade” a system to a later-phase spec while fixing a bug or doing a small enhancement.

Work-orders that span phases must explicitly say so (e.g., “implement this Full Alpha feature while CURRENT_PHASE = Pre-Alpha”).

A7.3 File & Module Creation Rules

Canonical file/module layout (by engine) lives in the Implementation Topology document.

Cursor/AI must:

Add new code only within the modules/folders declared there.

Not create new top-level packages or directories unless explicitly requested in a work-order and the topology has been updated by a human.

Not create alternate versions of the same system (e.g., ports_v2, PortSystem2, fleet_alt) unless explicitly requested as a migration path.

A7.4 Change Discipline

Bugfix tasks:

Modify the smallest possible surface (functions, methods, files).

Do not add new features, systems, or files.

Refactor tasks:

Must be explicitly requested as refactors.

Keep external behavior identical unless the work-order explicitly authorizes behavior changes.

Do not mix refactor and new feature work in the same change set.

New feature tasks:

Must name the target phase and the specific system(s) / pillar rows in D they implement.

May touch only schemas and systems required for that feature.

A7.5 Concept & Schema Uniqueness

Every gameplay concept has exactly one canonical representation in Appendix A (field/enum/etc.).

Cursor/AI must not:

Add “shadow fields” that duplicate existing concepts under new names (e.g., separate unrest scalar while stability exists).

Change the semantic meaning of existing fields without a human updating Appendix A and bumping PRD version.

New concepts must either:

Be added as new, clearly distinct fields to the appropriate schema; or

Live under the meta dictionary of that schema with a comment describing the new data.

A7.6 Composition Root Guardrails

When wiring systems together, AI must:

Prefer explicit Initialize/Configure methods over direct field access.

Keep BootyBootstrap free of gameplay logic; it may only create objects, call initializers, and configure camera/UI.

Avoid adding new composition roots or static god-objects without an explicit PRD update.

B. Non-Goals / Out-of-Scope (v1.0)

These are explicitly not in scope for 1.0 and should not be backdoored in:

No land battles or infantry combat.
All combat is ship-scale, naval encounters only.

No character action combat.
No on-foot swordfighting, third-person action, or FPS segments.

No 3D ocean physics simulation.
No buoyancy, wave dynamics, or full fluid simulation; water is visual only.

No ship interior simulation.
No walking around inside ships, no interior layouts.

No large (10+ ship) player-controlled mega-fleets for 1.0.
Player fleet is ≤ 3 ships for 1.0; big fleets & formal formations are DLC/stretch.

No full legal slider / deep law simulation in 1.0.
Laws, rebellions, and complex governance mechanics are DLC/stretch.

No multiplayer in 1.0.
Co-op / PvP is a separate stretch product, post-1.0 only if success/funding justify it.

No heavy scripting mod API in 1.0.
1.0 supports config-level modding (JSON) only; scripting API is DLC/stretch.

LLM Dialogue
Any LLM-driven dialogue is strictly cosmetic and must not delay or gate non-LLM features.

These non-goals protect the solo-dev scope and keep the game anchored to the isometric naval action–strategy core.

C. Data Model & Schema Rules (Overview)

The game uses a stable data model for:

Ports – Ownership, economy, governance, defense.

Factions – Attitudes, war states, diplomacy flags.

Player – Renown, rank, reputation, vassalage, kingdom state.

Fleets – Player and AI fleets, ship membership, officer assignment.

Ships – Classes, stats, crew, ammo.

Economy – Goods, world events, global knobs.

War Mechanics – Thresholds, score drivers for war/peace.

Quests & NPCs – Quest structures and light personalities.

C1. Schema Evolution Rule

Schemas are defined early and then evolve additively only once exposed to saves/mods:

You may add new fields and extend enums over time.

You must not remove or repurpose existing fields in a breaking way.

Backwards compatibility for shipped saves and documented config formats is required.

Each core schema has a meta dictionary reserved for future DLC/expansion data and advanced modding.

Details: Full field-level schemas live in Appendix A and are treated as the canonical reference for config and mod surfaces.

C2. Cursor/AI Schema Implementation Rules

Runtime representations of the schemas in Appendix A (classes/structs/data types) must have one canonical definition per schema in the codebase.

Cursor/AI must:

Map code-level representations directly to these schemas (field names and semantics must match).

Extend these canonical definitions in place when adding fields, not by creating new schema types.

Not create duplicate or divergent schema definitions across modules (e.g., Port, PortData, PortDTO with slightly different fields) unless explicitly specified in the Implementation Topology.

All game concepts that appear in code must map directly back to:

A field or enum in Appendix A; or

A documented meta extension for that schema.

C3. Schema Ownership & Write Rules

Each schema’s fields are owned by a single subsystem:

port.* → Ports & Governance system.

faction.* and war_mechanics.* → Factions & Wars system.

player.* → Progression & Vassalage system.

fleet.* and ship.* → Fleets & Combat system.

economy.* → Economy & Trade system.

quest.* and npc.* → Quests & Narrative system.

Rules for Cursor/AI:

Only the owning subsystem may directly write to its schema fields.

Other systems should request changes via clear APIs or service calls exposed by the owning subsystem.

Cursor/AI must not sprinkle direct writes to unrelated schemas across arbitrary modules (e.g., UI code should not directly mutate war_state or stability).

D. Roadmap – Phase Unlock Table

(One Loop per Pillar per Phase)

Key:

VS = Vertical Slice (Pre-Alpha).

EA v1 = Early Access launch (Full Alpha).

Depth = Pre-Beta.

Core Game = Full Beta.

Gold = 1.0 Release.

DLC/Stretch = Post-1.0, contingent on success.

Authority note: This table is the canonical phase spec for what exists in each phase. E. Phase Specs may elaborate with examples and narrative, but must not introduce new systems or requirements that contradict this table. If there is any discrepancy, this table wins for Cursor/AI and work-orders.

Note: “Systemic” features (wars, convoys, independence) may ship in simple, dumb, easily tunable forms at 1.0 and be deepened post-launch.

System / Pillar Pre-Alpha (VS) Full Alpha (EA v1) Pre-Beta (Depth) Full Beta (Core Game) 1.0 Gold DLC / Stretch

Sailing & Combat 1 ship, basic AI, 2–3 ship classes, basic Fleet intro: up to 3 ships, Refined multi-ship combat (still ≤3 ships), Tuned combat, full ship roster Larger fleets (3–5+), explicit formations using
isometric arcade combat ammo + 1 special simple orders (follow/hold/focus); better AI/readability, optional light for 1.0 scope fleet.formation, exotic ships, monsters
still small, readable engagements formation-like behaviors (heuristics only)

Ports & Governance Capture ports, simple income Port level (1–5) only tax_policy + stability activated; Stability-driven minor unrest events Governance polish: UI/feedback Deep laws & revolt mechanics (built on
simple scalar effects on income/defense affecting port income/defense; simple for level, tax, stability, unrest tax_policy, stability, kingdom, port.meta)

Economy & Trade Static prices base_price + local + supply/demand drift; AI trade convoys Blockade impact (route disruption) + Fully tuned price curves and Rare/legendary goods, more complex economic
(base_price only) event modifiers; mostly along simple routes; convoys initially lane shaping; convoy interceptions feeding content (goods, events, convoys) events, special trade regions; deeper systemic
event-driven with light light-impact modifiers on prices war/econ in a controlled way convoys if desired

Factions & Wars Static war backdrop war_state toggles via war_score introduced; simple AI wars Basic peace/war resolution (end conditions, Diplomacy tuning: attitude curves, Deeper alliances, coalitions, intricate treaties
(no dynamic changes) scripts (events) based on thresholds plus scripted truces), simple casus-belli-style modifiers offer frequencies, basic treaties and war goals (using faction.meta &
overrides to keep control via meta (no complex alliances) war_mechanics.meta)

Progression & Renown scalar only Rep + renown thresholds Richer contracts (stipend scaling, Deeper vassal politics (conflicting duties, Rank/title polish, better feedback Prestige paths, dynastic titles, legacy perks
Vassalage for offers; simple obligations), basic contract fail pressures) within existing schema for rep and contracts (extra data via player.meta)
vassalage contracts states

Independence & ❌ none ❌ none Independence flag (kingdom.is_independent) legitimacy used as a simple pressure knob Tuned “light kingdom” play w.r.t wars, Full crises, rebellions, deep law systems,
Kingdom + ports_owned tracked; independence affecting war likelihood and diplomacy economy, and vassalage within scalar international recognition games (using kingdom +
at this phase is mostly cosmetic + reaction; still a light scalar system, not a systems, not a full new mode faction.diplomacy_flags & meta)
scalar effects, not a new mode grand-strategy layer

Quests & Dialog 1–2 handcrafted quests, 3 procedural quest archetypes More quest archetypes using same Story arcs + simple personalities for Large quest pool; better dialog Expanded arcs, side campaigns, extra narrative
minimal dialog (bounty, escort, delivery) framework key NPCs flavor/branching within existing systems systems

Modding ❌ none Core data (ports, goods, factions, External JSON packs auto-loaded Stabilize config formats used in 1.0; Documented config/JSON format; Real mod API (scripting hooks) + tools,
ships) in JSON configs; from /mods folder; config-only light validation config-level modding considered using meta fields as extension points
internal use but structured modding, no scripting (no breaking changes) supported surface

Multiplayer ❌ none ❌ none ❌ none Internal tests only (if any) Maybe post-1.0, contingent on Co-op / other modes; full netcode as
(Stretch) success/funding separate, stretch product

LLM Cosmetic ❌ none ❌ none ❌ none ❌ none ❌ none Cosmetic LLM-driven dialogue layer;
Dialogue Layer non-systemic, reads schemas via summaries
only; always optional

E. Phase Specs (One Layer per Pillar per Phase)

All references to combat/sailing assume: isometric camera, 2D nav plane, arcade movement. E elaborates on D; when in doubt, D’s table is authoritative for Cursor/AI.

E1. Pre-Alpha – Vertical Slice

Goal
Prove isometric sailing + naval combat + port capture are fun and readable.

Scope

World

Regions: 1.

Ports: 3–4.

Static war backdrop, simple faction presence.

Player & Ships

1 player ship class.

Basic enemy ships (1–2 archetypes).

Single-ship combat only.

Economy

Static prices via base_price only.

No supply/demand or events yet.

Ports & Ownership

You can capture ports.

Owned ports pay simple income (fixed rate or simple formula).

Progression

renown scalar only.

Simple “letter of marque” system:

You’re allowed to target certain factions, get extra payouts.

No complex contracts UI.

Content

1 bounty-style quest.

1 rumor-style hook.

Tavern = simple interaction menu (no microgames; just placeholder UI).

Isometric Implementation Notes

Camera: Fixed/gentle-follow isometric camera.

Movement: Speed/turn curves for ships on 2D plane; no wind.

Combat:

Broadside arcs as 2D cones.

Simple projectile travel; hit detection in 2D.

AI: 2D steering, collision avoidance only.

Exit Criteria

30–60 minutes of playable loop:
Sail → Fight → Capture Port → Get Paid → Repeat.

Combat readable and responsive.

Port capture clearly alters map control (ownership flags, income).

Save/load stable for:

Player state.

Port ownership.

Basic inventory/economy.

E2. Full Alpha – Early Access v1

Goal
Deliver a small but real sandbox with basic politics, light governance scalar, simple wars (mostly scripted), and procedural quests.

Adds One Loop Per Pillar

Sailing & Combat

2–3 ship classes.

Ammo: round shot + 1 special (chain or grape).

Ports & Governance

port.level (1–5) activated.

Income/defense scale with level.

Economy

price_mod_local and price_mod_event active.

Local variations and event-based fluctuations.

Convoys may exist visually but are not yet systemic.

Factions & Wars

war_state toggles via scripts (events, story beats).

Still no full AI war simulation; scripted control is primary.

Progression & Vassalage

faction_rep + renown thresholds.

Real vassalage_contract structure with simple options (stipend, assignments).

Quests & Dialog

3 procedural quest archetypes (e.g., bounty, escort, delivery) using shared framework.

Modding

Core data in JSON:

Ports, goods, factions, ships.

Internal use but structured with external modding in mind.

Scope

Regions: 1–2.

Ports: 6–10.

Ship classes: 2–3 + 1 special ammo.

Quest pool: handcrafted + procedural mix.

Isometric Notes

Port levels visible via art swaps in isometric:

Harbor size, walls, visible buildings.

war_state changes:

Reflected via flag colors, overlays on ports.

Exit Criteria

10–20 hours of varied sandbox play.

Steam EA launch viable:

Simple but coherent loops across combat, trading, port capture, basic vassalage.

Stability:

Few hard crashes.

Basic bug tolerance.

E3. Pre-Beta – Depth Pass

Goal
Layer systemic depth without blowing scope. This is the “Bannerlord-lite taste” moment, not full parity.

Adds One Loop Per Pillar

Sailing & Combat – Fleets (Intro)

fleet.ships activated.

Player can command up to 3 ships.

Orders:

Follow (stick near flagship).

Hold (defend area).

Focus target (attack selected target).

UI remains minimal (simple squad-style commands).

Ports & Governance

tax_policy ("low" | "med" | "high") active.

stability active:

Affects income/defense multipliers.

No complex events yet, just scalar effects.

Economy & Trade

supply and demand active.

AI trade convoys appear along simple routes between ports.

Convoy arrivals/attacks:

Adjust port supply/demand and prices through bounded, easily tunable modifiers.

Convoys are still secondary to events; do not require perfect simulation.

Factions & Wars

war_score and war_mechanics active:

Port captures, major battles, convoy losses adjust scores between factions.

Simple AI wars:

Above war_score_threshold_start → war likely.

Below war_score_threshold_end → peace likely.

Designer scripts can override results to avoid degenerate or lore-breaking outcomes.

Independence & Kingdom

kingdom.is_independent flag can be set.

kingdom.ports_owned tracked.

Independence:

Faction attitudes and war likelihood shift based on:

Independence,

ports_owned,

renown.

At this phase, independence is mostly cosmetic + scalar effects, not a fully distinct game mode.

Quests & Dialog

More procedural archetypes reusing the same framework:

Variants of escort, blockade-breaking, convoy raids, etc.

Modding

External JSON packs auto-loaded from /mods directory:

New ports, goods, factions, or ships defined via config.

No scripting yet; config-only.

Scope Impact

Multi-ship combat via small fleets.

Trade activity visible on the map via convoy icons.

Wars now partially systemic instead of purely scripted, but still under designer control.

Isometric Notes

Fleets/convoys move along 2D paths.

Fleets:

Simple relative offset behavior (no formal formations).

Independence:

Player-owned ports show new banners/flags in the isometric view.

Exit Criteria

Sandbox feels like “Bannerlord-lite on water”:

Dynamic-feeling wars (even if partially scripted).

Trade routes.

Independence as a viable fantasy via cosmetics + modifiers.

Early fleet combat is functional and understandable.

E4. Full Beta – Integrated Core Game

Goal
All core loops (combat, ports, economy, wars, vassalage, independence/kingdom) integrated and stable. This is effectively the core game; 1.0 may be a trimmed version of this.

Adds One Loop Per Pillar

Sailing & Combat – Multi-Ship Polish

Still ≤ 3 ships in player fleet.

Better AI behaviors for fleet ships:

Smarter following, disengage behavior, target priorities.

Optional lightweight “formation-like” behaviors leveraging fleet.formation, but:

No heavy formation micromanagement.

No complex collision-heavy patterns.

Can be implemented as heuristics on top of offsets.

Ports & Governance

stability now drives minor unrest events:

Temporary income/defense penalties.

Simple UI feedback (icons, alerts) when unrest flares.

Still within scalar/governance system; no new law-sliders yet.

Economy & Trade

Blockades:

Ports/lane segments can be blockaded.

Blockades disrupt convoys, affecting supply/demand.

Lane shaping:

Some routes become more or less attractive based on risk.

System remains tunable and forgiving, not hardcore sim.

Factions & Wars

War/peace resolution:

Wars end based on war_score + timers.

Truces:

Temporary war_state with softer attitudes.

Diplomacy:

Simple offers/contracts based on attitudes and war state.

“Treaties” = simple state changes, not complex alliance graphs.

Kingdom (Independence)

kingdom.legitimacy now meaningful:

Low legitimacy → higher chance of wars, worse offers.

High legitimacy → fewer opportunistic wars, better treaties.

Integrated with:

Diplomacy decisions.

War likelihood.

Vassal offers.

Still a light scalar system, not a complicated grand-strategy layer.

Quests & Dialog

Story arcs:

Multi-step questlines with simple branching.

Personalities:

Key NPCs get stable traits affecting dialog and offers.

Modding

Mod API (still config-level) for content:

Ports, ships, units, quests defined via JSON.

Use of meta fields to allow future DLC/extension without schema changes.

No heavy scripting yet; but structure prepared.

Isometric Notes

Formations (if used) are simple 2D patterns (line, loose column) computed on nav plane; heuristics over strict physics.

Blockades:

Visualized via icons/areas around ports/lanes.

Interceptions trigger encounters.

Diplomatic states:

Color coding and overlays on map for war/peace/truce.

Exit Criteria

All core loops are present and interconnected.

Systems stable enough for:

Tuning.

Content passes.

UX improvements.

From a player POV, this is “the full game,” minus extra polish and stretch features.

E5. 1.0 Gold – Polished Release

Goal
Polished, content-rich, stable 1.0 with supported config-level modding. For a solo dev, this may sit at Pre-Beta or a trimmed Full Beta feature set.

Adds / Focuses

Governance & Feedback

Polished UI for port governance:

Clear surfaces for level, tax, stability, unrest.

Minor unrest events expanded within existing schema (no new systems).

Tutorial & UX

Onboarding/tutorial flows for:

Sailing & combat basics.

Port capture.

Trade & convoys (at their actual shipped depth).

Vassalage and independence (as scalar systems, not grand-strategy).

Tooltips, help overlays.

Balance Pass

Combat:

Ship stats, ammo, AI behaviors tuned.

Economy:

Price ranges, event frequencies, trade profitability tuned.

Diplomacy:

Attitude gain/loss, offer frequencies tweaked.

Progression:

Renown & rank pacing.

Modding

Documented JSON/config formats:

Ships, ports, goods, factions, quests.

Config-level mods considered a supported feature:

“If you stay within the documented config surface, it should work.”

Stability & Performance

Bugfixes.

Performance tuning on typical hardware targets.

Isometric Notes

Final camera & zoom tuning for readability in combat and on the map.

Clear visual feedback for:

Territory changes.

War/peace.

Stability/unrest events.

E6. DLC & Stretch Targets

Goal
Extend depth and fantasy without changing core schemas, using meta fields and content.

Built on Existing Schemas:

Deep Laws & Rebellions

Uses:

port.tax_policy

port.stability

player.kingdom fields

port.meta, player.meta, faction.meta

Adds:

Law sets per port/kingdom (stored in meta).

Rebellion events when stability and legitimacy thresholds are violated.

Optional “legal sliders” that shift stability and attitudes.

New Regions & Content

New region_id and port entries.

New factions and major powers via faction schema.

Special economic regions (rare goods, dangerous trade routes) using economy.meta.

Monsters & Exotic Ships

New enemy types and ship archetypes:

Sea monsters as special “ship/enemy” entries in combat tables.

Super-ships or unique flagships.

All implemented within existing combat systems.

Big Fleets & Explicit Formations

Expand practical fleet size:

fleet.ships with 3–5+ ships.

Use fleet.formation meaningfully:

Line, wedge, screen, column, etc.

Requires:

More advanced AI.

More performance work.

Clear stretch: only built if game success/funding justify it.

Advanced Modding (API + Tools)

Scriptable hooks:

Event listeners, quest logic, AI behaviors.

Tools:

Editors for ports, quests, encounters.

All built on top of:

JSON configs.

meta extension fields.

Multiplayer (Separate Stretch Track)

Co-op or other modes.

Uses existing systems where possible.

Requires separate netcode & UX track.

Explicitly:

Not part of 1.0.

Contingent on game success.

LLM Cosmetic Dialogue Layer (Stretch)

Cosmetic conversations:

Flavor dialog for NPCs.

Lore, banter, rumors.

Non-systemic:

No direct write access to game state.

Reads from schemas (via data summaries) and speaks back in-character.

Always optional / toggleable.

Must not delay non-LLM features.

Appendix A – Detailed Data Schemas

These are the canonical schema definitions for configs and modding surfaces, subject to the additive-only evolution rule. Cursor/AI must mirror these schemas exactly in the canonical runtime representations defined by the Implementation Topology.

A1. Port Schema

port = {
    "id": str,
    "name": str,
    "region_id": str,
    "faction_owner": "faction_id",

    // Economic
    "base_price": { "good_id": float },       // Pre-Alpha
    "price_mod_local": { "good_id": float },  // Alpha
    "price_mod_event": { "good_id": float },  // Alpha
    "supply": { "good_id": float },           // Pre-Beta
    "demand": { "good_id": float },           // Pre-Beta

    // Governance
    "level": int,                             // 1–5, Alpha
    "tax_policy": str,                        // enum: "low" | "med" | "high", Pre-Beta
    "stability": float,                       // 0–100, Pre-Beta

    // Military
    "defense_rating": float,                  // function of level, stability, etc.

    // Extension hook
    "meta": dict                              // free-form DLC/expansion data
}


A2. Faction Schema

faction = {
    "id": str,
    "name": str,
    "color": [int, int, int],

    "attitude": { "faction_id": float },  // -100 to +100; Alpha
    "war_score": { "faction_id": float }, // Pre-Beta

    "war_state": {                        // conceptual map:
        // war_state[other_faction_id] = "peace" | "truce" | "war"
    },

    "diplomacy_flags": {
        "is_major": bool,
        "is_pirate": bool,
        "recognizes_player_kingdom": bool
    },

    "meta": dict
}


Note: For simplicity, war_state can be represented as:

war_state[faction_id] = "peace" | "truce" | "war";


A3. Player Progression Schema

player = {
    "renown": float,
    "rank": str,                             // enum: "nobody" | "officer" | "noble" | "lord"
    "faction_rep": { "faction_id": float },  // -100 to +100

    "vassalage_contract": {
        "faction_id": str,
        "stipend": int,
        "fief_port_id": str | null,
        "obligations_active": bool
    } | null,

    "kingdom": {
        "is_independent": bool,     // Pre-Beta
        "ports_owned": [str],       // port_ids
        "legitimacy": float         // 0–100, Beta (used as a pressure knob)
    },

    "meta": dict
}


A4. Fleet Schema

fleet = {
    "ships": ["ship_id"],                    // size cap varies by phase (≤3 for 1.0)
    "officers": { "ship_id": "officer_id" }, // Pre-Beta (optional usage)
    "formation": str,                        // enum placeholder (e.g. "line", "column"); logic fully used in DLC/stretch
    "meta": dict
}


For 1.0, formation may be mostly cosmetic/simple; no requirement to ship deep formation logic before DLC.

A5. Ship Schema

ship = {
    "id": str,
    "name": str,
    "class": str,            // e.g. "sloop", "brig", "frigate"
    "hull": int,             // hit points / structural integrity
    "sail": int,             // sail health
    "speed": float,          // base speed scalar on 2D nav plane
    "turn_rate": float,      // turning agility
    "broadside_slots": int,  // number of cannons per side (or slots)
    "ammo_types": ["ammo_id"],

    "crew_min": int,
    "crew_optimal": int,

    "value": int,            // purchase/sell value
    "tier": int,             // rough power tier for progression

    "meta": dict
}


This schema backs:

Combat tuning.

Progression & unlocks.

AI variants (different loadouts and tier mixes).

A6. Economy Schema (Global)

economy = {
    "goods": ["good_id"],
    "world_events": ["event_id"],      // supply shocks, booms, famine, etc.
    "meta": dict
}


A7. War Mechanics Schema

war_mechanics = {
    "war_score_threshold_start": float,  // above this, war is likely
    "war_score_threshold_end": float,    // below this, peace is likely

    "score_drivers": {
        "port_capture": float,
        "major_battle": float,
        "convoy_loss": float
    },

    "meta": dict
}


In 1.0, war resolution is simple: thresholds + timers (with optional designer overrides) → war_state flips and attitude tweaks.

A8. Quest Schema

quest = {
    "id": str,
    "type": str,                 // "bounty" | "escort" | "delivery" | "story_arc" | ...
    "giver_npc_id": str | null,  // who gives it; may be null for generic board/tavern

    "requirements": {
        // Structured requirements: target faction, port_id, min_renown, etc.
    },

    "objectives": [
        {
            "objective_id": str,
            "kind": str,         // "sink_ship" | "deliver_goods" | "escort_convoy" | ...
            "params": { }        // ids, quantities, etc.
        }
    ],

    "rewards": {
        "gold": int,
        "renown": float,
        "faction_rep": { "faction_id": float },
        "items": ["good_id"]
    },

    "flags": {
        "is_repeatable": bool,
        "is_story_critical": bool
    },

    "meta": dict
}


This schema supports:

Handcrafted quests.

Procedural archetypes.

Simple story arcs via sequences of quests.

A9. NPC / Personality Schema (Light)

npc = {
    "id": str,
    "name": str,
    "role": str,                       // "governor" | "tavern_keeper" | "admiral" | ...
    "home_port_id": str | null,

    "faction_id": str | null,

    "personality": {
        "bravery": float,             // 0–1
        "greed": float,               // 0–1
        "honor": float,               // 0–1
        "talkativeness": float        // 0–1
    },

    "dialog_tags": [str],             // for flavor / LLM prompt context later

    "meta": dict
}


This supports:

Simple personality effects on offers and dialog.

Future LLM cosmetic layer by feeding personality + dialog_tags as context.