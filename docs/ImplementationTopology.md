This document concretizes A2 (Engine & Implementation) of the Master PRD.
Master PRD + Phase Sub-PRDs define what the game is; this doc defines where & how code and data live in Unity.
If this doc ever conflicts with the Master PRD, Master PRD wins and this must be updated.

This document concretizes A2 (Engine & Implementation) of the Master PRD, which defines Unity 6 LTS, URP, C# as the engine choice for Booty!. The Master PRD is authoritative for design; this document is authoritative for Unity project layout.

0. Engine & Global Rules

Engine: Unity 6 LTS (or latest 6.x LTS at project start).

Language: C# only for game logic.

Rendering: URP.

Physics: 3D physics allowed, but all ship motion is on a 2D nav-plane.

Input (P1): classic Unity Input API only (GetAxis, GetKey).

New Input System is banned in P1. Migration, if any, must be a future PRD change.

Pattern constraints (P1–1.0 unless PRD says otherwise):

GameObject + MonoBehaviour only.

No DOTS/ECS, no DI frameworks, no external architecture libraries.

No multiplayer stack, no networking libraries.

1. Project Layout (Folders & Namespaces)
1.1 Folder Structure (Unity Assets/)

All game content lives under Assets/Booty/.

Required layout (at least):

Assets/Booty/Code/Core/

World/

Player/

Ships/

Combat/

Ports/

Economy/

Assets/Booty/Code/Infra/

Bootstrap/

Config/

Save/

Debug/

Util/

Assets/Booty/Code/Dev/
(dev-only scripts: prototypes, test harnesses)

Assets/Booty/Scenes/

Assets/Booty/Prefabs/

Assets/Booty/Art/

Assets/Booty/Config/
(JSON TextAssets: ports, ships, factions, economy, etc.)

Folder Guardrails

Game logic C# files must live under Assets/Booty/Code/**.

Dev-only C# files:

Must live under Assets/Booty/Code/Dev/**.

Production code must not depend on anything in Dev.

No additional root-level game folders under Assets/ (no Assets/Game/, Assets/Scripts/, etc).

1.2 Namespaces

All C# namespaces must be rooted at Booty.

Examples:

Booty.World

Booty.Player

Booty.Ships

Booty.Combat

Booty.Ports

Booty.Economy

Booty.Save

Booty.Config

Booty.Debug

Booty.Bootstrap

Booty.Dev (for Code/Dev/** only)

Namespace Guardrails

No root namespaces other than Booty.*.

Banned: Game, Core, MyCompany, etc.

Booty.Dev types must not be referenced from non-Dev namespaces.

2. Scenes & Boot Flow
2.1 Allowed Scenes (P1)

Assets/Booty/Scenes/MainMenu.unity

Assets/Booty/Scenes/World_Main.unity

Assets/Booty/Scenes/Test_*.unity (dev-only)

Runtime scenes:

P1 shippable loop must run entirely in World_Main.unity.

MainMenu.unity is optional; if present, it only:

Shows a simple menu.

Loads World_Main and exits.

2.2 Boot Flow Rules

Game starts in MainMenu (if it exists); otherwise directly in World_Main.

GameRoot (see below) exists only in World_Main.

No other scene may contain global bootstrap managers.

2.3 Test Scenes

Any dev/test scene must be named Test_* and live under Assets/Booty/Scenes/.

Test scenes:

May reference production systems.

Must not be required for the shipped loop.

Must not define their own bootstrap or global singletons.

3. Runtime Architecture (GameRoot & Systems)
3.1 GameRoot

Prefab: Assets/Booty/Prefabs/GameRoot.prefab
Script: Booty.Bootstrap.GameRoot (Assets/Booty/Code/Infra/Bootstrap/GameRoot.cs)

Responsibilities:

On Awake:

Ensure singleton instance (see below).

Initialize ConfigService.

Load or create GameState via SaveSystem.

Instantiate / wire all core systems as components on itself or direct children.

On Start:

Apply initial world state (ports, player ship, ownership, etc.) per current phase PRD.

Singleton Rules:

GameRoot is the only class allowed to expose public static GameRoot Instance.

No other class may use static Instance singletons or global static state as a service locator.

3.2 Systems (High-Level Pattern)

Each core system is a MonoBehaviour on GameRoot (or a child), not a random scene object.

Examples (names may evolve with PRDs):

PlayerSystem (Booty.Player)

ShipController (player ship, Booty.Ships)

CombatSystem (Booty.Combat)

PortSystem (Booty.Ports)

EconomySystem (Booty.Economy)

SaveSystem (Booty.Save)

ConfigService (Booty.Config)

CameraController, HudController (Booty.World)

DebugConsole (Booty.Debug)

Dependency Direction:

Systems may reference:

ConfigService

SaveSystem

PlayerSystem

PortSystem

No cyclic dependencies between systems.

Systems get references:

Via serialized fields wired on GameRoot in the prefab, or

Via explicit initialization code inside GameRoot (constructor-style wiring).

3.3 Manager / Singleton Guardrails

Only one global anchor: GameRoot.

Banned:

Additional *Manager prefabs living outside GameRoot.

DontDestroyOnLoad singletons.

Static service locators or global static fields for cross-system access.

Any new cross-cutting service must:

Live under Booty/Code/Infra/**.

Be added as a component on GameRoot (or immediate child).

Be wired through GameRoot initialization.

4. Data & Persistence (JSON-Only)
4.1 Canonical Schemas → C# Types

Master PRD schemas (port, ship, player slice, economy, faction, etc.) map to one canonical C# class each.

Examples:

port → Booty.Config.PortConfig

ship → Booty.Config.ShipConfig

faction → Booty.Config.FactionConfig

economy → Booty.Config.EconomyConfig

Top-level save → Booty.Save.GameState

Schema Rules:

Field names follow JSON keys where reasonable.

Fields present but inactive for a phase are allowed but may be unused by logic.

No forked/parallel types:

Banned: PortLite, PortDTO, ShipSaveState that diverge from schema.

Any new fields require:

PRD/schema update first.

Then C# GameState / config class update.

4.2 Static Config (Ports, Ships, Factions, Economy)

Location: Assets/Booty/Config/*.json (TextAssets).

Loaded by: Booty.Config.ConfigService.

ConfigService pattern:

ConfigService has serialized TextAsset fields for each config file.

On Awake, it deserializes each JSON into in-memory collections using a single JSON library.

Provides read-only accessors, e.g.:

IReadOnlyList<PortConfig> Ports { get; }

IReadOnlyList<ShipConfig> Ships { get; }

IReadOnlyList<FactionConfig> Factions { get; }

EconomyConfig Economy { get; }

Config Guardrails:

JSON is the only canonical config format.

ScriptableObjects must not be used as a second config pipeline.

They may be used only as thin view helpers (if ever), not as source of truth.

No Resources.Load or StreamingAssets for config in P1:

Config JSON is always supplied as serialized TextAsset references on ConfigService.

4.3 Save System (GameState)

Single top-level save struct: Booty.Save.GameState.

Location on disk:
Application.persistentDataPath + "/booty_save.json".

SaveSystem API:

GameState LoadOrNew()

void Save(GameState state)

Save Guardrails:

Save pipeline is JSON-only for P1–1.0.

No alternate save formats (binary, XML, PlayerPrefs) for canonical game state.

Only SaveSystem may perform disk I/O for game saves.

Any change to GameState fields must be consistent with PRD schemas.

5. Debug, Dev, and 3rd-Party Code
5.1 Debug Console

Prefab: Assets/Booty/Prefabs/DebugConsole.prefab

Script: Booty.Debug.DebugConsole.

Rules:

All debug commands (teleport, give gold, set port owner, spawn enemies, etc.) live only in DebugConsole (or helpers it owns).

No scattered debug hotkeys in random systems.

Production systems may not depend on DebugConsole.

5.2 Dev-Only Code

Dev-only scripts (prototyping, test harnesses) live under:

Assets/Booty/Code/Dev/**

Namespace Booty.Dev.*.

Rules:

Booty.Dev.* must not be referenced from any non-Dev namespace.

Test scenes (Test_*) may reference both production systems and Booty.Dev scripts.

5.3 3rd-Party Packages

3rd-party packages (assets, utilities) must be wrapped behind adapter classes in Booty.Code.Infra (e.g. Booty.Infra.XYLibraryAdapter).

Production code must not call 3rd-party APIs directly all over the codebase.

Adapters are the only place where 3rd-party namespaces appear.

6. AI / Cursor Guardrails (Topology-Level)

In addition to all Master PRD + Sub-PRD guardrails, the following are binding:

Engine & Pattern:

Implementation assumes Unity 6 + C# + MonoBehaviour/GameObject.

No DOTS/ECS, no Godot/Unreal-style patterns.

Folders & Namespaces:

Game logic C# files must be placed under Assets/Booty/Code/** with namespaces rooted at Booty.*.

Dev-only code under Assets/Booty/Code/Dev/** and Booty.Dev.*.

No new root folders or root namespaces.

No Parallel Systems:

There must never be multiple competing systems for the same concern:

No SaveSystemV2, PortSystemExperimental, CombatSystem_Alt, etc.

Extending behaviour means modifying existing systems or adding small helpers under the same domain namespace and wiring them through GameRoot.

Singleton / Manager Discipline:

GameRoot is the only singleton with a static Instance.

No other static global access patterns; no extra *Manager singletons.

No DontDestroyOnLoad managers; scene lifetime is controlled through GameRoot and the allowed scenes.

Config & Save Discipline:

All configs and saves use JSON and the canonical C# types mirroring PRD schemas.

No second config or save pipeline.

Scene Discipline:

Shippable loop lives in World_Main.unity only.

MainMenu (if used) only loads World_Main and never contains GameRoot.

Test_* scenes are dev-only; they cannot introduce new bootstrap or managers.