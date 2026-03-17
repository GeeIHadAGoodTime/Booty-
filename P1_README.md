# Booty! A Pirates Rise - P1 Vertical Slice

## How to Run

1. Open Unity 6 LTS
2. Open the project (may show compilation errors initially)
3. **If compilation errors appear**: Select "Ignore" to continue loading
4. Open scene: `Assets/Booty/Scenes/World_Main.unity`
5. Press Play (▶️ button)

The BootyBootstrap system will automatically set up the entire game procedurally when the scene loads.

**Note**: The project uses Unity UI components. If you get UI-related errors, ensure the Unity UI package is installed via Window → Package Manager.

## Troubleshooting

### "Compilation errors" on project open
- **Cause**: Unity detected script compilation issues during import
- **Fix**: Select "Ignore" to continue loading, then check Console for specific errors
- **Prevention**: The project should compile cleanly after the initial load

### "UI components not found" errors
- **Cause**: Unity UI package not installed or not properly imported
- **Fix**:
  1. Go to Window → Package Manager
  2. Search for "Unity UI" or "UI"
  3. Install the Unity UI package
  4. Restart Unity

### Game doesn't start when pressing Play
- **Cause**: Bootstrap system failed to initialize
- **Fix**: Check Unity Console for error messages
- **Verify**: Look for "BootyBootstrap: Setting up P1 scene..." in console

### No ships/ports visible in scene
- **Cause**: Bootstrap failed or scene not set up
- **Fix**: Restart Unity and try again, or check Console for setup errors

## What You'll See

### Core Gameplay Loop (30-60 minutes)

1. **Sailing** - Use WASD to move your blue ship around the isometric world
2. **Combat** - Press Q/E or left/right mouse to fire broadsides at red enemy ships
3. **Port Capture** - Sail near red enemy ports to trigger battles, sink defenders, then press **Y** to capture (or **N** to leave it enemy-held)
4. **Income** - Captured ports turn blue and generate gold every 30 seconds
5. **Repairs** - Dock at friendly/player ports and press **R** to repair for gold

### UI Elements
- HP Bar: Shows ship hull integrity
- Gold Display: Current gold amount
- Renown Display: Fame level and title
- Guns Ready: Shows broadside cooldown status

### Debug Commands (F1 to show menu)
- F2: Give 1000 gold
- F3: Teleport to home port
- F4: Set nearest enemy port to player ownership
- F5: Spawn enemy ship nearby
- F6: Save game
- F7: Load game

## Technical Implementation

### Systems Implemented
- ✅ Isometric camera with smooth follow
- ✅ Arcade ship movement on 2D nav plane
- ✅ Broadside combat with projectile physics
- ✅ Enemy AI with patrol/engage states
- ✅ Port ownership and capture mechanics
- ✅ Economy with income ticks and repairs
- ✅ Renown progression system
- ✅ JSON-based save/load system
- ✅ Debug console with test commands

### Scene Setup
Everything is created procedurally by `SceneSetup.cs`:
- Player ship with all components
- 3 enemy ships with AI
- 4 ports (1 home, 3 enemy)
- Projectile prefabs
- UI canvas with HUD elements
- GameRoot with all system managers

## Known Limitations (P1 Scope)

- Simple cube/sphere visuals (not final art)
- Basic UI text elements
- No sound effects
- Limited enemy variety
- No procedural quests or tavern interactions
- Static port placement

## Testing the Loop

1. Sail around and find enemy ships
2. Fight and sink them (gain renown)
3. Approach enemy ports to trigger capture
4. Watch ports turn blue and start generating income
5. Use gold to repair at home port
6. Save/load to test persistence

The game should run stably for 30-60 minutes without crashes!
