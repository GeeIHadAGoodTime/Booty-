# Booty! — Testing & Setup Guide

Step-by-step guide for anyone new to the project. No prior knowledge assumed.

---

## Section 1: Prerequisites

### Unity Version

- **Unity 6 LTS** (version **6000.0.x**) is required — do not use Unity 2022 or 2023.
- Install via **Unity Hub**: [https://unity.com/download](https://unity.com/download)
- In Unity Hub, go to **Installs → Add** and select **Unity 6 (LTS)**.
- During install, include the **Windows Build Support (IL2CPP)** module (optional but recommended).

### Project Location

```
J:\CLAUDE\PROJECTS\Booty!
```

The project folder must remain at this exact path. If you move it, update the
`-projectPath` argument in any scripts you run.

### Required Packages (from `Packages/packages-lock.json`)

All packages below are already listed in the project's lock file and will be
resolved automatically when you open the project in Unity Hub. No manual
installation is needed.

| Package | Version | Purpose |
|---------|---------|---------|
| `com.unity.render-pipelines.universal` | 17.2.0 | URP renderer (required — project uses URP) |
| `com.unity.render-pipelines.core` | 17.2.0 | URP core pipeline |
| `com.unity.shadergraph` | 17.2.0 | Shader Graph support |
| `com.unity.test-framework` | 1.4.6 | **Unity Test Framework** (NUnit-based tests) |
| `com.unity.test-framework.performance` | 1.3.2 | Performance test utilities |
| `com.unity.inputsystem` | 1.14.2 | New Input System (ship controls) |
| `com.unity.ugui` | 2.0.0 | Unity UI (HUD, debug console) |
| `com.unity.timeline` | 1.8.9 | Timeline / cutscene support |
| `com.unity.visualscripting` | 1.9.8 | Visual Scripting |
| `com.unity.2d.animation` | 12.0.2 | 2D animation support |
| `com.unity.2d.spriteshape` | 12.0.1 | Sprite Shape renderer |
| `com.unity.2d.tilemap.extras` | 5.0.1 | Extended tilemap tools |
| `com.unity.2d.psdimporter` | 11.0.1 | Photoshop file importer |
| `com.unity.2d.aseprite` | 2.0.2 | Aseprite file importer |
| `com.unity.burst` | 1.8.25 | Burst compiler (performance) |
| `com.unity.collections` | 2.6.2 | Native collections (used by Burst) |
| `com.unity.mathematics` | 1.3.2 | High-performance math |
| `com.unity.collab-proxy` | 2.10.2 | Unity Version Control proxy |
| `com.unity.ide.rider` | 3.0.38 | JetBrains Rider integration |
| `com.unity.ide.visualstudio` | 2.0.25 | Visual Studio integration |

> **Note:** `manifest.json` was not present at the time of writing, but all
> packages above appear in `packages-lock.json` and will be used as-is.

---

## Section 2: Opening the Project

1. Open **Unity Hub**.
2. Click **Open** (top-right) → **Add project from disk**.
3. Navigate to `J:\CLAUDE\PROJECTS\Booty!` and click **Add Project**.
4. Click the project tile to open it. Unity will begin importing assets.
   - First open typically takes **2–5 minutes** while shaders and assets compile.
   - Progress bar appears at the bottom of the Unity Editor window.
5. Unity may show **compilation warnings** in the Console panel — these are
   expected while the project is under active development.
6. If Unity asks to upgrade the project, click **No** unless you are intentionally
   upgrading from an older Unity version.

---

## Section 3: Running the SceneSetup Tool

The `World_Main` scene must be generated before you can enter Play Mode.

1. In the Unity menu bar, click **Booty → Setup World_Main Scene**.
2. Unity will create `Assets/Booty/Scenes/World_Main.unity` containing:
   - Ocean plane
   - Port GameObjects (Port Haven, Fort Imperial, Smuggler's Cove, Isla del Oro)
   - IsometricCamera
   - Bootstrap GameObject with `BootyBootstrap` component
3. After the tool finishes, configure the Inspector fields:
   - In the **Hierarchy** panel, find the **Bootstrap** GameObject.
   - Select it and locate the **BootyBootstrap** component in the Inspector.
   - Find the **GameRoot** GameObject → **ConfigService** component.
   - Assign the `ports.json`, `factions.json`, and any other `TextAsset` fields
     by dragging the files from `Assets/Booty/Config/` into the slots.
4. Press **Ctrl+S** to save the scene.

> If the menu item **Booty → Setup World_Main Scene** is missing, check the
> Unity Console for script compilation errors. All `.cs` files in
> `Assets/Booty/Code/` must compile cleanly before Editor Tools appear.

---

## Section 4: Entering Play Mode

1. In the **Project** panel, navigate to `Assets/Booty/Scenes/`.
2. Double-click **World_Main.unity** to open it.
3. Press the **Play** button (triangle icon at the top) or press **Ctrl+P**.

**Expected state on first Play Mode entry:**
- Ocean plane visible across the world.
- Player ship spawned as a **green capsule** at or near Port Haven (position ~(-40, 0, 30)).
- Two enemy ships spawned as **red capsules**.
- HUD visible in the top-left corner showing Gold / HP / Renown.

### Controls

| Key | Action |
|-----|--------|
| W / Up Arrow | Accelerate forward |
| S / Down Arrow | Decelerate / reverse |
| A / Left Arrow | Turn left |
| D / Right Arrow | Turn right |
| Space | Fire broadside cannons |
| F | Dock at a nearby friendly port |
| Y | Accept a port capture prompt |
| N | Decline a port capture prompt |
| F1 | Toggle the debug console overlay |

---

## Section 5: Using the Debug Console

Press **F1** to open the debug console overlay at runtime.

### Available Commands

| Command | Effect |
|---------|--------|
| `give_gold 1000` | Add 1000 gold to the player |
| `set_hp 80` | Set player ship HP to 80 |
| `teleport port_haven` | Teleport to Port Haven (x=-40, z=30) |
| `teleport fort_imperial` | Teleport to Fort Imperial (x=50, z=40) |
| `teleport smugglers_cove` | Teleport to Smuggler's Cove (x=10, z=-50) |
| `teleport isla_del_oro` | Teleport to Isla del Oro (x=-30, z=-30) |
| `spawn_enemy` | Spawn an enemy ship near the player |
| `capture_port port_haven` | Instantly capture Port Haven |
| `show_state` | Print current gold, HP, and position to the console |

### Port Reference (from `Assets/Booty/Config/ports.json`)

| Port ID | Name | Position (x, z) | Starting Faction | Income | Defense |
|---------|------|-----------------|-----------------|--------|---------|
| `port_haven` | Port Haven | (-40, 30) | Player Pirates | 50/turn | 2.0 |
| `fort_imperial` | Fort Imperial | (50, 40) | Spanish Crown | 80/turn | 4.0 |
| `smugglers_cove` | Smuggler's Cove | (10, -50) | Neutral Traders | 30/turn | 1.0 |
| `isla_del_oro` | Isla del Oro | (-30, -30) | Spanish Crown | 100/turn | 5.0 |

---

## Section 6: Running Automated Tests

The project uses **Unity Test Framework** (NUnit-based, package version 1.4.6)
for EditMode and PlayMode tests.

### Method A: Unity Test Runner (In-Editor, Recommended)

1. In Unity menu bar: **Window → General → Test Runner**.
2. The Test Runner panel opens. Select the **EditMode** tab.
3. Click **Run All**.
4. Results appear inline — green check = pass, red X = fail.
5. Click a failed test to see the assertion message and stack trace.

For **PlayMode** tests, switch to the **PlayMode** tab and click **Run All**.
Unity will enter Play Mode briefly to execute the tests.

### Method B: Command Line — Compilation Check (`check_compilation.ps1`)

Verifies that all C# scripts compile without errors (does not run tests):

```powershell
cd J:\CLAUDE\PROJECTS\Booty!
.\check_compilation.ps1
```

Expected output when healthy:
```
COMPILATION SUCCESS — Zero errors.
```

### Method C: Command Line — Full Test Run (`run_tests.ps1`)

Runs Unity Test Framework tests in headless batch mode:

```powershell
# EditMode tests (default)
cd J:\CLAUDE\PROJECTS\Booty!
.\run_tests.ps1

# PlayMode tests
.\run_tests.ps1 -TestPlatform PlayMode

# Both suites
.\run_tests.ps1 -TestPlatform All

# Custom Unity install path
.\run_tests.ps1 -UnityPath "D:\Unity\6000.0.47f1\Editor\Unity.exe" -TestPlatform EditMode
```

**Exit codes:**
- `0` — all tests passed
- `1` — one or more tests failed, or Unity failed to launch

**What the script does:**
1. Verifies Unity.exe exists at the specified path.
2. Launches Unity in `-batchmode -nographics` with `-runTests`.
3. Waits for Unity to finish (30–120 seconds depending on machine).
4. Parses the NUnit XML output file from `%TEMP%`.
5. Prints pass/fail counts and the names of any failed tests.
6. Exits with code 0 (all pass) or 1 (any failure).

**Tip:** On first run the Library folder must be rebuilt. This adds ~2 minutes.
Subsequent runs reuse the cached Library and are faster.

---

## Section 7: HUD Reference

The HUD is displayed in the **top-left corner** during Play Mode.

| Field | Description |
|-------|-------------|
| **Gold** | Current gold balance. Increases from port income and combat. |
| **HP** | Current hull points / Maximum hull points (e.g., `75 / 100`). |
| **Renown** | Player reputation score. Increases by capturing ports and winning battles. |

---

## Section 8: Troubleshooting

### "Scripts have compiler errors" / Red errors in Console

- Go to **Window → General → Console** and read the error messages.
- Ensure all `.cs` files under `Assets/Booty/Code/` are present.
- Try **Assets → Reimport All** if errors appeared after moving files.
- Run `.\check_compilation.ps1` from PowerShell for a clean headless check.

### Black screen in Play Mode

- The `IsometricCamera` GameObject may be missing from the scene.
- Solution: Exit Play Mode, run **Booty → Setup World_Main Scene** again, save with Ctrl+S, then re-enter Play Mode.

### No ship spawns

- The **Bootstrap** GameObject must have a **BootyBootstrap** component attached.
- The `playerShipPrefab` field may be unassigned — a fallback capsule ship will still spawn automatically even without a prefab.
- Check the Console for any `NullReferenceException` on startup.

### HUD is invisible

- Confirm the **Canvas** GameObject exists in the Hierarchy with a **HUDManager** component.
- The Canvas Render Mode should be **Screen Space - Overlay**.

### Tests not found in Test Runner

- Confirm `com.unity.test-framework` version 1.4.6 is listed in **Window → Package Manager → In Project**.
- Test files must live in folders with an `asmdef` that references `UnityEngine.TestRunner` and `UnityEditor.TestRunner`.
- Check that test `.cs` files have the `[TestFixture]` and `[Test]` attributes from `NUnit.Framework`.

### `run_tests.ps1` fails with "Unity.exe not found"

- Find your Unity 6 LTS install: open Unity Hub → **Installs** → right-click the 6000.0.x version → **Show in Explorer**.
- Pass the correct path: `.\run_tests.ps1 -UnityPath "C:\path\to\Unity.exe"`

### `run_tests.ps1` times out or Unity crashes during batch run

- Check the Unity log file printed by the script (in `%TEMP%\booty_test_*.log`).
- Common cause: the Library cache is stale. Delete `J:\CLAUDE\PROJECTS\Booty!\Library` and retry (Unity will rebuild it — allow 3–5 minutes).

### Port capture prompt never appears

- Sail within docking range of a port you don't own (watch for an on-screen prompt).
- If no prompt appears, check that the **PortManager** and **InteractionService** GameObjects exist in the Hierarchy and have no errors in the Console.

---

## Quick Reference Card

```
Open project  → Unity Hub → Add → J:\CLAUDE\PROJECTS\Booty!
Build scene   → Booty menu → Setup World_Main Scene → Ctrl+S
Play          → Ctrl+P
Debug console → F1 (in Play Mode)

Compile check → .\check_compilation.ps1
Run tests     → .\run_tests.ps1 [-TestPlatform EditMode|PlayMode|All]
In-editor     → Window → General → Test Runner → Run All
```
